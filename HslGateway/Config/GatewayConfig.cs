namespace HslGateway.Config;

public class GatewayConfig
{
    public List<DeviceConfig> Devices { get; set; } = new();
    public List<TagConfig> Tags { get; set; } = new();
}

public class DeviceConfig
{
    public string Id { get; set; } = default!;
    public string Type { get; set; } = default!; // e.g. "SiemensS7", "ModbusTcp"
    public string Ip { get; set; } = default!;
    public int Port { get; set; }
    public int? Rack { get; set; } // for Siemens
    public int? Slot { get; set; } // for Siemens
    public int PollIntervalMs { get; set; } = 1000;
}

public class TagConfig
{
    public string DeviceId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? DataType { get; set; } // e.g. "int", "double", etc.
}
