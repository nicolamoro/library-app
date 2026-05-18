using LibraryApp.Models;

namespace LibraryApp.Data;

public interface IGenreRepository
{
    Task<(IEnumerable<Genre> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false);
    Task<Genre?> GetByIdAsync(int id);
    Task<int> CreateAsync(Genre genre);
    Task UpdateAsync(Genre genre);
    Task DeleteAsync(int id);
}
