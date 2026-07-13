namespace Slate.Api.Models;

public enum CardGameType
{
    Dominoes,
    Koshina,
    Conquian,
    Trix,
}

public enum CardGameStatus
{
    WaitingForOpponent,
    InProgress,
    Finished,
}

public class CardGame
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public CardGameType GameType { get; set; }

    public Guid Player1Id { get; set; }
    public User Player1 { get; set; } = default!;

    public Guid? Player2Id { get; set; }
    public User? Player2 { get; set; }

    // Full game state (hands, deck, discard pile, board, whose turn, etc.),
    // shaped differently per GameType and entirely owned/interpreted by the
    // frontend. The server just persists and relays it.
    public string StateJson { get; set; } = "{}";

    public CardGameStatus Status { get; set; } = CardGameStatus.WaitingForOpponent;
    public Guid? WinnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
