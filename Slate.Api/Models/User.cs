namespace Slate.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;

    // Data URL (e.g. "data:image/jpeg;base64,...") of the user's profile
    // picture. Stored inline since the app has no separate blob storage yet;
    // the frontend resizes/compresses images client-side before upload to
    // keep this small.
    public string? AvatarUrl { get; set; }

    // Null for accounts created via Google OAuth (Phase 2)
    public string? PasswordHash { get; set; }

    public string? GoogleId { get; set; }

    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<BoardMember> BoardMemberships { get; set; } = new();
}
