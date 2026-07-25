namespace RealTimeMarketPulse.Domain.Models;

public class CryptoTicker
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal PriceUsd { get; set; }
    public decimal ChangePercent24Hr { get; set; }
    public decimal High24Hr { get; set; }
    public decimal Low24Hr { get; set; }
    public decimal Volume24Hr { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
