using HslCommunication;
using HslCommunication.ModBus;
using HslGateway.Config;
using Microsoft.Extensions.Logging;

namespace HslGateway.Drivers;

public class ModbusTcpHslClient : IHslClient
{
    private readonly ModbusTcpNet _modbus;
    private readonly ILogger _logger;
    private readonly string _deviceId;

    public ModbusTcpHslClient(DeviceConfig config, ILogger logger)
    {
        _logger = logger;
        _deviceId = config.Id;
        _modbus = new ModbusTcpNet(config.Ip, config.Port);
        // Default Station ID is usually 1.
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var result = await _modbus.ConnectServerAsync();
        if (!result.IsSuccess)
        {
            _logger.LogError("Device {DeviceId} connection failed: {Message}", _deviceId, result.Message);
            throw new Exception($"Connection failed: {result.Message}");
        }
        _logger.LogInformation("Device {DeviceId} connected.", _deviceId);
    }

    public async Task<double?> ReadAsync(string address, string? dataType = null, CancellationToken cancellationToken = default)
    {
        if (string.Equals(dataType, "int", StringComparison.OrdinalIgnoreCase))
        {
             var read = await _modbus.ReadInt32Async(address);
             if (!read.IsSuccess) return LogReadFailure(address, read.Message);
             return (double)read.Content;
        }
        else if (string.Equals(dataType, "short", StringComparison.OrdinalIgnoreCase))
        {
             var read = await _modbus.ReadInt16Async(address);
             if (!read.IsSuccess) return LogReadFailure(address, read.Message);
             return (double)read.Content;
        }
        else if (string.Equals(dataType, "float", StringComparison.OrdinalIgnoreCase))
        {
             var read = await _modbus.ReadFloatAsync(address);
             if (!read.IsSuccess) return LogReadFailure(address, read.Message);
             return (double)read.Content;
        }

        var result = await _modbus.ReadDoubleAsync(address);
        if (!result.IsSuccess) return LogReadFailure(address, result.Message);
        return result.Content;
    }

    private double? LogReadFailure(string address, string message)
    {
        _logger.LogWarning("Read failed for {DeviceId} tag {Address}: {Message}", _deviceId, address, message);
        return null;
    }

    public async Task WriteAsync(string address, double value, string? dataType = null, CancellationToken cancellationToken = default)
    {
        if (string.Equals(dataType, "int", StringComparison.OrdinalIgnoreCase))
        {
             await _modbus.WriteAsync(address, (int)value);
        }
        else if (string.Equals(dataType, "short", StringComparison.OrdinalIgnoreCase))
        {
             await _modbus.WriteAsync(address, (short)value);
        }
        else if (string.Equals(dataType, "float", StringComparison.OrdinalIgnoreCase))
        {
             await _modbus.WriteAsync(address, (float)value);
        }
        else
        {
             await _modbus.WriteAsync(address, value);
        }
    }

    public void Disconnect()
    {
        _modbus.ConnectClose();
    }
}
