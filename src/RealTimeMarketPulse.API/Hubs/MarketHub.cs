using Microsoft.AspNetCore.SignalR;
using RealTimeMarketPulse.Domain.Interfaces;
using RealTimeMarketPulse.Domain.Models;

namespace RealTimeMarketPulse.API.Hubs;

public class MarketHub : Hub
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<MarketHub> _logger;

    public MarketHub(ICacheService cacheService, ILogger<MarketHub> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR Client connected: {ConnectionId}", Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-market");
        
        var tickers = await _cacheService.GetTickersAsync();
        if (tickers.Any())
        {
            await Clients.Caller.SendAsync("ReceiveMarketTickers", tickers);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToSymbol(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, symbol.ToUpperInvariant());
            _logger.LogInformation("Client {ConnectionId} subscribed to symbol {Symbol}", Context.ConnectionId, symbol);
        }
    }

    public async Task UnsubscribeFromSymbol(string symbol)
    {
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, symbol.ToUpperInvariant());
        }
    }
}
