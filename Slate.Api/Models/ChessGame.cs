namespace Slate.Api.Models;

public enum ChessStatus
{
    WaitingForOpponent,
    InProgress,
    Checkmate,
    Stalemate,
    Draw,
    Resigned,
}

public enum ChessWinner
{
    None,
    White,
    Black,
}

public class ChessGame
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WhitePlayerId { get; set; }
    public User WhitePlayer { get; set; } = default!;

    public Guid? BlackPlayerId { get; set; }
    public User? BlackPlayer { get; set; }

    // Standard FEN string for the current position. Starts at the standard
    // opening position and is updated after every validated move.
    public string Fen { get; set; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    // Moves in SAN notation, space-separated, for replay/history display.
    public string MoveHistory { get; set; } = "";

    public ChessStatus Status { get; set; } = ChessStatus.WaitingForOpponent;
    public ChessWinner Winner { get; set; } = ChessWinner.None;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
