using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slate.Api.Data;
using Slate.Api.DTOs;
using Slate.Api.Models;
using Slate.Api.Services;

namespace Slate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly string _googleClientId;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthController(AppDbContext db, ITokenService tokenService, IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _googleClientId = config["Google:ClientId"]!;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == emailNormalized))
            return Conflict(new { message = "An account with this email already exists." });

        var user = new User
        {
            Email = emailNormalized,
            DisplayName = request.DisplayName.Trim(),
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailNormalized);

        if (user is null || user.PasswordHash is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin(GoogleLoginRequest request)
    {
        Google.Apis.Auth.GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(request.IdToken,
                new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _googleClientId },
                });
        }
        catch (Exception)
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }

        var emailNormalized = payload.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject || u.Email == emailNormalized);

        if (user is null)
        {
            user = new User
            {
                Email = emailNormalized,
                DisplayName = payload.Name ?? emailNormalized,
                GoogleId = payload.Subject,
            };
            _db.Users.Add(user);
        }
        else if (user.GoogleId is null)
        {
            user.GoogleId = payload.Subject; // link existing email/password account
        }

        await _db.SaveChangesAsync();

        var token = _tokenService.CreateToken(user);
        return Ok(new AuthResponse(token, user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetMe()
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFound();
        return Ok(new UserProfileResponse(user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }

    [Authorize]
    [HttpPatch("me")]
    public async Task<ActionResult<UserProfileResponse>> UpdateMe(UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFound();

        user.DisplayName = request.DisplayName.Trim();
        await _db.SaveChangesAsync();

        return Ok(new UserProfileResponse(user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }

    // The frontend resizes to a small square (~256px) JPEG/PNG before
    // sending, so a real upload lands well under this. The cap just guards
    // against something oversized ever reaching the database column.
    private const int MaxAvatarDataUrlLength = 700_000;

    [Authorize]
    [HttpPut("me/avatar")]
    public async Task<ActionResult<UserProfileResponse>> UpdateAvatar(UpdateAvatarRequest request)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFound();

        if (!request.AvatarDataUrl.StartsWith("data:image/"))
            return BadRequest(new { message = "That doesn't look like a valid image." });

        if (request.AvatarDataUrl.Length > MaxAvatarDataUrlLength)
            return BadRequest(new { message = "That image is too large. Try a smaller photo." });

        user.AvatarUrl = request.AvatarDataUrl;
        await _db.SaveChangesAsync();

        return Ok(new UserProfileResponse(user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }

    [Authorize]
    [HttpDelete("me/avatar")]
    public async Task<ActionResult<UserProfileResponse>> DeleteAvatar()
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null) return NotFound();

        user.AvatarUrl = null;
        await _db.SaveChangesAsync();

        return Ok(new UserProfileResponse(user.Id, user.Email, user.DisplayName, user.AvatarUrl));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(CurrentUserId);
        if (user is null || user.PasswordHash is null) return NotFound();

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect." });

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailNormalized);

        // Always return 200 regardless of whether the account exists, so this
        // endpoint can't be used to check which emails are registered.
        if (user is null) return Ok(new ForgotPasswordResponse(null));

        user.ResetToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        user.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        // DEV MODE: no email service configured yet, so the token is returned
        // directly. Swap this for an actual email send before going to production.
        return Ok(new ForgotPasswordResponse(user.ResetToken));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ResetToken == request.Token);
        if (user is null || user.ResetTokenExpiresAt is null || user.ResetTokenExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "This reset link is invalid or has expired." });

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiresAt = null;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
