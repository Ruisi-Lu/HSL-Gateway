using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HslGateway.Services;

public class EnterpriseLicenseStartupService : IHostedService
{
    private readonly EnterpriseLicenseService _licenseService;
    private readonly ILogger<EnterpriseLicenseStartupService> _logger;

    public EnterpriseLicenseStartupService(EnterpriseLicenseService licenseService, ILogger<EnterpriseLicenseStartupService> logger)
    {
        _licenseService = licenseService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var result = _licenseService.TryLoadPersistedCertificate();
        _logger.LogInformation("Enterprise license startup result: {Message}", result.Message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
