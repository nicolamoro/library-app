using LibraryApp.Models;

namespace LibraryApp.Data;

public interface IPublisherRepository
{
    Task<(IEnumerable<Publisher> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false);
    Task<Publisher?> GetByIdAsync(int id);
    Task<int> CreateAsync(Publisher publisher);
    Task UpdateAsync(Publisher publisher);
    Task DeleteAsync(int id);
}
