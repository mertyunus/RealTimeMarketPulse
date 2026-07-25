using Microsoft.AspNetCore.Mvc;
using Quartz;
using RealTimeMarketPulse.Domain.Interfaces;
using RealTimeMarketPulse.Domain.Models;
using RealTimeMarketPulse.Workers.Jobs;

namespace RealTimeMarketPulse.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly ICryptoProviderService _cryptoProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IConfiguration _configuration;

    public MarketController(
        ICacheService cacheService,
        ICryptoProviderService cryptoProvider,
        ISchedulerFactory schedulerFactory,
        IConfiguration configuration)
    {
        _cacheService = cacheService;
        _cryptoProvider = cryptoProvider;
        _schedulerFactory = schedulerFactory;
        _configuration = configuration;
    }

    [HttpGet("tickers")]
    public async Task<IActionResult> GetTickers()
    {
        var tickers = await _cacheService.GetTickersAsync();
        return Ok(tickers);
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = new SystemStatus
        {
            IsRedisConnected = _cacheService.IsConnected(),
            CacheMode = _cacheService.GetCacheMode(),
            QuartzStatus = "Running",
            RefreshIntervalSeconds = _configuration.GetValue<int>("Quartz:IntervalSeconds", 3),
            TotalFetchCount = CryptoDataFetchJob.GetTotalExecutionCount(),
            LastUpdated = DateTime.UtcNow,
            ActiveProvider = _cryptoProvider.GetProviderName()
        };
        return Ok(status);
    }

    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerFetchNow()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.TriggerJob(new JobKey("CryptoDataFetchJob", "MarketPulseGroup"));
        return Ok(new { message = "Arka plan vergi toplama işi (Quartz Job) anlık olarak tetiklendi." });
    }

    [HttpPost("test-alert")]
    public async Task<IActionResult> TriggerTestAlert()
    {
        var alert = new MarketAlert
        {
            Symbol = "BTC",
            Title = "TEST UYARISI: BTC Hızlı Sıçrama!",
            Message = "Test amaçlı tetiklenen anlık yükseliş alarmı. Bitcoin (BTC) 5 dakikada %3.80 değer kazandı!",
            Severity = "warning",
            CurrentPrice = 68950.00m,
            ChangeRate = 3.80m
        };

        await _cacheService.PublishAlertAsync("pulse:channel:alerts", alert);
        return Ok(new { message = "Test uyarısı başarıyla yayınlandı ve SignalR üzerinden istemcilere aktarıldı." });
    }
}
