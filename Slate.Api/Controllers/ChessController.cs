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
[Route("api/games/chess")]
public class ChessController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChessHub> _hub;

    public ChessController(AppDbContext db, IHubContext<ChessHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static ChessGameResponse ToResponse(ChessGame g, Guid currentUserId)
    {
        var myColor = g.WhitePlayerId == currentUserId ? "White"
            : g.BlackPlayerId == currentUserId ? "Black"
            : "Spectator";

        return new ChessGameResponse(
            g.Id, g.WhitePlayerId, g.WhitePlayer.DisplayName,
            g.BlackPlayerId, g.BlackPlayer?.DisplayName,
            g.Fen, g.MoveHistory, g.Status, g.Winner, myColor);
    }

    [HttpPost]
    public async Task<ActionResult<ChessGameResponse>> CreateGame()
    {
        var userId = CurrentUserId;
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Unauthorized();

        var game = new ChessGame { WhitePlayerId = userId };
        _db.ChessGames.Add(game);
        await _db.SaveChangesAsync();

        game.WhitePlayer = user;
        return Ok(ToResponse(game, userId));
    }

    // "Share the link" model, same as boards: opening the link joins you as
    // Black automatically if the seat is open, or as a spectator otherwise.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChessGameResponse>> GetGame(Guid id)
    {
        var userId = CurrentUserId;

        var game = await _db.ChessGames
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (game is null) return NotFound();

        if (game.BlackPlayerId is null && game.WhitePlayerId != userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return Unauthorized();

            game.BlackPlayerId = userId;
            game.BlackPlayer = user;
            game.Status = ChessStatus.InProgress;
            await _db.SaveChangesAsync();

            // The white player's tab has been sitting on "Waiting for an
            // opponent" — it only ever fetched the game once on mount, so
            // without this push it would stay stuck showing Waiting/no moves
            // until they manually refreshed the page.
            await _hub.Clients.Group(ChessHub.GroupName(id.ToString())).SendAsync("PlayerJoined", new
            {
                blackPlayerId = game.BlackPlayerId,
                blackPlayerName = user.DisplayName,
                status = game.Status.ToString(),
            });
        }

        return Ok(ToResponse(game, userId));
    }

    [HttpPost("{id:guid}/move")]
    public async Task<IActionResult> MakeMove(Guid id, MakeMoveRequest request)
    {
        var userId = CurrentUserId;

        var game = await _db.ChessGames.FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();

        var isWhiteTurn = game.Fen.Contains(" w ");
        var movingPlayerId = isWhiteTurn ? game.WhitePlayerId : game.BlackPlayerId;
        if (movingPlayerId != userId) return Forbid();

        if (game.Status is ChessStatus.Checkmate or ChessStatus.Stalemate or ChessStatus.Draw or ChessStatus.Resigned)
            return BadRequest(new { message = "This game has already ended." });

        game.Fen = request.Fen;
        game.MoveHistory = string.IsNullOrEmpty(game.MoveHistory) ? request.San : $"{game.MoveHistory} {request.San}";
        game.Status = request.Status;
        game.Winner = request.Winner;
        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/resign")]
    public async Task<IActionResult> Resign(Guid id)
    {
        var userId = CurrentUserId;

        var game = await _db.ChessGames.FirstOrDefaultAsync(g => g.Id == id);
        if (game is null) return NotFound();
        if (game.WhitePlayerId != userId && game.BlackPlayerId != userId) return Forbid();

        game.Status = ChessStatus.Resigned;
        game.Winner = game.WhitePlayerId == userId ? ChessWinner.Black : ChessWinner.White;
        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
