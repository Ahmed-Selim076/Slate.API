using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Slate.Api.Data;

namespace Slate.Api.Hubs;

[Authorize]
public class CardGameHub : Hub
{
    private readonly AppDbContext _db;

    public CardGameHub(AppDbContext db)
    {
        _db = db;
    }

    public static string GroupName(string gameId) => $"cardgame:{gameId}";

    // Fixes a real race: a client can finish the REST GET (which seats them
    // as Player 2 and fires "PlayerJoined") before its SignalR connection has
    // actually joined this group. If Player 1 deals and broadcasts in that
    // window, the late joiner never sees it and sits on "Dealing the hand…"
    // forever. So the moment a connection joins the group, we hand it the
    // current authoritative state directly — no reliance on timing.
    public async Task JoinGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));

        if (Guid.TryParse(gameId, out var id))
        {
            var game = await _db.CardGames.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            if (game is not null && game.StateJson is not null)
            {
                await Clients.Caller.SendAsync("ReceiveState", new
                {
                    stateJson = game.StateJson,
                    status = game.Status,
                    winnerId = game.WinnerId,
                });
            }
        }
    }

    public async Task LeaveGame(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));
    }

    // State has already been persisted via POST /api/games/cards/{id}/state —
    // this just pushes it live to the other player.
    public async Task SendState(string gameId, string stateJson, string status, string? winnerId)
    {
        await Clients.OthersInGroup(GroupName(gameId)).SendAsync("ReceiveState", new { stateJson, status, winnerId });
    }
}
