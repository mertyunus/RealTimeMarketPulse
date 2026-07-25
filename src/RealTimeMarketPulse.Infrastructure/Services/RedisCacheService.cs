using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RealTimeMarketPulse.Domain.Interfaces;
using RealTimeMarketPulse.Domain.Models;
using StackExchange.Redis;

namespace RealTimeMarketPulse.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly ConcurrentDictionary<string, List<Delegate>> _localSubscribers = new();
    private IConnectionMultiplexer? _redis;
    private bool _isRedisConnected;
    private const string CacheKeyTickers = "pulse:tickers:latest";
    private readonly string _redisConnectionString;

    public RedisCacheService(IMemoryCache memoryCache, IConfiguration configuration, ILogger<RedisCacheService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        TryConnectRedis();
    }

    private void TryConnectRedis()
    {
        try
        {
            var options = ConfigurationOptions.Parse(_redisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 3000;
            options.SyncTimeout = 3000;
            _redis = ConnectionMultiplexer.Connect(options);
            _isRedisConnected = _redis.IsConnected;
            
            _redis.ConnectionFailed += (s, e) => { _isRedisConnected = false; _logger.LogWarning("Redis connection failed. Switching to In-Memory Fallback mode."); };
            _redis.ConnectionRestored += (s, e) => { _isRedisConnected = true; _logger.LogInformation("Redis connection restored."); };
            
            if (_isRedisConnected)
            {
                _logger.LogInformation("Successfully connected to Redis at {Endpoint}", _redisConnectionString);
            }
            else
            {
                _logger.LogWarning("Redis at {Endpoint} is not reachable. Operating in In-Memory Fallback mode.", _redisConnectionString);
            }
        }
        catch (Exception ex)
        {
            _isRedisConnected = false;
            _logger.LogWarning(ex, "Failed to initialize Redis multiplexer. Operating in In-Memory Fallback mode.");
        }
    }

    public bool IsConnected() => _redis != null && _isRedisConnected;

    public string GetCacheMode() => IsConnected() ? "Redis (Distributed & Pub/Sub)" : "In-Memory Cache (Local Fallback)";

    public async Task SetTickersAsync(IEnumerable<CryptoTicker> tickers, TimeSpan? expiration = null)
    {
        var tickerList = tickers.ToList();
        var exp = expiration ?? TimeSpan.FromMinutes(10);

        if (IsConnected() && _redis != null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(tickerList);
                await db.StringSetAsync(CacheKeyTickers, json, exp);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SetTickersAsync error. Falling back to MemoryCache.");
                _isRedisConnected = false;
            }
        }

        _memoryCache.Set(CacheKeyTickers, tickerList, exp);
    }

    public async Task<IEnumerable<CryptoTicker>> GetTickersAsync()
    {
        if (IsConnected() && _redis != null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var json = await db.StringGetAsync(CacheKeyTickers);
                if (!json.IsNullOrEmpty)
                {
                    var cached = JsonSerializer.Deserialize<List<CryptoTicker>>(json!);
                    if (cached != null) return cached;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis GetTickersAsync error. Falling back to MemoryCache.");
                _isRedisConnected = false;
            }
        }

        return _memoryCache.TryGetValue(CacheKeyTickers, out List<CryptoTicker>? tickers) && tickers != null 
            ? tickers 
            : Enumerable.Empty<CryptoTicker>();
    }

    public async Task PublishTickersAsync(string channel, IEnumerable<CryptoTicker> tickers)
    {
        var tickerList = tickers.ToList();
        if (IsConnected() && _redis != null)
        {
            try
            {
                var sub = _redis.GetSubscriber();
                var json = JsonSerializer.Serialize(tickerList);
                await sub.PublishAsync(RedisChannel.Literal(channel), json);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis Publish error on channel {Channel}. Falling back to local subscribers.", channel);
                _isRedisConnected = false;
            }
        }

        NotifyLocalSubscribers(channel, tickerList);
    }

    public async Task PublishAlertAsync(string channel, MarketAlert alert)
    {
        if (IsConnected() && _redis != null)
        {
            try
            {
                var sub = _redis.GetSubscriber();
                var json = JsonSerializer.Serialize(alert);
                await sub.PublishAsync(RedisChannel.Literal(channel), json);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis PublishAlert error. Falling back to local subscribers.");
                _isRedisConnected = false;
            }
        }

        NotifyLocalSubscribers(channel, alert);
    }

    public void Subscribe<T>(string channel, Action<T> handler)
    {
        if (IsConnected() && _redis != null)
        {
            try
            {
                var sub = _redis.GetSubscriber();
                sub.Subscribe(RedisChannel.Literal(channel), (ch, val) =>
                {
                    try
                    {
                        if (!val.IsNullOrEmpty)
                        {
                            var obj = JsonSerializer.Deserialize<T>(val!);
                            if (obj != null) handler(obj);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deserializing message on Redis channel {Channel}", channel);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis Subscribe error on channel {Channel}. Registering locally.", channel);
                _isRedisConnected = false;
            }
        }

        _localSubscribers.AddOrUpdate(
            channel,
            _ => new List<Delegate> { handler },
            (_, list) => { lock (list) { list.Add(handler); } return list; });
    }

    private void NotifyLocalSubscribers<T>(string channel, T data)
    {
        if (_localSubscribers.TryGetValue(channel, out var handlers))
        {
            lock (handlers)
            {
                foreach (var handler in handlers)
                {
                    if (handler is Action<T> action)
                    {
                        try { action(data); }
                        catch (Exception ex) { _logger.LogError(ex, "Error executing local subscriber handler for channel {Channel}", channel); }
                    }
                }
            }
        }
    }
}
