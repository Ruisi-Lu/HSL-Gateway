using HslCommunication;
using HslCommunication.ModBus;
using HslGateway.Config;
using Microsoft.Extensions.Logging;

namespace HslGateway.Drivers;

public class ModbusRtuHslClient : IHslClient
{
    private readonly ModbusRtu _modbus;
    private readonly ILogger _logger;
    private readonly string _deviceId;

    public ModbusRtuHslClient(DeviceConfig config, ILogger logger)
    {
        _logger = logger;
        _deviceId = config.Id;
        _modbus = new ModbusRtu(config.Station);
        
        if (!string.IsNullOrEmpty(config.PortName))
        {
            try 
            {
                _modbus.SerialPortInni(sp => 
                {
                    sp.PortName = config.PortName;
                    sp.BaudRate = config.BaudRate ?? 9600;
                    sp.DataBits = config.DataBits ?? 8;
                    sp.StopBits = (System.IO.Ports.StopBits)(config.StopBits ?? 1);
                    sp.Parity = (System.IO.Ports.Parity)(config.Parity ?? 0);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure serial port for device {DeviceId}", _deviceId);
            }
        }
        else
        {
            _logger.LogWarning("Device {DeviceId} is ModbusRtu but no PortName specified.", _deviceId);
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try 
        {
            _modbus.Open();
            _logger.LogInformation("Device {DeviceId} serial port opened.", _deviceId);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Device {DeviceId} connection failed.", _deviceId);
             throw;
        }
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
        _modbus.Close();
    }
}
