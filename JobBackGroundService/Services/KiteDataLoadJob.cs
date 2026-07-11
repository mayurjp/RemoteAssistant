using System.Text.Json;
using RemoteAssistant.Core.Database;
using Microsoft.Extensions.Logging;

namespace JobBackGroundService.Services;

public class KiteDataLoadJob : IJobExecutor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KiteDataLoadJob> _logger;

    public KiteDataLoadJob(IHttpClientFactory httpClientFactory, ILogger<KiteDataLoadJob> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string JobType => "KiteDataLoad";

    public async Task<string> ExecuteAsync(JobRequest job, CancellationToken ct)
    {
        _logger.LogInformation("KiteDataLoadJob running for Job {JobId}", job.Id);

        var candles = await FetchMockOhlcDataAsync(ct);

        return JsonSerializer.Serialize(candles, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<List<OhlcCandle>> FetchMockOhlcDataAsync(CancellationToken ct)
    {
        // Simulate API call with mock OHLC data
        await Task.Delay(500, ct);

        var random = new Random();
        var candles = new List<OhlcCandle>();
        var basePrice = random.Next(1000, 2000) + random.NextDouble();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 10; i++)
        {
            var open = basePrice + random.NextDouble() * 20 - 10;
            var close = open + random.NextDouble() * 15 - 7.5;
            var high = Math.Max(open, close) + random.NextDouble() * 5;
            var low = Math.Min(open, close) - random.NextDouble() * 5;
            var volume = random.Next(1000, 50000);

            candles.Add(new OhlcCandle
            {
                Timestamp = now.AddMinutes(-(10 - i) * 5),
                Open = Math.Round(open, 2),
                High = Math.Round(high, 2),
                Low = Math.Round(low, 2),
                Close = Math.Round(close, 2),
                Volume = volume
            });

            basePrice = close;
        }

        _logger.LogInformation("Generated {Count} mock OHLC candles", candles.Count);
        return candles;
    }
}

public class OhlcCandle
{
    public DateTime Timestamp { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public int Volume { get; set; }
}
