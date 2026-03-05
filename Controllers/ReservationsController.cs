using Leafy_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leafy_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly ReservationService _reservationService;

    public ReservationsController(ReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    /// <summary>
    /// Get current user's reservations.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserReservations()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Unable to determine user identity" });

        var reservations = await _reservationService.GetUserReservationsAsync(userId);
        return Ok(reservations);
    }

    /// <summary>
    /// Create a reservation for a book.
    /// </summary>
    [HttpPost("{bookId}")]
    public async Task<IActionResult> CreateReservation(string bookId)
    {
        var userId = User.FindFirst("sub")?.Value;
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
            return Unauthorized(new { message = "Unable to determine user identity" });

        try
        {
            var reservation = await _reservationService.CreateReservationAsync(bookId, userId, userName);
            return StatusCode(201, new { message = "Reservation created", insertedId = reservation.Id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a reservation for a book.
    /// </summary>
    [HttpDelete("{bookId}")]
    public async Task<IActionResult> CancelReservation(string bookId)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Unable to determine user identity" });

        var deleted = await _reservationService.CancelReservationAsync(bookId, userId);
        if (!deleted)
            return NotFound(new { message = "Reservation not found" });

        return Ok(new { message = "Reservation cancelled" });
    }

    /// <summary>
    /// Get a paginated list of all reservations (admin only).
    /// </summary>
    [HttpGet("page")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> GetReservationsPage([FromQuery] int limit = 10, [FromQuery] int skip = 0)
    {
        var result = await _reservationService.GetReservationsPageAsync(limit, skip);
        return Ok(result);
    }
}
