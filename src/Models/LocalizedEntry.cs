// Models/LocalizedEntry.cs
public class LocalizedEntry
{
    public string LanguageId { get; set; } = "en_us";
    public string Description { get; set; }
    public string Title { get; set; }
    public string ReleaseDate { get; set; } = "";
    public int ItemPrice { get; set; }
}