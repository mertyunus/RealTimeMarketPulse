namespace RealTimeMarketPulse.Domain.Models;

public class SystemStatus
{
    public bool IsRedisConnected { get; set; }
    public string CacheMode { get; set; } = "Unknown";
    public string QuartzStatus { get; set; } = "Running";
    public int RefreshIntervalSeconds { get; set; }
    public int TotalFetchCount { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string ActiveProvider { get; set; } = "CoinCap Live API";
}
