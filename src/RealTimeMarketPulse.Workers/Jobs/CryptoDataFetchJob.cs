using Microsoft.Extensions.Logging;
using Quartz;
using RealTimeMarketPulse.Domain.Interfaces;
using RealTimeMarketPulse.Domain.Models;

namespace RealTimeMarketPulse.Workers.Jobs;

[DisallowConcurrentExecution]
public class CryptoDataFetchJob : IJob
{
    private readonly ICryptoProviderService _cryptoProvider;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CryptoDataFetchJob> _logger;
    private static int _executionCount;

    public CryptoDataFetchJob(ICryptoProviderService cryptoProvider, ICacheService cacheService, ILogger<CryptoDataFetchJob> logger)
    {
        _cryptoProvider = cryptoProvider;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        int currentCount = Interlocked.Increment(ref _executionCount);
        _logger.LogInformation("Quartz Job [{JobName}] started execution #{Count} at {Time}", context.JobDetail.Key.Name, currentCount, DateTime.UtcNow.ToLongTimeString());

        try
        {
            var tickers = (await _cryptoProvider.FetchLatestTickersAsync(context.CancellationToken)).ToList();
            if (tickers.Count > 0)
            {
                await _cacheService.SetTickersAsync(tickers, TimeSpan.FromMinutes(5));
                await _cacheService.PublishTickersAsync("pulse:channel:tickers", tickers);
                
                _logger.LogInformation("Fetched and published {Count} tickers via {Provider} (Cache Mode: {CacheMode})", 
                    tickers.Count, _cryptoProvider.GetProviderName(), _cacheService.GetCacheMode());

                var topGainer = tickers.OrderByDescending(t => t.ChangePercent24Hr).FirstOrDefault();
                if (topGainer != null && Math.Abs(topGainer.ChangePercent24Hr) >= 4.0m && currentCount % 3 == 0)
                {
                    var alert = new MarketAlert
                    {
                        Symbol = topGainer.Symbol,
                        Title = $"Piyasa Hareketliliği: {topGainer.Symbol}",
                        Message = $"{topGainer.Name} ({topGainer.Symbol}) son 24 saatte %{topGainer.ChangePercent24Hr:F2} oranında {(topGainer.ChangePercent24Hr > 0 ? "yükseldi" : "düşüş yaşadı")}! Anlık Fiyat: ${topGainer.PriceUsd:N2}",
                        Severity = topGainer.ChangePercent24Hr > 0 ? "success" : "danger",
                        CurrentPrice = topGainer.PriceUsd,
                        ChangeRate = topGainer.ChangePercent24Hr
                    };
                    await _cacheService.PublishAlertAsync("pulse:channel:alerts", alert);
                    _logger.LogInformation("Published market alert for {Symbol} (%{Change})", topGainer.Symbol, topGainer.ChangePercent24Hr);
                }

                var status = new SystemStatus
                {
                    IsRedisConnected = _cacheService.IsConnected(),
                    CacheMode = _cacheService.GetCacheMode(),
                    QuartzStatus = "Active (Polling)",
                    RefreshIntervalSeconds = 3,
                    TotalFetchCount = currentCount,
                    LastUpdated = DateTime.UtcNow,
                    ActiveProvider = _cryptoProvider.GetProviderName()
                };
                await _cacheService.PublishAlertAsync("pulse:channel:status", new MarketAlert
                {
                    Title = "SYSTEM_STATUS",
                    Message = System.Text.Json.JsonSerializer.Serialize(status, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }),
                    Severity = "info"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during Quartz Job [{JobName}] execution #{Count}", context.JobDetail.Key.Name, currentCount);
        }
    }

    public static int GetTotalExecutionCount() => _executionCount;
}
