using Grpc.Core;
using HslGateway.Config;
using HslGateway.Grpc;
using HslGateway.Models;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace HslGateway.Services;

public class GatewayService : Gateway.GatewayBase
{
    private readonly TagValueCache _cache;
    private readonly GatewayConfig _config;
    private readonly DeviceRegistry _registry;
    private readonly ILogger<GatewayService> _logger;

    public GatewayService(
        TagValueCache cache,
        IOptions<GatewayConfig> config,
        DeviceRegistry registry,
        ILogger<GatewayService> logger)
    {
        _cache = cache;
        _config = config.Value;
        _registry = registry;
        _logger = logger;
    }

    public override Task<TagResponse> GetTagValue(TagRequest request, ServerCallContext context)
    {
        var val = _cache.Get(request.DeviceId, request.TagName);
        if (val == null)
        {
            return Task.FromResult(new TagResponse
            {
                DeviceId = request.DeviceId,
                TagName = request.TagName,
                Quality = "not_found",
                Value = 0
            });
        }

        return Task.FromResult(new TagResponse
        {
            DeviceId = val.DeviceId,
            TagName = val.TagName,
            Value = val.NumericValue ?? 0,
            TimestampUtc = val.TimestampUtc.ToString("o"),
            Quality = val.Quality
        });
    }

    public override Task<DeviceList> ListDevices(Empty request, ServerCallContext context)
    {
        var devices = _registry.GetDeviceIds().Select(id => new Device { Id = id });
        var response = new DeviceList();
        response.Devices.AddRange(devices);
        return Task.FromResult(response);
    }

    public override Task<TagList> ListDeviceTags(DeviceRequest request, ServerCallContext context)
    {
        var tags = _config.Tags
            .Where(t => t.DeviceId == request.DeviceId)
            .Select(t => new TagInfo
            {
                DeviceId = t.DeviceId,
                TagName = t.Name,
                Address = t.Address,
                DataType = t.DataType ?? "double"
            });
            
        var response = new TagList();
        response.Tags.AddRange(tags);
        return Task.FromResult(response);
    }
    public override async Task<WriteTagResponse> WriteTagValue(WriteTagRequest request, ServerCallContext context)
    {
        try
        {
            var client = _registry.GetClient(request.DeviceId);
            var tagConfig = _config.Tags.FirstOrDefault(t => t.DeviceId == request.DeviceId && t.Name == request.TagName);

            if (tagConfig == null)
            {
                return new WriteTagResponse { Success = false, Message = $"Tag '{request.TagName}' not found for device '{request.DeviceId}'" };
            }

            // Ensure connection (simple check, ideally client handles this)
            // For now, we assume the client is connected or will auto-connect on write if supported, 
            // but HslCommunication usually requires explicit connection.
            // The PollingWorker keeps it connected, but here we might be on a different thread/context.
            // Ideally IHslClient implementations should handle auto-reconnect or we use the existing connection.
            // Since PollingWorker holds the client and connects it, we can reuse it.
            // However, concurrent access might be an issue if not thread-safe. 
            // HslCommunication clients are generally thread-safe for Read/Write.

            await client.WriteAsync(tagConfig.Address, request.Value, tagConfig.DataType);
            
            // Update cache immediately to reflect change
            _cache.Save(new TagValue 
            { 
                DeviceId = request.DeviceId, 
                TagName = request.TagName, 
                NumericValue = request.Value, 
                TimestampUtc = DateTime.UtcNow, 
                Quality = "good" 
            });

            return new WriteTagResponse { Success = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing tag {Tag} to device {Device}", request.TagName, request.DeviceId);
            return new WriteTagResponse { Success = false, Message = ex.Message };
        }
    }
    public override async Task SubscribeTagValue(TagRequest request, IServerStreamWriter<TagResponse> responseStream, ServerCallContext context)
    {
        var channel = Channel.CreateUnbounded<TagValue>();

        void Handler(TagValue val)
        {
            if (val.DeviceId == request.DeviceId && val.TagName == request.TagName)
            {
                channel.Writer.TryWrite(val);
            }
        }

        _cache.OnValueChanged += Handler;

        try
        {
            // Send initial value if exists
            var current = _cache.Get(request.DeviceId, request.TagName);
            if (current != null)
            {
                await responseStream.WriteAsync(new TagResponse
                {
                    DeviceId = current.DeviceId,
                    TagName = current.TagName,
                    Value = current.NumericValue ?? 0,
                    TimestampUtc = current.TimestampUtc.ToString("o"),
                    Quality = current.Quality
                });
            }

            while (!context.CancellationToken.IsCancellationRequested)
            {
                var val = await channel.Reader.ReadAsync(context.CancellationToken);
                await responseStream.WriteAsync(new TagResponse
                {
                    DeviceId = val.DeviceId,
                    TagName = val.TagName,
                    Value = val.NumericValue ?? 0,
                    TimestampUtc = val.TimestampUtc.ToString("o"),
                    Quality = val.Quality
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            _cache.OnValueChanged -= Handler;
        }
    }

    public override async Task SubscribeDeviceStatus(DeviceRequest request, IServerStreamWriter<DeviceStatusResponse> responseStream, ServerCallContext context)
    {
        var channel = Channel.CreateUnbounded<(string DeviceId, bool IsOnline)>();

        void Handler(string deviceId, bool isOnline)
        {
            if (string.IsNullOrEmpty(request.DeviceId) || request.DeviceId == deviceId)
            {
                channel.Writer.TryWrite((deviceId, isOnline));
            }
        }

        _cache.OnDeviceStatusChanged += Handler;

        try
        {
            // Send initial status? 
            // We don't track current status in a queryable way in cache easily without adding more logic, 
            // but for now we just stream updates. 
            // Ideally we should send current status. Let's assume the client waits for updates or we add GetDeviceStatus later.
            // For now, just stream updates.

            while (!context.CancellationToken.IsCancellationRequested)
            {
                var (deviceId, isOnline) = await channel.Reader.ReadAsync(context.CancellationToken);
                await responseStream.WriteAsync(new DeviceStatusResponse
                {
                    DeviceId = deviceId,
                    IsOnline = isOnline,
                    TimestampUtc = DateTime.UtcNow.ToString("o")
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            _cache.OnDeviceStatusChanged -= Handler;
        }
    }
}
