using LibraryApp.Models;

namespace LibraryApp.Data;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task UpdateLastLoginAsync(int userId);
    Task<IEnumerable<User>> GetAllAsync();
    Task<(IEnumerable<User> Items, int Total)> GetPagedAsync(int page, int pageSize, string? search, string? sortBy = null, bool sortDescending = false);
    Task<User?> GetByIdAsync(int id);
    Task<int> CreateAsync(User u, string plainPassword);
    Task UpdateAsync(User u, string? newPassword = null);
    Task DeleteAsync(int id);
}
