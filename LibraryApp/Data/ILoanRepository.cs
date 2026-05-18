using LibraryApp.Models;

namespace LibraryApp.Data;

public interface ILoanRepository
{
    Task<IEnumerable<LoanDetail>> GetAllAsync();
    Task<(IEnumerable<LoanDetail> Items, int Total)> GetPagedAsync(int page, int pageSize, string filter, string? sortBy = null, bool sortDescending = false);
    Task<(IEnumerable<LoanDetail> Items, int Total)> GetByUserIdPagedAsync(int userId, int page, int pageSize, string filter = "all", string? sortBy = null, bool sortDescending = false);
    Task<IEnumerable<LoanDetail>> GetOverdueAsync();
    Task<(IEnumerable<LoanDetail> Items, int Total)> GetOverduePagedAsync(int page, int pageSize, string? sortBy = null, bool sortDescending = false);
    Task<IEnumerable<LoanDetail>> GetActiveAsync();
    Task BorrowAsync(int userId, int bookId, int loanDays = 30);
    Task<LoanDetail?> ReturnAsync(int loanId);
}
