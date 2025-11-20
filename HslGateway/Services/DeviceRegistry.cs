using HslGateway.Config;
using HslGateway.Drivers;
using Microsoft.Extensions.Options;

namespace HslGateway.Services;

public class DeviceRegistry
{
    private readonly Dictionary<string, IHslClient> _clients = new();
    private readonly ILogger<DeviceRegistry> _logger;

    public DeviceRegistry(IOptions<GatewayConfig> config, ILogger<DeviceRegistry> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        foreach (var device in config.Value.Devices)
        {
            try 
            {
                IHslClient client = device.Type switch
                {
                    "SiemensS7" => new SiemensHslClient(device, loggerFactory.CreateLogger<SiemensHslClient>()),
                    "ModbusTcp" => new ModbusTcpHslClient(device, loggerFactory.CreateLogger<ModbusTcpHslClient>()),
                    _ => throw new NotSupportedException($"Device type '{device.Type}' is not supported.")
                };
                _clients.Add(device.Id, client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize device {DeviceId}", device.Id);
            }
        }
    }

    public IHslClient GetClient(string deviceId)
    {
        if (_clients.TryGetValue(deviceId, out var client))
        {
            return client;
        }
        throw new KeyNotFoundException($"Device '{deviceId}' not found.");
    }

    public IEnumerable<string> GetDeviceIds() => _clients.Keys;
}
