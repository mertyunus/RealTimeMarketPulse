using RealTimeMarketPulse.Domain.Models;

namespace RealTimeMarketPulse.Domain.Interfaces;

public interface ICryptoProviderService
{
    Task<IEnumerable<CryptoTicker>> FetchLatestTickersAsync(CancellationToken cancellationToken = default);
    string GetProviderName();
}
