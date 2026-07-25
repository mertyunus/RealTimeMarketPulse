using Microsoft.AspNetCore.SignalR;
using RealTimeMarketPulse.API.Hubs;
using RealTimeMarketPulse.Domain.Interfaces;
using RealTimeMarketPulse.Domain.Models;

namespace RealTimeMarketPulse.API.Services;

public class SignalRBroadcastHostedService : BackgroundService
{
    private readonly ICacheService _cacheService;
    private readonly IHubContext<MarketHub> _hubContext;
    private readonly ILogger<SignalRBroadcastHostedService> _logger;

    public SignalRBroadcastHostedService(ICacheService cacheService, IHubContext<MarketHub> hubContext, ILogger<SignalRBroadcastHostedService> logger)
    {
        _cacheService = cacheService;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SignalR Broadcast Bridge Service is starting. Subscribing to Redis/Local Pub/Sub channels...");

        _cacheService.Subscribe<List<CryptoTicker>>("pulse:channel:tickers", async tickers =>
        {
            try
            {
                await _hubContext.Clients.Group("all-market").SendAsync("ReceiveMarketTickers", tickers, stoppingToken);
                foreach (var t in tickers)
                {
                    await _hubContext.Clients.Group(t.Symbol).SendAsync("ReceiveSymbolUpdate", t, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting tickers over SignalR");
            }
        });

        _cacheService.Subscribe<MarketAlert>("pulse:channel:alerts", async alert =>
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ReceiveMarketAlert", alert, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting market alert over SignalR");
            }
        });

        _cacheService.Subscribe<MarketAlert>("pulse:channel:status", async statusAlert =>
        {
            try
            {
                if (statusAlert.Title == "SYSTEM_STATUS")
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveSystemStatus", statusAlert.Message, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting system status over SignalR");
            }
        });

        _logger.LogInformation("SignalR Broadcast Bridge Service active and subscribed to channels.");
        return Task.CompletedTask;
    }
}
