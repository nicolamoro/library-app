namespace LibraryApp.Models;

public class Author
{
    public int AuthorId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string? Nationality { get; set; }
    public string? Biography { get; set; }
    public string FullName => $"{FirstName} {LastName}";
}
