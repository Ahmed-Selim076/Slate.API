using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Slate.Api.Hubs;

[Authorize]
public class ChessHub : Hub
{
    public static string GroupName(string gameId) => $"chess:{gameId}";

    public async Task JoinGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    public async Task LeaveGame(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    // The move has already been validated client-side (chess.js) and persisted
    // via POST /api/games/chess/{id}/move — this just pushes it live to the
    // opponent so they don't have to poll or refresh.
    public async Task SendMove(string gameId, string fen, string san, string status, string winner)
    {
        await Clients.OthersInGroup(GroupName(gameId)).SendAsync("ReceiveMove", new { fen, san, status, winner });
    }

    public async Task SendResign(string gameId, string winner)
    {
        await Clients.OthersInGroup(GroupName(gameId)).SendAsync("ReceiveResign", new { winner });
    }
}
