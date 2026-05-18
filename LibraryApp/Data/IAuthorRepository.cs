using LibraryApp.Models;

namespace LibraryApp.Data;

public interface IAuthorRepository
{
    Task<(IEnumerable<Author> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false);
    Task<Author?> GetByIdAsync(int id);
    Task<int> CreateAsync(Author author);
    Task UpdateAsync(Author author);
    Task DeleteAsync(int id);
}
