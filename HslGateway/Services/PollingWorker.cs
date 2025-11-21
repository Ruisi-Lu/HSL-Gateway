using HslGateway.Config;
using HslGateway.Models;
using Microsoft.Extensions.Options;

namespace HslGateway.Services;

public class PollingWorker : BackgroundService
{
    private readonly GatewayConfig _config;
    private readonly DeviceRegistry _registry;
    private readonly TagValueCache _cache;
    private readonly ILogger<PollingWorker> _logger;

    public PollingWorker(
        IOptions<GatewayConfig> config,
        DeviceRegistry registry,
        TagValueCache cache,
        ILogger<PollingWorker> logger)
    {
        _config = config.Value;
        _registry = registry;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = _config.Devices.Select(d => PollDeviceLoop(d, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task PollDeviceLoop(DeviceConfig device, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = _registry.GetClient(device.Id);
                await client.ConnectAsync(token);
                _cache.UpdateDeviceStatus(device.Id, true);
                
                var deviceTags = _config.Tags.Where(t => t.DeviceId == device.Id).ToList();
                var failedPollCycles = 0;

                while (!token.IsCancellationRequested)
                {
                    var anySuccess = false;
                    if (deviceTags.Count == 0)
                    {
                        anySuccess = true; // nothing to poll but connection is alive
                    }

                    foreach (var tag in deviceTags)
                    {
                        try
                        {
                            var val = await client.ReadAsync(tag.Address, tag.DataType, token);
                            var tagValue = new TagValue
                            {
                                DeviceId = device.Id,
                                TagName = tag.Name,
                                NumericValue = val,
                                TimestampUtc = DateTime.UtcNow,
                                Quality = val.HasValue ? "good" : "bad"
                            };
                            _cache.Save(tagValue);

                            if (val.HasValue)
                            {
                                anySuccess = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error reading tag {Tag} from {Device}", tag.Name, device.Id);
                        }
                    }

                    if (!anySuccess)
                    {
                        failedPollCycles++;
                        _logger.LogWarning("Device {Device} had no successful tag reads in cycle #{Cycle}", device.Id, failedPollCycles);
                        if (failedPollCycles >= 3)
                        {
                            throw new InvalidOperationException($"Device {device.Id} failed to return data for {failedPollCycles} consecutive polls.");
                        }
                    }
                    else
                    {
                        failedPollCycles = 0;
                        _cache.UpdateDeviceStatus(device.Id, true);
                    }
                    
                    await Task.Delay(device.PollIntervalMs, token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling loop for device {Device}. Retrying in 5s...", device.Id);
                _cache.UpdateDeviceStatus(device.Id, false);
                await Task.Delay(5000, token);
            }
        }
    }
}
