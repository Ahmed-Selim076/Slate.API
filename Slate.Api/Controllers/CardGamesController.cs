using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Slate.Api.Data;
using Slate.Api.DTOs;
using Slate.Api.Hubs;
using Slate.Api.Models;

namespace Slate.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/games/cards")]
public class CardGamesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<CardGameHub> _hub;

    public CardGamesController(AppDbContext db, IHubContext<CardGameHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static CardGameResponse ToResponse(CardGame g, Guid currentUserId)
    {
        var mySeat = g.Player1Id == currentUserId ? 1 : g.Player2Id == currentUserId ? 2 : 0;
        return new CardGameResponse(
            g.Id, g.GameType, g.Player1Id, g.Player1.DisplayName,
            g.Player2Id, g.Player2?.DisplayName,
            g.StateJson, g.Status, g.WinnerId, mySeat);
    }

    [HttpPost]
    public async Task<ActionResult<CardGameResponse>> CreateGame(CreateCardGameRequest request)
    {
        var userId = CurrentUserId;
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();

        var game = new CardGame { GameType = request.GameType, Player1Id = userId };
        _db.CardGames.Add(game);
        await _db.SaveChangesAsync();

        game.Player1 = user;
        return Ok(ToResponse(game, userId));
    }

    // "Share the link" model, same as boards/chess: opening the link seats you
    // as Player 2 automatically if the seat is open, or makes you a spectator.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CardGameResponse>> GetGame(Guid id)
    {
        var userId = CurrentUserId;

        var game = await _db.CardGames
            .Include(g => g.Player1)
            .Include(g => g.Player2)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();

        if (game.Player2Id is null && game.Player1Id != userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return Unauthorized();

            game.Player2Id = userId;
            game.Player2 = user;
            // Status stays WaitingForOpponent until the frontend deals the
            // first hand and pushes an InProgress state update — it needs to
            // own the deal/shuffle since that's game-specific logic.
            await _db.SaveChangesAsync();

            // Player 1's tab has been sitting on the "waiting for an
            // opponent" screen since they only fetched the game once on
            // mount — without this push it stays stuck until they refresh.
            await _hub.Clients.Group(CardGameHub.GroupName(id.ToString())).SendAsync("PlayerJoined", new
            {
                player2Id = game.Player2Id,
                player2Name = user.DisplayName,
            });
        }

        return Ok(ToResponse(game, userId));
    }

    [HttpPost("{id:guid}/state")]
    public async Task<IActionResult> UpdateState(Guid id, UpdateCardGameStateRequest request)
    {
        var userId = CurrentUserId;

        var game = await _db.CardGames.FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();
        if (game.Player1Id != userId && game.Player2Id != userId) return Forbid();

        game.StateJson = request.StateJson;
        game.Status = request.Status;
        game.WinnerId = request.WinnerId;
        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
