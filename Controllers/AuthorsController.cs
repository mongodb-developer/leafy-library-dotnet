using Leafy_Library.Models;
using Leafy_Library.Services;
using Microsoft.AspNetCore.Mvc;

namespace Leafy_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthorsController : ControllerBase
{
    private readonly AuthorService _authorService;

    public AuthorsController(AuthorService authorService)
    {
        _authorService = authorService;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var authors = await _authorService.GetAllAsync(page, pageSize);
        var total = await _authorService.GetCountAsync();

        return Ok(new { authors, total, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Author>> GetById(string id)
    {
        var author = await _authorService.GetByIdAsync(id);
        if (author is null)
            return NotFound(new { message = "Author not found" });

        return Ok(author);
    }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<Author>> GetByName(string name)
    {
        var author = await _authorService.GetByNameAsync(name);
        if (author is null)
            return NotFound(new { message = "Author not found" });

        return Ok(author);
    }

    [HttpGet("search")]
    public async Task<ActionResult<object>> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Search query is required" });

        var authors = await _authorService.SearchAsync(q, page, pageSize);
        return Ok(new { authors, page, pageSize });
    }
}
