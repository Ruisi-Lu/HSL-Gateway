using Grpc.Core;
using HslGateway.Config;
using HslGateway.Grpc;
using Microsoft.Extensions.Options;

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
}
