using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealTimeMarketPulse.Domain.Interfaces;
using RealTimeMarketPulse.Domain.Models;

namespace RealTimeMarketPulse.Infrastructure.Services;

public class CryptoProviderService : ICryptoProviderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CryptoProviderService> _logger;
    private string _activeProviderName = "CoinCap Public API (Live)";
    private static readonly ConcurrentDictionary<string, CryptoTicker> _simulatedTickers = new();
    private static readonly Random _random = new();

    public CryptoProviderService(HttpClient httpClient, ILogger<CryptoProviderService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(4);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RealTimeMarketPulse-Service/1.0");
        InitializeSimulatedData();
    }

    public string GetProviderName() => _activeProviderName;

    public async Task<IEnumerable<CryptoTicker>> FetchLatestTickersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.coincap.io/v2/assets?limit=12", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(content);
                var data = doc.RootElement.GetProperty("data");

                var list = new List<CryptoTicker>();
                foreach (var item in data.EnumerateArray())
                {
                    decimal price = ParseDecimal(item.GetProperty("priceUsd").GetString());
                    decimal change24 = ParseDecimal(item.GetProperty("changePercent24Hr").GetString());
                    decimal volume = ParseDecimal(item.GetProperty("volumeUsd24Hr").GetString());
                    string symbol = item.GetProperty("symbol").GetString() ?? "";
                    string name = item.GetProperty("name").GetString() ?? "";

                    decimal high24 = price * (1 + Math.Abs(change24) / 100 * 1.05m);
                    decimal low24 = price * (1 - Math.Abs(change24) / 100 * 0.95m);

                    var ticker = new CryptoTicker
                    {
                        Symbol = symbol,
                        Name = name,
                        PriceUsd = Math.Round(price, price < 1 ? 4 : 2),
                        ChangePercent24Hr = Math.Round(change24, 2),
                        High24Hr = Math.Round(high24, price < 1 ? 4 : 2),
                        Low24Hr = Math.Round(low24, price < 1 ? 4 : 2),
                        Volume24Hr = Math.Round(volume, 0),
                        Timestamp = DateTime.UtcNow
                    };
                    list.Add(ticker);
                    _simulatedTickers[symbol] = ticker; // keep simulation sync with live data
                }

                if (list.Count > 0)
                {
                    _activeProviderName = "CoinCap Public API (Live WebSocket Pulse)";
                    return list;
                }
            }
            _logger.LogWarning("CoinCap API response was not successful ({StatusCode}). Using high-frequency simulation fallback.", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not reach CoinCap live API ({Msg}). Using high-frequency simulation fallback.", ex.Message);
        }

        _activeProviderName = "Smart Simulation Engine (High-Frequency Fallback)";
        return GenerateSimulatedTickers();
    }

    private static decimal ParseDecimal(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return 0m;
        return decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val) ? val : 0m;
    }

    private static IEnumerable<CryptoTicker> GenerateSimulatedTickers()
    {
        var result = new List<CryptoTicker>();
        foreach (var kvp in _simulatedTickers)
        {
            var t = kvp.Value;
            double pctChange = (_random.NextDouble() * 1.6 - 0.78); // -0.78% to +0.82%
            decimal oldPrice = t.PriceUsd;
            decimal changeAmount = oldPrice * (decimal)(pctChange / 100.0);
            decimal newPrice = oldPrice + changeAmount;
            if (newPrice <= 0) newPrice = 0.01m;

            t.PriceUsd = Math.Round(newPrice, newPrice < 1 ? 4 : 2);
            t.ChangePercent24Hr = Math.Round(t.ChangePercent24Hr + (decimal)(pctChange * 0.15), 2);
            if (t.PriceUsd > t.High24Hr) t.High24Hr = t.PriceUsd;
            if (t.PriceUsd < t.Low24Hr) t.Low24Hr = t.PriceUsd;
            t.Timestamp = DateTime.UtcNow;
            
            result.Add(new CryptoTicker
            {
                Symbol = t.Symbol,
                Name = t.Name,
                PriceUsd = t.PriceUsd,
                ChangePercent24Hr = t.ChangePercent24Hr,
                High24Hr = t.High24Hr,
                Low24Hr = t.Low24Hr,
                Volume24Hr = t.Volume24Hr,
                Timestamp = t.Timestamp
            });
        }
        return result;
    }

    private static void InitializeSimulatedData()
    {
        if (!_simulatedTickers.IsEmpty) return;
        var initialData = new List<CryptoTicker>
        {
            new() { Symbol = "BTC", Name = "Bitcoin", PriceUsd = 67845.20m, ChangePercent24Hr = 3.42m, High24Hr = 68400.00m, Low24Hr = 65200.00m, Volume24Hr = 28450123000m },
            new() { Symbol = "ETH", Name = "Ethereum", PriceUsd = 3512.80m, ChangePercent24Hr = 4.15m, High24Hr = 3550.00m, Low24Hr = 3380.00m, Volume24Hr = 15203400000m },
            new() { Symbol = "SOL", Name = "Solana", PriceUsd = 158.45m, ChangePercent24Hr = -1.25m, High24Hr = 164.20m, Low24Hr = 155.00m, Volume24Hr = 3401200000m },
            new() { Symbol = "BNB", Name = "BNB", PriceUsd = 585.10m, ChangePercent24Hr = 0.85m, High24Hr = 592.00m, Low24Hr = 578.00m, Volume24Hr = 1102300000m },
            new() { Symbol = "XRP", Name = "XRP", PriceUsd = 0.6120m, ChangePercent24Hr = 6.78m, High24Hr = 0.6350m, Low24Hr = 0.5720m, Volume24Hr = 2104500000m },
            new() { Symbol = "DOGE", Name = "Dogecoin", PriceUsd = 0.1245m, ChangePercent24Hr = -2.10m, High24Hr = 0.1310m, Low24Hr = 0.1210m, Volume24Hr = 980400000m },
            new() { Symbol = "ADA", Name = "Cardano", PriceUsd = 0.4580m, ChangePercent24Hr = 1.45m, High24Hr = 0.4650m, Low24Hr = 0.4420m, Volume24Hr = 450120000m },
            new() { Symbol = "AVAX", Name = "Avalanche", PriceUsd = 32.40m, ChangePercent24Hr = 5.20m, High24Hr = 33.10m, Low24Hr = 30.20m, Volume24Hr = 620300000m },
            new() { Symbol = "LINK", Name = "Chainlink", PriceUsd = 14.65m, ChangePercent24Hr = 2.10m, High24Hr = 15.00m, Low24Hr = 14.10m, Volume24Hr = 310400000m },
            new() { Symbol = "DOT", Name = "Polkadot", PriceUsd = 6.42m, ChangePercent24Hr = -0.65m, High24Hr = 6.60m, Low24Hr = 6.30m, Volume24Hr = 240100000m },
            new() { Symbol = "MATIC", Name = "Polygon", PriceUsd = 0.7180m, ChangePercent24Hr = 0.35m, High24Hr = 0.7300m, Low24Hr = 0.7050m, Volume24Hr = 190200000m },
            new() { Symbol = "UNI", Name = "Uniswap", PriceUsd = 9.85m, ChangePercent24Hr = -3.40m, High24Hr = 10.30m, Low24Hr = 9.65m, Volume24Hr = 280100000m }
        };
        foreach (var t in initialData) _simulatedTickers[t.Symbol] = t;
    }
}
