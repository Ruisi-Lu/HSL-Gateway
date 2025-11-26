using HslCommunication;
using HslGateway.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HslGateway.Services;

/// <summary>
/// Loads the HslCommunication enterprise license (certificate or authorization code) during host startup.
/// </summary>
public class EnterpriseLicenseInitializer : IHostedService
{
    private readonly EnterpriseLicenseOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EnterpriseLicenseInitializer> _logger;

    public EnterpriseLicenseInitializer(
        IOptions<EnterpriseLicenseOptions> options,
        IHostEnvironment environment,
        ILogger<EnterpriseLicenseInitializer> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoLoadOnStartup)
        {
            _logger.LogInformation("Enterprise license auto-load disabled.");
            return Task.CompletedTask;
        }

        var activated = TryLoadFromEnvironmentCertificate()
                        || TryLoadFromEnvironmentAuthorizationCode()
                        || TryLoadFromFileCertificate();

        if (activated)
        {
            if (!string.IsNullOrWhiteSpace(_options.ContactInfo))
            {
                Authorization.SetDllContact(_options.ContactInfo);
            }

            _logger.LogInformation("HslCommunication enterprise features enabled.");
        }
        else
        {
            _logger.LogWarning("HslCommunication enterprise license was not loaded. The gateway will continue in evaluation mode.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool TryLoadFromEnvironmentCertificate()
    {
        var envVar = string.IsNullOrWhiteSpace(_options.CertificateBase64EnvironmentVariable)
            ? "HSL_ENTERPRISE_CERT_BASE64"
            : _options.CertificateBase64EnvironmentVariable;

        var base64 = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(base64))
        {
            return false;
        }

        try
        {
            // Whitespace or accidental newline characters are ignored automatically by Convert.FromBase64String.
            var certBytes = Convert.FromBase64String(base64);
            Authorization.SetHslCertificate(certBytes);
            _logger.LogInformation("Enterprise certificate loaded from environment variable {EnvVar}.", envVar);
            return true;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode Base64 certificate from environment variable {EnvVar}.", envVar);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply enterprise certificate from environment variable {EnvVar}.", envVar);
            return false;
        }
    }

    private bool TryLoadFromEnvironmentAuthorizationCode()
    {
        var envVar = string.IsNullOrWhiteSpace(_options.AuthorizationCodeEnvironmentVariable)
            ? "HSL_ENTERPRISE_AUTH_CODE"
            : _options.AuthorizationCodeEnvironmentVariable;

        var code = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        try
        {
            var success = Authorization.SetAuthorizationCode(code);
            if (success)
            {
                _logger.LogInformation("Enterprise authorization code loaded from environment variable {EnvVar}.", envVar);
            }
            else
            {
                _logger.LogWarning("Authorization.SetAuthorizationCode returned false for environment variable {EnvVar}.", envVar);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate enterprise authorization code from environment variable {EnvVar}.", envVar);
            return false;
        }
    }

    private bool TryLoadFromFileCertificate()
    {
        if (string.IsNullOrWhiteSpace(_options.CertificateFilePath))
        {
            return false;
        }

        try
        {
            var path = _options.CertificateFilePath;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(_environment.ContentRootPath, path);
            }

            if (!File.Exists(path))
            {
                _logger.LogDebug("Enterprise certificate file {Path} not found.", path);
                return false;
            }

            var certBytes = File.ReadAllBytes(path);
            Authorization.SetHslCertificate(certBytes);
            _logger.LogInformation("Enterprise certificate loaded from file {Path}.", path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load enterprise certificate from file {FilePath}.", _options.CertificateFilePath);
            return false;
        }
    }
}
