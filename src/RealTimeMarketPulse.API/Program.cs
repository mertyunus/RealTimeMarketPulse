using RealTimeMarketPulse.API.Hubs;
using RealTimeMarketPulse.API.Services;
using RealTimeMarketPulse.Domain.Interfaces;
using RealTimeMarketPulse.Infrastructure.Services;
using RealTimeMarketPulse.Workers.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ICryptoProviderService, CryptoProviderService>();
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

builder.Services.AddPulseWorkers(builder.Configuration);
builder.Services.AddHostedService<SignalRBroadcastHostedService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();
app.MapHub<MarketHub>("/hub/market");

app.Run();
