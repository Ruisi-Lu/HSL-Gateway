namespace HslGateway.Config;

public class EnterpriseLicenseOptions
{
    /// <summary>
    /// Optional path to the persisted enterprise certificate file. Relative paths are resolved against the content root.
    /// </summary>
    public string? CertificateFilePath { get; set; }

    /// <summary>
    /// Whether the gateway should attempt to load the persisted certificate on startup.
    /// </summary>
    public bool AutoLoadOnStartup { get; set; } = true;

    /// <summary>
    /// Name of the environment variable that contains the enterprise certificate as a Base64 string.
    /// Defaults to <c>HSL_ENTERPRISE_CERT_BASE64</c> when not specified.
    /// </summary>
    public string? CertificateBase64EnvironmentVariable { get; set; } = "HSL_ENTERPRISE_CERT_BASE64";

    /// <summary>
    /// Name of the environment variable that contains the authorization code string (fallback when no certificate is provided).
    /// Defaults to <c>HSL_ENTERPRISE_AUTH_CODE</c> when not specified.
    /// </summary>
    public string? AuthorizationCodeEnvironmentVariable { get; set; } = "HSL_ENTERPRISE_AUTH_CODE";

    /// <summary>
    /// Optional custom contact info passed to <c>Authorization.SetDllContact</c> after successful activation.
    /// </summary>
    public string? ContactInfo { get; set; }
}
