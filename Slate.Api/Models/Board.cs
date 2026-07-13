namespace Slate.Api.Models;

public class Board
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Untitled board";

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = default!;

    // Raw JSON array of board elements (strokes, sticky notes, text, shapes).
    // Stored as jsonb — see AppDbContext for the column type configuration.
    // Phase 1: written via REST on stroke-up. Phase 2: written via SignalR broadcasts.
    public string ElementsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public List<BoardMember> Members { get; set; } = new();
}
