using System.Collections.Concurrent;
using System.Linq;
using HslGateway.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HslGateway.Services;

public enum ConfigChangeKind
{
    DeviceAdded,
    DeviceUpdated,
    DeviceRemoved,
    TagAdded,
    TagUpdated,
    TagRemoved
}

public sealed record ConfigChange(ConfigChangeKind Kind, string DeviceId, string? TagName = null);

public class GatewayConfigStore
{
    private readonly ConcurrentDictionary<string, DeviceConfig> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(string deviceId, string tagName), TagConfig> _tags = new();
    private readonly ILogger<GatewayConfigStore> _logger;

    public event EventHandler<ConfigChange>? ConfigChanged;

    public GatewayConfigStore(IOptions<GatewayConfig> options, ILogger<GatewayConfigStore> logger)
    {
        _logger = logger;
        foreach (var device in options.Value.Devices)
        {
            _devices[device.Id] = Clone(device);
        }

        foreach (var tag in options.Value.Tags)
        {
            _tags[NormalizeKey(tag.DeviceId, tag.Name)] = Clone(tag);
        }
    }

    public IReadOnlyCollection<DeviceConfig> GetDevices()
    {
        return _devices.Values.Select(Clone).ToList();
    }

    public DeviceConfig? GetDevice(string deviceId)
    {
        return _devices.TryGetValue(deviceId, out var config) ? Clone(config) : null;
    }

    public IReadOnlyCollection<TagConfig> GetTags(string? deviceId = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return _tags.Values.Select(Clone).ToList();
        }

        return _tags
            .Where(t => string.Equals(t.Key.deviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            .Select(t => Clone(t.Value))
            .ToList();
    }

    public TagConfig? GetTag(string deviceId, string tagName)
    {
        return _tags.TryGetValue(NormalizeKey(deviceId, tagName), out var tag) ? Clone(tag) : null;
    }

    public void UpsertDevice(DeviceConfig config)
    {
        var cloned = Clone(config);
        var exists = _devices.TryGetValue(config.Id, out var current);
        _devices[config.Id] = cloned;
        _logger.LogInformation("Device config upserted: {DeviceId}", config.Id);
        RaiseChange(exists ? ConfigChangeKind.DeviceUpdated : ConfigChangeKind.DeviceAdded, config.Id);
    }

    public bool RemoveDevice(string deviceId)
    {
        if (!_devices.TryRemove(deviceId, out _))
        {
            return false;
        }

        var removedTags = _tags.Keys.Where(k => string.Equals(k.deviceId, deviceId, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var key in removedTags)
        {
            _tags.TryRemove(key, out _);
            RaiseChange(ConfigChangeKind.TagRemoved, key.deviceId, key.tagName);
        }

        _logger.LogInformation("Device config removed: {DeviceId}", deviceId);
        RaiseChange(ConfigChangeKind.DeviceRemoved, deviceId);
        return true;
    }

    public void UpsertTag(TagConfig config)
    {
        var cloned = Clone(config);
        var key = NormalizeKey(config.DeviceId, config.Name);
        var exists = _tags.ContainsKey(key);
        _tags[key] = cloned;
        _logger.LogInformation("Tag config upserted: {DeviceId}/{TagName}", config.DeviceId, config.Name);
        RaiseChange(exists ? ConfigChangeKind.TagUpdated : ConfigChangeKind.TagAdded, config.DeviceId, config.Name);
    }

    public bool RemoveTag(string deviceId, string tagName)
    {
        if (_tags.TryRemove(NormalizeKey(deviceId, tagName), out _))
        {
            _logger.LogInformation("Tag config removed: {DeviceId}/{TagName}", deviceId, tagName);
            RaiseChange(ConfigChangeKind.TagRemoved, deviceId, tagName);
            return true;
        }

        return false;
    }

    private static DeviceConfig Clone(DeviceConfig source)
    {
        return new DeviceConfig
        {
            Id = source.Id,
            Type = source.Type,
            Ip = source.Ip,
            Port = source.Port,
            Rack = source.Rack,
            Slot = source.Slot,
            PollIntervalMs = source.PollIntervalMs,
            PortName = source.PortName,
            BaudRate = source.BaudRate,
            DataBits = source.DataBits,
            StopBits = source.StopBits,
            Parity = source.Parity,
            Station = source.Station
        };
    }

    private static TagConfig Clone(TagConfig source)
    {
        return new TagConfig
        {
            DeviceId = source.DeviceId,
            Name = source.Name,
            Address = source.Address,
            DataType = source.DataType
        };
    }

    private void RaiseChange(ConfigChangeKind kind, string deviceId, string? tagName = null)
    {
        var handler = ConfigChanged;
        handler?.Invoke(this, new ConfigChange(kind, deviceId, tagName));
    }

    private static (string deviceId, string tagName) NormalizeKey(string deviceId, string tagName)
    {
        return (deviceId.ToLowerInvariant(), tagName.ToLowerInvariant());
    }
}
