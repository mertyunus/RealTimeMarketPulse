namespace RealTimeMarketPulse.Domain.Models;

public class MarketAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Symbol { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "info"; // info, warning, danger, success
    public decimal CurrentPrice { get; set; }
    public decimal ChangeRate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
