using System.Collections.Concurrent;
using HslGateway.Models;

namespace HslGateway.Services;

public sealed class TagValueCache
{
    private readonly ConcurrentDictionary<string, TagValue> _cache = new();

    private static string GetKey(string deviceId, string tagName) => $"{deviceId}:{tagName}";

    public event Action<TagValue>? OnValueChanged;
    public event Action<string, bool>? OnDeviceStatusChanged;

    public void Save(TagValue value)
    {
        if (value == null) return;
        var key = GetKey(value.DeviceId, value.TagName);
        _cache[key] = value;
        OnValueChanged?.Invoke(value);
    }

    public void UpdateDeviceStatus(string deviceId, bool isOnline)
    {
        OnDeviceStatusChanged?.Invoke(deviceId, isOnline);
    }

    public TagValue? Get(string deviceId, string tagName)
    {
        var key = GetKey(deviceId, tagName);
        return _cache.TryGetValue(key, out var value) ? value : null;
    }

    public IEnumerable<TagValue> GetByDevice(string deviceId)
    {
        return _cache.Values.Where(v => v.DeviceId == deviceId);
    }
}
