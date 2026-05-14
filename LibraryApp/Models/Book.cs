namespace LibraryApp.Models;

public class Book
{
    public int BookId { get; set; }
    public string? Isbn { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? PublisherId { get; set; }
    public string? PublisherName { get; set; }
    public int? GenreId { get; set; }
    public string? GenreName { get; set; }
    public short? PublicationYear { get; set; }
    public string? Language { get; set; }
    public short? PageCount { get; set; }
    public short TotalCopies { get; set; } = 1;
    public short AvailableCopies { get; set; } = 1;
    public string? AuthorsDisplay { get; set; }
    public List<int> SelectedAuthorIds { get; set; } = new();
}
