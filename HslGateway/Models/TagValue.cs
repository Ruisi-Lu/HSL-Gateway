namespace HslGateway.Models;

public class TagValue
{
    public string DeviceId { get; set; } = default!;
    public string TagName { get; set; } = default!;
    public double? NumericValue { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Quality { get; set; } = "unknown"; // e.g. "good" / "bad"
}
