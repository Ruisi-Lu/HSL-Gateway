using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HslGateway.Config;
using Microsoft.Extensions.Hosting;
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
    private readonly string _configFilePath;
    private readonly object _persistenceSync = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public event EventHandler<ConfigChange>? ConfigChanged;

    public GatewayConfigStore(
        IOptions<GatewayConfig> options,
        IOptions<GatewayPersistenceOptions> persistenceOptions,
        IHostEnvironment environment,
        ILogger<GatewayConfigStore> logger)
    {
        _logger = logger;
        _configFilePath = ResolveConfigPath(persistenceOptions.Value, environment);

        var initialConfig = LoadInitialConfig(options.Value);
        foreach (var device in initialConfig.Devices)
        {
            _devices[device.Id] = Clone(device);
        }

        foreach (var tag in initialConfig.Tags)
        {
            _tags[NormalizeKey(tag.DeviceId, tag.Name)] = Clone(tag);
        }

        if (!File.Exists(_configFilePath))
        {
            TryPersistSnapshot();
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
        TryPersistSnapshot();
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
        TryPersistSnapshot();
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
        TryPersistSnapshot();
    }

    public bool RemoveTag(string deviceId, string tagName)
    {
        if (_tags.TryRemove(NormalizeKey(deviceId, tagName), out _))
        {
            _logger.LogInformation("Tag config removed: {DeviceId}/{TagName}", deviceId, tagName);
            RaiseChange(ConfigChangeKind.TagRemoved, deviceId, tagName);
            TryPersistSnapshot();
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

    private GatewayConfig LoadInitialConfig(GatewayConfig optionsConfig)
    {
        optionsConfig ??= new GatewayConfig();
        optionsConfig.Devices ??= new List<DeviceConfig>();
        optionsConfig.Tags ??= new List<TagConfig>();

        if (TryLoadFromDisk(out var persisted))
        {
            _logger.LogInformation("Loaded gateway configuration from {Path}", _configFilePath);
            return persisted;
        }

        _logger.LogInformation("Using gateway configuration from appsettings (no persisted file yet). Path: {Path}", _configFilePath);
        return optionsConfig ?? new GatewayConfig();
    }

    private bool TryLoadFromDisk(out GatewayConfig config)
    {
        config = new GatewayConfig();

        try
        {
            if (!File.Exists(_configFilePath))
            {
                return false;
            }

            var json = File.ReadAllText(_configFilePath);
            var snapshot = JsonSerializer.Deserialize<GatewayConfig>(json, _serializerOptions);
            if (snapshot == null)
            {
                _logger.LogWarning("Persisted config file {Path} was empty or invalid.", _configFilePath);
                return false;
            }

            snapshot.Devices ??= new List<DeviceConfig>();
            snapshot.Tags ??= new List<TagConfig>();
            config = snapshot;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load gateway config from {Path}; falling back to defaults.", _configFilePath);
            return false;
        }
    }

    private void TryPersistSnapshot()
    {
        try
        {
            PersistSnapshot();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist gateway configuration to {Path}", _configFilePath);
            throw;
        }
    }

    private void PersistSnapshot()
    {
        var snapshot = new GatewayConfig
        {
            Devices = _devices.Values.Select(Clone).ToList(),
            Tags = _tags.Values.Select(Clone).ToList()
        };

        lock (_persistenceSync)
        {
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempFile = _configFilePath + ".tmp";
            var json = JsonSerializer.Serialize(snapshot, _serializerOptions);
            File.WriteAllText(tempFile, json);
            if (File.Exists(_configFilePath))
            {
                File.Replace(tempFile, _configFilePath, null);
            }
            else
            {
                File.Move(tempFile, _configFilePath);
            }
        }
    }

    private static string ResolveConfigPath(GatewayPersistenceOptions options, IHostEnvironment environment)
    {
        var configuredPath = options.ConfigFilePath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = Path.Combine(environment.ContentRootPath, "data", "gateway-config.json");
        }

        if (!Path.IsPathRooted(configuredPath))
        {
            configuredPath = Path.Combine(environment.ContentRootPath, configuredPath);
        }

        return Path.GetFullPath(configuredPath);
    }
}
