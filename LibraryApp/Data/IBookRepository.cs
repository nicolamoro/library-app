using LibraryApp.Models;

namespace LibraryApp.Data;

public interface IBookRepository
{
    Task<IEnumerable<Book>> GetAllAsync();
    Task<(IEnumerable<Book> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false);
    Task<Book?> GetByIdAsync(int id);
    Task<IEnumerable<int>> GetAuthorIdsAsync(int bookId);
    Task<int> CreateAsync(Book book);
    Task UpdateAsync(Book book);
    Task DeleteAsync(int id);
    Task<IEnumerable<Author>> GetAllAuthorsAsync();
    Task<IEnumerable<Publisher>> GetAllPublishersAsync();
    Task<IEnumerable<Genre>> GetAllGenresAsync();
}
