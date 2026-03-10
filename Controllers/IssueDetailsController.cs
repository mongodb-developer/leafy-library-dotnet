using Leafy_Library.Models;
using Leafy_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leafy_Library.Controllers;

public class BorrowRequest
{
    public string BookId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IssueDetailsController : ControllerBase
{
    private readonly IssueDetailService _issueDetailService;

    public IssueDetailsController(IssueDetailService issueDetailService)
    {
        _issueDetailService = issueDetailService;
    }

    // ──────────────────────────────────────────────
    //  Queries
    // ──────────────────────────────────────────────

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<IssueDetail>>> GetByUserId(string userId)
    {
        var issues = await _issueDetailService.GetByUserIdAsync(userId);
        return Ok(issues);
    }

    [HttpGet("user/{userId}/active")]
    public async Task<ActionResult<List<IssueDetail>>> GetActiveBorrows(string userId)
    {
        var issues = await _issueDetailService.GetActiveBorrowsAsync(userId);
        return Ok(issues);
    }

    [HttpGet("book/{bookId}")]
    public async Task<ActionResult<List<IssueDetail>>> GetByBookId(string bookId)
    {
        var issues = await _issueDetailService.GetByBookIdAsync(bookId);
        return Ok(issues);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IssueDetail>> GetById(string id)
    {
        var issue = await _issueDetailService.GetByIdAsync(id);
        if (issue is null)
            return NotFound(new { message = "Issue record not found" });

        return Ok(issue);
    }

    // ──────────────────────────────────────────────
    //  User actions
    // ──────────────────────────────────────────────

    [HttpPost("borrow")]
    public async Task<ActionResult<IssueDetail>> BorrowBook([FromBody] BorrowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BookId))
            return BadRequest(new { message = "BookId is required" });

        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new { message = "UserId is required" });

        var issue = await _issueDetailService.BorrowBookAsync(
            request.BookId, request.UserId, request.UserName);

        if (issue is null)
            return Conflict(new { message = "Book is not available or already borrowed by this user" });

        return CreatedAtAction(nameof(GetById), new { id = issue.Id }, issue);
    }

    [HttpPost("return/{issueId}")]
    public async Task<ActionResult> ReturnBook(string issueId)
    {
        var returned = await _issueDetailService.ReturnBookAsync(issueId);
        if (!returned)
            return NotFound(new { message = "Active borrow record not found" });

        return Ok(new { message = "Book returned successfully" });
    }

    // ──────────────────────────────────────────────
    //  Admin endpoints
    // ──────────────────────────────────────────────

    /// <summary>
    /// Get a paginated list of all borrowed books (admin only).
    /// </summary>
    [HttpGet("borrow/page")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> GetBorrowedBooksPage([FromQuery] int limit = 10, [FromQuery] int skip = 0)
    {
        var result = await _issueDetailService.GetBorrowedBooksPageAsync(limit, skip);
        return Ok(result);
    }

    /// <summary>
    /// Admin: Lend a book to a user (converts reservation to borrow).
    /// </summary>
    [HttpPost("borrow/{bookId}/{userId}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> AdminBorrowBook(string bookId, string userId, [FromServices] UserService userService)
    {
        var user = await userService.GetUserByIdAsync(userId);
        if (user is null)
            return NotFound(new { message = "User not found" });

        var issue = await _issueDetailService.AdminBorrowBookAsync(bookId, userId, user.Name);
        if (issue is null)
            return Conflict(new { message = "Book not found" });

        return Ok(issue);
    }

    /// <summary>
    /// Admin: Return a borrowed book on behalf of a user.
    /// </summary>
    [HttpPost("borrow/{bookId}/{userId}/return")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> AdminReturnBook(string bookId, string userId)
    {
        var returned = await _issueDetailService.AdminReturnBookAsync(bookId, userId);
        if (!returned)
            return NotFound(new { message = "Active borrow record not found" });

        return Ok(new { message = "Book returned successfully" });
    }
}
