using Leafy_Library.Models;
using Leafy_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leafy_Library.Controllers;

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
}

public class BorrowRequest
{
    public string BookId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
