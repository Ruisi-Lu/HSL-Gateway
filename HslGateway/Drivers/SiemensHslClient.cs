using System;
using System.Text;
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

        var plcSeries = ResolvePlcSeries(config.PlcModel);
        _siemens = new SiemensS7Net(plcSeries)
        {
            IpAddress = config.Ip,
            Port = ResolvePort(config.Port)
        };

        if (config.Rack.HasValue)
        {
            _siemens.Rack = (byte)config.Rack.Value;
        }

        var slot = ResolveSlot(config.Slot, plcSeries);
        if (slot.HasValue)
        {
            _siemens.Slot = slot.Value;
        }

        _logger.LogInformation(
            "Device {DeviceId} using PLC {PlcSeries} (IP={Ip}, Port={Port}, Rack={Rack}, Slot={Slot})",
            _deviceId,
            plcSeries,
            _siemens.IpAddress,
            _siemens.Port,
            _siemens.Rack,
            _siemens.Slot);
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
           else if (string.Equals(dataType, "bool", StringComparison.OrdinalIgnoreCase))
           {
               var read = await _siemens.ReadBoolAsync(address);
               if (!read.IsSuccess) return LogReadFailure(address, read.Message);
               return read.Content ? 1d : 0d;
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
           else if (string.Equals(dataType, "bool", StringComparison.OrdinalIgnoreCase))
           {
               await _siemens.WriteAsync(address, Math.Abs(value) > double.Epsilon);
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

    private SiemensPLCS ResolvePlcSeries(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return SiemensPLCS.S1500;
        }

        if (Enum.TryParse(rawValue, true, out SiemensPLCS parsed))
        {
            return parsed;
        }

        var normalized = Normalize(rawValue);
        var (series, known) = normalized switch
        {
            "S71500" or "S1500" => (SiemensPLCS.S1500, true),
            "S71200" or "S1200" => (SiemensPLCS.S1200, true),
            "S7300" or "S300" => (SiemensPLCS.S300, true),
            "S7400" or "S400" => (SiemensPLCS.S400, true),
            "S7200SMART" or "S200SMART" => (SiemensPLCS.S200Smart, true),
            "S7200" or "S200" => (SiemensPLCS.S200, true),
            _ => (SiemensPLCS.S1500, false)
        };

        if (!known)
        {
            _logger.LogWarning(
                "Device {DeviceId} specified unknown PLC model '{PlcModel}', defaulting to S1500.",
                _deviceId,
                rawValue);
        }

        return series;
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static int ResolvePort(int value)
    {
        return value > 0 ? value : 102;
    }

    private static byte? ResolveSlot(int? configuredSlot, SiemensPLCS plcSeries)
    {
        if (configuredSlot.HasValue)
        {
            return (byte)configuredSlot.Value;
        }

        return plcSeries switch
        {
            SiemensPLCS.S300 => (byte)2,
            _ => null
        };
    }
}
