using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using RealTimeMarketPulse.Workers.Jobs;

namespace RealTimeMarketPulse.Workers.Extensions;

public static class QuartzExtensions
{
    public static IServiceCollection AddPulseWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        int intervalSeconds = configuration.GetValue<int>("Quartz:IntervalSeconds", 3);
        if (intervalSeconds <= 0) intervalSeconds = 3;

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("CryptoDataFetchJob", "MarketPulseGroup");
            q.AddJob<CryptoDataFetchJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("CryptoDataFetchTrigger", "MarketPulseGroup")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(intervalSeconds)
                    .RepeatForever()));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }
}
