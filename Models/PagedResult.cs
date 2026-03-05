namespace Leafy_Library.Models;

public class PagedResult<T>
{
    public List<T> Data { get; set; } = new();
    public long TotalCount { get; set; }
}
