using Slate.Api.Models;

namespace Slate.Api.DTOs;

public record CreateCardGameRequest(CardGameType GameType);

public record CardGameResponse(
    Guid Id,
    CardGameType GameType,
    Guid Player1Id,
    string Player1Name,
    Guid? Player2Id,
    string? Player2Name,
    string StateJson,
    CardGameStatus Status,
    Guid? WinnerId,
    int MySeat // 1, 2, or 0 for spectator
);

public record UpdateCardGameStateRequest(
    string StateJson,
    CardGameStatus Status,
    Guid? WinnerId
);
