namespace Slate.Api.Models;

public enum BoardRole
{
    Owner,
    Editor,
    Viewer,
}

public class BoardMember
{
    public Guid BoardId { get; set; }
    public Board Board { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public BoardRole Role { get; set; } = BoardRole.Editor;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
