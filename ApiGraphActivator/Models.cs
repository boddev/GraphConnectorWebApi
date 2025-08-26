namespace ApiGraphActivator;

public class Company
{
    public int Cik { get; set; }
    public string Ticker { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime? LastCrawledDate { get; set; }
}

public class CrawlRequest
{
    public List<Company> Companies { get; set; } = new();
    public string ConnectionId { get; set; } = "";
}
