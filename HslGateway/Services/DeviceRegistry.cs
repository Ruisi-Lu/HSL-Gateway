using HslGateway.Config;
using HslGateway.Drivers;
using System.Collections.Concurrent;

namespace HslGateway.Services;

public class DeviceRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, IHslClient> _clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly GatewayConfigStore _configStore;
    private readonly ILogger<DeviceRegistry> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public DeviceRegistry(GatewayConfigStore configStore, ILogger<DeviceRegistry> logger, ILoggerFactory loggerFactory)
    {
        _configStore = configStore;
        _logger = logger;
        _loggerFactory = loggerFactory;

        foreach (var device in _configStore.GetDevices())
        {
            TryAddOrUpdateClient(device);
        }

        _configStore.ConfigChanged += OnConfigChanged;
    }

    public IHslClient GetClient(string deviceId)
    {
        if (_clients.TryGetValue(deviceId, out var client))
        {
            return client;
        }

        var config = _configStore.GetDevice(deviceId) ?? throw new KeyNotFoundException($"Device '{deviceId}' not found.");
        if (TryAddOrUpdateClient(config, out client, forceReplace: false))
        {
            return client ?? throw new InvalidOperationException($"Device '{deviceId}' client could not be created.");
        }

        throw new KeyNotFoundException($"Device '{deviceId}' not found.");
    }

    public IEnumerable<string> GetDeviceIds() => _clients.Keys;

    private void OnConfigChanged(object? sender, ConfigChange change)
    {
        switch (change.Kind)
        {
            case ConfigChangeKind.DeviceAdded:
            case ConfigChangeKind.DeviceUpdated:
                var device = _configStore.GetDevice(change.DeviceId);
                if (device != null)
                {
                    TryAddOrUpdateClient(device, forceReplace: true);
                }
                break;
            case ConfigChangeKind.DeviceRemoved:
                if (_clients.TryRemove(change.DeviceId, out var removed))
                {
                    removed.Disconnect();
                    _logger.LogInformation("Removed device client {DeviceId}", change.DeviceId);
                }
                break;
        }
    }

    private bool TryAddOrUpdateClient(DeviceConfig device, out IHslClient? client, bool forceReplace = false)
    {
        client = null;
        try
        {
            if (!forceReplace && _clients.ContainsKey(device.Id))
            {
                client = _clients[device.Id];
                return true;
            }

            var newClient = CreateClient(device);
            if (_clients.TryGetValue(device.Id, out var existing))
            {
                existing.Disconnect();
            }

            _clients[device.Id] = newClient;
            client = newClient;
            _logger.LogInformation("Initialized client for device {DeviceId}", device.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize device {DeviceId}", device.Id);
            return false;
        }
    }

    private bool TryAddOrUpdateClient(DeviceConfig device, bool forceReplace = false)
    {
        return TryAddOrUpdateClient(device, out _, forceReplace);
    }

    private IHslClient CreateClient(DeviceConfig device)
    {
        return device.Type switch
        {
            "SiemensS7" => new SiemensHslClient(device, _loggerFactory.CreateLogger<SiemensHslClient>()),
            "ModbusTcp" => new ModbusTcpHslClient(device, _loggerFactory.CreateLogger<ModbusTcpHslClient>()),
            "ModbusRtu" => new ModbusRtuHslClient(device, _loggerFactory.CreateLogger<ModbusRtuHslClient>()),
            _ => throw new NotSupportedException($"Device type '{device.Type}' is not supported.")
        };
    }

    public void Dispose()
    {
        _configStore.ConfigChanged -= OnConfigChanged;
        foreach (var client in _clients.Values)
        {
            client.Disconnect();
        }
    }
}
