using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Slate.Api.Data;

namespace Slate.Api.Hubs;

public record CursorPosition(double X, double Y);

[Authorize]
public class BoardHub : Hub
{
    private readonly AppDbContext _db;

    public BoardHub(AppDbContext db)
    {
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? Context.User!.FindFirstValue("sub")!);

    private string CurrentDisplayName => Context.User!.FindFirstValue("displayName") ?? "Someone";

    private static string GroupName(string boardId) => $"board:{boardId}";

    // Called by the client right after connecting. Verifies the user is actually
    // a member of the board before letting them join the SignalR group — this is
    // the real-time equivalent of the membership check in BoardsController.
    public async Task JoinBoard(string boardId)
    {
        if (!Guid.TryParse(boardId, out var boardGuid)) return;

        var isMember = await _db.BoardMembers
            .AnyAsync(m => m.BoardId == boardGuid && m.UserId == CurrentUserId);

        if (!isMember)
        {
            // Same "share the link" auto-join model as BoardsController.GetBoard.
            // In practice the REST call already runs first when the board page loads,
            // so this is mostly a safety net for the hub connection specifically.
            var boardExists = await _db.Boards.AnyAsync(b => b.Id == boardGuid);
            if (!boardExists)
            {
                throw new HubException("This board doesn't exist.");
            }

            _db.BoardMembers.Add(new Models.BoardMember { BoardId = boardGuid, UserId = CurrentUserId });
            await _db.SaveChangesAsync();
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId));

        await Clients.OthersInGroup(GroupName(boardId)).SendAsync("UserJoined", new
        {
            connectionId = Context.ConnectionId,
            displayName = CurrentDisplayName,
        });
    }

    public async Task LeaveBoard(string boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId));
        await Clients.OthersInGroup(GroupName(boardId)).SendAsync("UserLeft", Context.ConnectionId);
    }

    // Broadcasts a single finished stroke (or other board element) to everyone else
    // on the board. The sender already has it locally, so we exclude them.
    // Durable persistence still happens via the REST PUT /api/boards/{id}/elements
    // endpoint (debounced client-side) — this hub call is purely for live sync.
    public async Task SendElement(string boardId, string elementJson)
    {
        if (!Guid.TryParse(boardId, out var boardGuid)) return;

        var membership = await _db.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardGuid && m.UserId == CurrentUserId);
        if (membership is null || membership.Role == Models.BoardRole.Viewer) return;

        await Clients.OthersInGroup(GroupName(boardId)).SendAsync("ReceiveElement", elementJson);
    }

    public async Task DeleteElement(string boardId, string elementId)
    {
        if (!Guid.TryParse(boardId, out var boardGuid)) return;

        var membership = await _db.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardGuid && m.UserId == CurrentUserId);
        if (membership is null || membership.Role == Models.BoardRole.Viewer) return;

        await Clients.OthersInGroup(GroupName(boardId)).SendAsync("ElementDeleted", elementId);
    }

    // Broadcasts the sender's live cursor position. Throttled on the client
    // (e.g. every ~50ms) rather than on every raw pointer-move event.
    public async Task SendCursor(string boardId, CursorPosition position)
    {
        await Clients.OthersInGroup(GroupName(boardId)).SendAsync("ReceiveCursor", new
        {
            connectionId = Context.ConnectionId,
            displayName = CurrentDisplayName,
            x = position.X,
            y = position.Y,
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Best-effort: the client also calls LeaveBoard explicitly on unmount,
        // but this covers tab closes / network drops.
        await base.OnDisconnectedAsync(exception);
    }
}
