namespace LibraryApp.Models;

public class LoanDetail
{
    public int LoanId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public DateOnly LoanDate { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? ReturnDate { get; set; }
    public string Status { get; set; } = "active";
    public decimal DailyFineRate { get; set; }
    public decimal? FineAmount { get; set; }
    public bool FinePaid { get; set; }

    public int DaysOverdue
    {
        get
        {
            if (ReturnDate != null || DueDate >= DateOnly.FromDateTime(DateTime.Today)) return 0;
            return DateOnly.FromDateTime(DateTime.Today).DayNumber - DueDate.DayNumber;
        }
    }

    public decimal EstimatedFine => DaysOverdue > 0 ? DaysOverdue * DailyFineRate : 0;
}
