namespace HslGateway.Config;

public class GatewayPersistenceOptions
{
    /// <summary>
    /// Relative or absolute path to the persisted gateway configuration file.
    /// If relative, it is resolved against the application content root.
    /// </summary>
    public string? ConfigFilePath { get; set; }
}
