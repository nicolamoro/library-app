using LibraryApp.Models;

namespace LibraryApp.Tests.Unit;

[Trait("Category", "Unit")]
public class LoanDetailTests
{
    private static LoanDetail Make(DateOnly dueDate, DateOnly? returnDate = null, decimal dailyFineRate = 0.50m) =>
        new()
        {
            LoanId = 1,
            UserId = 1,
            BookId = 1,
            LoanDate = dueDate.AddDays(-30),
            DueDate = dueDate,
            ReturnDate = returnDate,
            DailyFineRate = dailyFineRate,
            Status = returnDate.HasValue ? "returned" : "active",
        };

    // --- DaysOverdue ---

    [Fact]
    public void DaysOverdue_ReturnDateSet_ReturnsZero()
    {
        var loan = Make(DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(0, loan.DaysOverdue);
    }

    [Fact]
    public void DaysOverdue_DueDateIsToday_ReturnsZero()
    {
        var loan = Make(DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(0, loan.DaysOverdue);
    }

    [Fact]
    public void DaysOverdue_DueDateInFuture_ReturnsZero()
    {
        var loan = Make(DateOnly.FromDateTime(DateTime.Today.AddDays(7)));
        Assert.Equal(0, loan.DaysOverdue);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(30)]
    public void DaysOverdue_DueDateInPast_ReturnsCorrectDays(int daysLate)
    {
        var loan = Make(DateOnly.FromDateTime(DateTime.Today.AddDays(-daysLate)));
        Assert.Equal(daysLate, loan.DaysOverdue);
    }

    [Fact]
    public void DaysOverdue_ReturnedLate_IgnoresLateReturn()
    {
        // Returned after the due date: DaysOverdue is based on today vs DueDate,
        // but ReturnDate != null means the loan is closed → 0.
        var loan = Make(
            dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            returnDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-5)));
        Assert.Equal(0, loan.DaysOverdue);
    }

    // --- EstimatedFine ---

    [Fact]
    public void EstimatedFine_NotOverdue_ReturnsZero()
    {
        var loan = Make(DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        Assert.Equal(0m, loan.EstimatedFine);
    }

    [Fact]
    public void EstimatedFine_Overdue_ReturnsDaysTimesDailyRate()
    {
        const int daysLate = 5;
        const decimal dailyRate = 0.50m;
        var loan = Make(DateOnly.FromDateTime(DateTime.Today.AddDays(-daysLate)), dailyFineRate: dailyRate);
        Assert.Equal(daysLate * dailyRate, loan.EstimatedFine);
    }

    [Fact]
    public void EstimatedFine_AlreadyReturned_ReturnsZero()
    {
        var loan = Make(
            dueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-10)),
            returnDate: DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(0m, loan.EstimatedFine);
    }
}
