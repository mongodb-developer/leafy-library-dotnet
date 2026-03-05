using Leafy_Library.Models;
using Leafy_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leafy_Library.Controllers;

[ApiController]
[Route("api/books/{bookId}/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly ReviewService _reviewService;

    public ReviewsController(ReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Review>>> GetByBookId(string bookId)
    {
        var reviews = await _reviewService.GetByBookIdAsync(bookId);
        return Ok(reviews);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Review>> GetById(string id)
    {
        var review = await _reviewService.GetByIdAsync(id);
        if (review is null)
            return NotFound(new { message = "Review not found" });

        return Ok(review);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Review>> Create(string bookId, [FromBody] CreateReviewRequest request)
    {
        // Get the reviewer's name from JWT claims — the user can't fake who they are
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return Unauthorized(new { message = "Unable to determine user identity" });

        if (string.IsNullOrWhiteSpace(bookId))
            return BadRequest(new { message = "BookId is required" });

        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Review text is required" });

        if (request.Rating.HasValue && (request.Rating < 1 || request.Rating > 5))
            return BadRequest(new { message = "Rating must be between 1 and 5" });

        var review = await _reviewService.CreateAsync(
            bookId, userName, request.Text, request.Rating);

        return CreatedAtAction(nameof(GetById), new { bookId, id = review.Id }, review);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var deleted = await _reviewService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { message = "Review not found" });

        return NoContent();
    }
}

public class CreateReviewRequest
{
    public string Text { get; set; } = string.Empty;
    public int? Rating { get; set; }
}
