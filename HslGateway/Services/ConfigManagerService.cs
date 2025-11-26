using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using HslGateway.Config;
using HslGateway.Grpc;
using Microsoft.Extensions.Logging;
using ProtoEmpty = HslGateway.Grpc.Empty;

namespace HslGateway.Services;

public class ConfigManagerService : ConfigManager.ConfigManagerBase
{
    private readonly GatewayConfigStore _configStore;
    private readonly ILogger<ConfigManagerService> _logger;

    public ConfigManagerService(GatewayConfigStore configStore, ILogger<ConfigManagerService> logger)
    {
        _configStore = configStore;
        _logger = logger;
    }

    public override Task<DeviceConfigList> ListDevicesConfig(ProtoEmpty request, ServerCallContext context)
    {
        var response = new DeviceConfigList();
        response.Devices.AddRange(_configStore.GetDevices().Select(ToDto));
        return Task.FromResult(response);
    }

    public override Task<OperationStatus> UpsertDevice(DeviceConfigDto request, ServerCallContext context)
    {
        var validationError = ValidateDevice(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            return Task.FromResult(Failure(validationError));
        }

        var model = ToDeviceConfig(request);
        try
        {
            _configStore.UpsertDevice(model);
            _logger.LogInformation("Upserted device {DeviceId}", model.Id);
            return Task.FromResult(Success("Device saved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert device {DeviceId}", model.Id);
            return Task.FromResult(Failure($"Failed to save device: {ex.Message}"));
        }
    }

    public override Task<OperationStatus> DeleteDevice(DeviceRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return Task.FromResult(Failure("deviceId is required"));
        }

        try
        {
            var removed = _configStore.RemoveDevice(request.DeviceId);
            return Task.FromResult(removed ? Success("Device removed") : Failure("Device not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove device {DeviceId}", request.DeviceId);
            return Task.FromResult(Failure($"Failed to remove device: {ex.Message}"));
        }
    }

    public override Task<TagConfigList> ListTagConfigs(DeviceRequest request, ServerCallContext context)
    {
        var tags = string.IsNullOrWhiteSpace(request.DeviceId)
            ? _configStore.GetTags()
            : _configStore.GetTags(request.DeviceId);

        var response = new TagConfigList();
        response.Tags.AddRange(tags.Select(ToDto));
        return Task.FromResult(response);
    }

    public override Task<OperationStatus> UpsertTag(TagConfigDto request, ServerCallContext context)
    {
        var validationError = ValidateTag(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            return Task.FromResult(Failure(validationError));
        }

        if (_configStore.GetDevice(request.DeviceId) == null)
        {
            return Task.FromResult(Failure($"Device '{request.DeviceId}' not found"));
        }

        try
        {
            _configStore.UpsertTag(ToTagConfig(request));
            _logger.LogInformation("Upserted tag {DeviceId}/{TagName}", request.DeviceId, request.Name);
            return Task.FromResult(Success("Tag saved"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert tag {DeviceId}/{TagName}", request.DeviceId, request.Name);
            return Task.FromResult(Failure($"Failed to save tag: {ex.Message}"));
        }
    }

    public override Task<OperationStatus> DeleteTag(TagIdentifier request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.TagName))
        {
            return Task.FromResult(Failure("deviceId and tagName are required"));
        }

        try
        {
            var removed = _configStore.RemoveTag(request.DeviceId, request.TagName);
            return Task.FromResult(removed ? Success("Tag removed") : Failure("Tag not found"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove tag {DeviceId}/{TagName}", request.DeviceId, request.TagName);
            return Task.FromResult(Failure($"Failed to remove tag: {ex.Message}"));
        }
    }

    private static string? ValidateDevice(DeviceConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            return "device id is required";
        }

        if (string.IsNullOrWhiteSpace(dto.Type))
        {
            return "device type is required";
        }

        if (dto.PollIntervalMs <= 0)
        {
            return "pollIntervalMs must be > 0";
        }

        var isSerial = string.Equals(dto.Type, "ModbusRtu", StringComparison.OrdinalIgnoreCase);
        if (isSerial)
        {
            if (string.IsNullOrWhiteSpace(dto.PortName))
            {
                return "portName is required for ModbusRtu";
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Ip))
            {
                return "ip is required";
            }
        }

        return null;
    }

    private static string? ValidateTag(TagConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceId))
        {
            return "deviceId is required";
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return "tag name is required";
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            return "tag address is required";
        }

        return null;
    }

    private static DeviceConfig ToDeviceConfig(DeviceConfigDto dto)
    {
        var station = dto.Station == 0 ? 1u : dto.Station;
        station = Math.Min(station, (uint)byte.MaxValue);

        return new DeviceConfig
        {
            Id = dto.Id.Trim(),
            Type = dto.Type.Trim(),
            Ip = string.IsNullOrWhiteSpace(dto.Ip) ? string.Empty : dto.Ip.Trim(),
            Port = dto.Port,
            PollIntervalMs = dto.PollIntervalMs,
            Rack = dto.Rack,
            Slot = dto.Slot,
            PlcModel = string.IsNullOrWhiteSpace(dto.PlcModel) ? null : dto.PlcModel.Trim(),
            PortName = string.IsNullOrWhiteSpace(dto.PortName) ? null : dto.PortName.Trim(),
            BaudRate = dto.BaudRate,
            DataBits = dto.DataBits,
            StopBits = dto.StopBits,
            Parity = dto.Parity,
            Station = (byte)station
        };
    }

    private static TagConfig ToTagConfig(TagConfigDto dto)
    {
        return new TagConfig
        {
            DeviceId = dto.DeviceId.Trim(),
            Name = dto.Name.Trim(),
            Address = dto.Address.Trim(),
            DataType = string.IsNullOrWhiteSpace(dto.DataType) ? null : dto.DataType.Trim()
        };
    }

    private static DeviceConfigDto ToDto(DeviceConfig config)
    {
        var dto = new DeviceConfigDto
        {
            Id = config.Id,
            Type = config.Type,
            Ip = config.Ip,
            Port = config.Port,
            PollIntervalMs = config.PollIntervalMs,
            PortName = config.PortName ?? string.Empty,
            Station = config.Station,
            PlcModel = config.PlcModel ?? string.Empty
        };

        dto.Rack = config.Rack;
        dto.Slot = config.Slot;
        dto.BaudRate = config.BaudRate;
        dto.DataBits = config.DataBits;
        dto.StopBits = config.StopBits;
        dto.Parity = config.Parity;

        return dto;
    }

    private static TagConfigDto ToDto(TagConfig config)
    {
        return new TagConfigDto
        {
            DeviceId = config.DeviceId,
            Name = config.Name,
            Address = config.Address,
            DataType = config.DataType ?? string.Empty
        };
    }

    private static OperationStatus Success(string message) => new() { Success = true, Message = message };
    private static OperationStatus Failure(string message) => new() { Success = false, Message = message };
}
