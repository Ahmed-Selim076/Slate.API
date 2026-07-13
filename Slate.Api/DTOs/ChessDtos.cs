using Slate.Api.Models;

namespace Slate.Api.DTOs;

public record ChessGameResponse(
    Guid Id,
    Guid WhitePlayerId,
    string WhitePlayerName,
    Guid? BlackPlayerId,
    string? BlackPlayerName,
    string Fen,
    string MoveHistory,
    ChessStatus Status,
    ChessWinner Winner,
    string MyColor // "White" | "Black" | "Spectator"
);

public record MakeMoveRequest(
    string Fen,
    string San, // the move in Standard Algebraic Notation, appended to history
    ChessStatus Status,
    ChessWinner Winner
);
