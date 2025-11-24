using HslGateway.Config;
using HslGateway.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HslGateway.Services;

public class PollingWorker : BackgroundService
{
    private readonly DeviceRegistry _registry;
    private readonly TagValueCache _cache;
    private readonly ILogger<PollingWorker> _logger;
    private readonly GatewayConfigStore _configStore;
    private readonly ConcurrentDictionary<string, DeviceRunner> _deviceRunners = new(StringComparer.OrdinalIgnoreCase);

    public PollingWorker(
        DeviceRegistry registry,
        TagValueCache cache,
        GatewayConfigStore configStore,
        ILogger<PollingWorker> logger)
    {
        _registry = registry;
        _cache = cache;
        _configStore = configStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SynchronizeDevices(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                await SynchronizeDevices(stoppingToken);
            }
        }
        finally
        {
            foreach (var runner in _deviceRunners.Values)
            {
                runner.CancellationTokenSource.Cancel();
            }

            await Task.WhenAll(_deviceRunners.Values.Select(r => r.Task));
        }
    }

    private Task SynchronizeDevices(CancellationToken token)
    {
        var desired = _configStore.GetDevices().ToDictionary(d => d.Id, d => d);

        foreach (var existing in _deviceRunners.Keys)
        {
            if (!desired.ContainsKey(existing))
            {
                if (_deviceRunners.TryRemove(existing, out var runner))
                {
                    runner.CancellationTokenSource.Cancel();
                    _cache.UpdateDeviceStatus(existing, false);
                }
            }
        }

        foreach (var device in desired.Values)
        {
            if (_deviceRunners.TryGetValue(device.Id, out var existingRunner))
            {
                if (existingRunner.ConfigurationSignature == GetSignature(device))
                {
                    continue;
                }

                existingRunner.CancellationTokenSource.Cancel();
                _deviceRunners.TryRemove(device.Id, out _);
            }

            StartDevice(device, token);
        }

        return Task.CompletedTask;
    }

    private void StartDevice(DeviceConfig device, CancellationToken parentToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
        var task = Task.Run(() => PollDeviceLoop(device, linkedCts.Token), linkedCts.Token);
        var runner = new DeviceRunner(device, GetSignature(device), linkedCts, task);
        _deviceRunners[device.Id] = runner;
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

                var failedPollCycles = 0;

                while (!token.IsCancellationRequested)
                {
                    var tags = _configStore.GetTags(device.Id).ToList();
                    var anySuccess = tags.Count == 0;

                    foreach (var tag in tags)
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
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling loop for device {Device}. Retrying in 5s...", device.Id);
                _cache.UpdateDeviceStatus(device.Id, false);
                try
                {
                    await Task.Delay(5000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static string GetSignature(DeviceConfig config)
    {
        return string.Join('|',
            config.Type,
            config.Ip,
            config.Port,
            config.PollIntervalMs,
            config.Rack?.ToString() ?? string.Empty,
            config.Slot?.ToString() ?? string.Empty,
            config.PortName ?? string.Empty,
            config.BaudRate?.ToString() ?? string.Empty,
            config.DataBits?.ToString() ?? string.Empty,
            config.StopBits?.ToString() ?? string.Empty,
            config.Parity?.ToString() ?? string.Empty,
            config.Station.ToString());
    }

    private sealed record DeviceRunner(
        DeviceConfig Device,
        string ConfigurationSignature,
        CancellationTokenSource CancellationTokenSource,
        Task Task);
}
