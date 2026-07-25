using RealTimeMarketPulse.Domain.Models;

namespace RealTimeMarketPulse.Domain.Interfaces;

public interface ICacheService
{
    Task SetTickersAsync(IEnumerable<CryptoTicker> tickers, TimeSpan? expiration = null);
    Task<IEnumerable<CryptoTicker>> GetTickersAsync();
    Task PublishTickersAsync(string channel, IEnumerable<CryptoTicker> tickers);
    Task PublishAlertAsync(string channel, MarketAlert alert);
    void Subscribe<T>(string channel, Action<T> handler);
    bool IsConnected();
    string GetCacheMode();
}
