using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Slate.Api.Data;
using Slate.Api.DTOs;
using Slate.Api.Models;

namespace Slate.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/boards")]
public class BoardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BoardsController(AppDbContext db)
    {
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    [HttpGet]
    public async Task<ActionResult<List<BoardSummaryResponse>>> GetMyBoards()
    {
        var userId = CurrentUserId;

        var boards = await _db.BoardMembers
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Board.LastActiveAt)
            .Select(m => new BoardSummaryResponse(m.Board.Id, m.Board.Title, m.Board.LastActiveAt))
            .ToListAsync();

        return Ok(boards);
    }

    [HttpPost]
    public async Task<ActionResult<BoardSummaryResponse>> CreateBoard(CreateBoardRequest request)
    {
        var userId = CurrentUserId;

        var board = new Board
        {
            Title = request.Title.Trim(),
            OwnerId = userId,
        };
        board.Members.Add(new BoardMember { UserId = userId, Role = BoardRole.Owner });

        _db.Boards.Add(board);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBoard), new { id = board.Id },
            new BoardSummaryResponse(board.Id, board.Title, board.LastActiveAt));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BoardDetailResponse>> GetBoard(Guid id)
    {
        var userId = CurrentUserId;

        var board = await _db.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (board is null) return NotFound();

        // "Share the link" model: anyone with the link and an account joins as an
        // editor automatically the first time they open it. No invite flow yet.
        var membership = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null)
        {
            membership = new BoardMember { UserId = userId, Role = BoardRole.Editor };
            board.Members.Add(membership);
            await _db.SaveChangesAsync();
        }

        return Ok(new BoardDetailResponse(
            board.Id, board.Title, board.ElementsJson, board.CreatedAt, board.LastActiveAt,
            board.OwnerId, membership.Role));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> RenameBoard(Guid id, UpdateBoardTitleRequest request)
    {
        var userId = CurrentUserId;

        var board = await _db.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var membership = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null || membership.Role == BoardRole.Viewer) return Forbid();

        board.Title = request.Title.Trim();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<List<BoardMemberResponse>>> GetMembers(Guid id)
    {
        var userId = CurrentUserId;

        var isMember = await _db.BoardMembers.AnyAsync(m => m.BoardId == id && m.UserId == userId);
        if (!isMember) return Forbid();

        var members = await _db.BoardMembers
            .Where(m => m.BoardId == id)
            .Select(m => new BoardMemberResponse(m.UserId, m.User.DisplayName, m.User.Email, m.Role))
            .ToListAsync();

        return Ok(members);
    }

    // Only the board's owner can change another member's role between Editor and
    // Viewer. This is the "let this person read-only" control from the UI.
    [HttpPatch("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid memberId, UpdateMemberRoleRequest request)
    {
        var userId = CurrentUserId;

        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();
        if (board.OwnerId != userId) return Forbid();
        if (memberId == board.OwnerId) return BadRequest(new { message = "Can't change the owner's role." });
        if (request.Role == BoardRole.Owner) return BadRequest(new { message = "Can't assign the Owner role." });

        var membership = await _db.BoardMembers.FirstOrDefaultAsync(m => m.BoardId == id && m.UserId == memberId);
        if (membership is null) return NotFound();

        membership.Role = request.Role;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Phase 1: elements are saved via this REST endpoint on stroke-up.
    // Phase 2: this will be replaced by SignalR broadcasts (see BoardHub), with
    // this endpoint kept only as a periodic durability snapshot.
    [HttpPut("{id:guid}/elements")]
    public async Task<IActionResult> UpdateElements(Guid id, UpdateBoardElementsRequest request)
    {
        var userId = CurrentUserId;

        var board = await _db.Boards.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();

        var membership = board.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null || membership.Role == BoardRole.Viewer) return Forbid();

        board.ElementsJson = request.ElementsJson;
        board.LastActiveAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBoard(Guid id)
    {
        var userId = CurrentUserId;

        var board = await _db.Boards.FirstOrDefaultAsync(b => b.Id == id);
        if (board is null) return NotFound();
        if (board.OwnerId != userId) return Forbid();

        _db.Boards.Remove(board);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
