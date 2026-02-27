using Leafy_Library.Models;
using Leafy_Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leafy_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly BookService _bookService;

    public BooksController(BookService bookService)
    {
        _bookService = bookService;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var books = await _bookService.GetAllAsync(page, pageSize);
        var total = await _bookService.GetCountAsync();

        return Ok(new { books, total, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Book>> GetById(string id)
    {
        var book = await _bookService.GetByIdAsync(id);
        if (book is null)
            return NotFound(new { message = "Book not found" });

        return Ok(book);
    }

    [HttpGet("search")]
    public async Task<ActionResult<object>> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Search query is required" });

        var books = await _bookService.SearchAsync(q, page, pageSize);
        var total = await _bookService.SearchCountAsync(q);

        return Ok(new { books, total, page, pageSize });
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Book>> Create([FromBody] Book book)
    {
        await _bookService.CreateAsync(book);
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] Book book)
    {
        book.Id = id;
        var updated = await _bookService.UpdateAsync(id, book);
        if (!updated)
            return NotFound(new { message = "Book not found" });

        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var deleted = await _bookService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { message = "Book not found" });

        return NoContent();
    }
}
