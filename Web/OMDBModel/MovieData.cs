namespace Web.OMDBModel;

public class MovieData
{
    public string Title { get; set; }
    public string Released { get; set; }
    public string Runtime { get; set; }
    public string Plot { get; set; }
    public string Awards { get; set; }
    public string Poster { get; set; }
    public List<Rating> Ratings { get; set; }
}