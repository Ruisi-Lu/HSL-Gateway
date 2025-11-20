using HslCommunication;
using HslCommunication.Profinet.Siemens;
using HslGateway.Config;
using Microsoft.Extensions.Logging;

namespace HslGateway.Drivers;

public class SiemensHslClient : IHslClient
{
    private readonly SiemensS7Net _siemens;
    private readonly ILogger _logger;
    private readonly string _deviceId;

    public SiemensHslClient(DeviceConfig config, ILogger logger)
    {
        _logger = logger;
        _deviceId = config.Id;
        
        // Default to S1500. In a real app, we might parse config.Type to distinguish S1200/S300 etc.
        _siemens = new SiemensS7Net(SiemensPLCS.S1500);
        _siemens.IpAddress = config.Ip;
        _siemens.Port = config.Port;
        if (config.Rack.HasValue) _siemens.Rack = (byte)config.Rack.Value;
        if (config.Slot.HasValue) _siemens.Slot = (byte)config.Slot.Value;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var result = await _siemens.ConnectServerAsync();
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
             var read = await _siemens.ReadInt32Async(address);
             if (!read.IsSuccess) return LogReadFailure(address, read.Message);
             return (double)read.Content;
        }
        else if (string.Equals(dataType, "short", StringComparison.OrdinalIgnoreCase))
        {
             var read = await _siemens.ReadInt16Async(address);
             if (!read.IsSuccess) return LogReadFailure(address, read.Message);
             return (double)read.Content;
        }
        else if (string.Equals(dataType, "float", StringComparison.OrdinalIgnoreCase))
        {
             var read = await _siemens.ReadFloatAsync(address);
             if (!read.IsSuccess) return LogReadFailure(address, read.Message);
             return (double)read.Content;
        }

        // Default to double
        var res = await _siemens.ReadDoubleAsync(address);
        if (!res.IsSuccess) return LogReadFailure(address, res.Message);
        return res.Content;
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
             await _siemens.WriteAsync(address, (int)value);
        }
        else if (string.Equals(dataType, "short", StringComparison.OrdinalIgnoreCase))
        {
             await _siemens.WriteAsync(address, (short)value);
        }
        else if (string.Equals(dataType, "float", StringComparison.OrdinalIgnoreCase))
        {
             await _siemens.WriteAsync(address, (float)value);
        }
        else
        {
             await _siemens.WriteAsync(address, value);
        }
    }

    public void Disconnect()
    {
        _siemens.ConnectClose();
    }
}
