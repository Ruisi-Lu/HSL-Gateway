namespace HslGateway.Drivers;

public interface IHslClient
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<double?> ReadAsync(string address, string? dataType = null, CancellationToken cancellationToken = default);
    Task WriteAsync(string address, double value, string? dataType = null, CancellationToken cancellationToken = default);
    void Disconnect();
}
