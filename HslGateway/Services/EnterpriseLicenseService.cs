using HslCommunication;
using HslGateway.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HslGateway.Services;

public sealed class EnterpriseLicenseService
{
    private readonly ILogger<EnterpriseLicenseService> _logger;
    private readonly IHostEnvironment _environment;
    private readonly EnterpriseLicenseOptions _options;
    private readonly object _sync = new();
    private string? _resolvedCertificatePath;

    public EnterpriseLicenseService(
        IOptions<EnterpriseLicenseOptions> options,
        IHostEnvironment environment,
        ILogger<EnterpriseLicenseService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public ActivationResult ActivateFromCertificate(byte[] certificateBytes, bool persist)
    {
        if (certificateBytes == null || certificateBytes.Length == 0)
        {
            return ActivationResult.Failure("certificateBytes is required");
        }

        try
        {
            Authorization.SetHslCertificate(certificateBytes);
            _logger.LogInformation("Enterprise certificate loaded successfully.");

            if (persist)
            {
                PersistCertificate(certificateBytes);
            }

            return ActivationResult.Successful("Enterprise features activated via certificate.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate enterprise certificate.");
            return ActivationResult.Failure($"Certificate activation failed: {ex.Message}");
        }
    }

    public ActivationResult ActivateFromAuthorizationCode(string authorizationCode)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            return ActivationResult.Failure("authorizationCode is required");
        }

        try
        {
            Authorization.SetAuthorizationCode(authorizationCode.Trim());
            _logger.LogInformation("Enterprise authorization code applied successfully.");
            return ActivationResult.Successful("Enterprise features activated via authorization code.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate enterprise authorization code.");
            return ActivationResult.Failure($"Authorization code activation failed: {ex.Message}");
        }
    }

    public ActivationResult TryLoadPersistedCertificate()
    {
        if (!_options.AutoLoadOnStartup)
        {
            return ActivationResult.Skipped("Auto-load disabled");
        }

        var path = GetCertificatePathOrDefault();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return ActivationResult.Skipped("No persisted certificate");
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            Authorization.SetHslCertificate(bytes);
            _logger.LogInformation("Enterprise certificate auto-loaded from {Path}", path);
            return ActivationResult.Successful("Enterprise certificate auto-loaded.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-load enterprise certificate from {Path}", path);
            return ActivationResult.Failure($"Auto-load failed: {ex.Message}");
        }
    }

    private void PersistCertificate(byte[] certificateBytes)
    {
        var path = GetCertificatePathOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("Persist certificate requested but no EnterpriseLicense:CertificateFilePath configured.");
            return;
        }

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, certificateBytes);
            _logger.LogInformation("Persisted enterprise certificate to {Path}", path);
        }
    }

    private string? GetCertificatePathOrDefault()
    {
        if (!string.IsNullOrWhiteSpace(_resolvedCertificatePath))
        {
            return _resolvedCertificatePath;
        }

        if (string.IsNullOrWhiteSpace(_options.CertificateFilePath))
        {
            return null;
        }

        var path = _options.CertificateFilePath;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(_environment.ContentRootPath, path);
        }

        _resolvedCertificatePath = Path.GetFullPath(path);
        return _resolvedCertificatePath;
    }

    public sealed record ActivationResult(bool Success, string Message)
    {
        public static ActivationResult Successful(string message) => new(true, message);
        public static ActivationResult Failure(string message) => new(false, message);
        public static ActivationResult Skipped(string message) => new(true, message);
    }
}
