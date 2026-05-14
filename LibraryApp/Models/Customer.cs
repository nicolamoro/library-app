namespace LibraryApp.Models;

public class Customer
{
    public int CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly RegistrationDate { get; set; }
    public string Status { get; set; } = "active";
    public string FullName => $"{FirstName} {LastName}";
}
