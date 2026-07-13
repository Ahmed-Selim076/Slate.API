using System.ComponentModel.DataAnnotations;

namespace Slate.Api.DTOs;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MinLength(1)] string DisplayName
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(
    string Token,
    Guid UserId,
    string Email,
    string DisplayName
);

public record UserProfileResponse(
    Guid UserId,
    string Email,
    string DisplayName
);

public record UpdateProfileRequest(
    [Required, MinLength(1), MaxLength(80)] string DisplayName
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword
);

public record ForgotPasswordRequest(
    [Required, EmailAddress] string Email
);

// DEV MODE: returns the raw token directly since there's no email service
// wired up yet. In production this must be emailed to the user instead of
// returned in the API response — returning it here is NOT secure.
public record ForgotPasswordResponse(
    string? ResetToken
);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword
);
