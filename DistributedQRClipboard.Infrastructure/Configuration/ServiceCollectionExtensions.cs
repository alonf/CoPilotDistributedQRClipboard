using Microsoft.Extensions.DependencyInjection;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Infrastructure.Services;

namespace DistributedQRClipboard.Infrastructure.Configuration;

/// <summary>
/// Extension methods for configuring infrastructure services in dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds infrastructure services to the dependency injection container.
    /// Note: ClipboardNotificationService is registered in the API layer where SignalR is available.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register background services
        services.AddHostedService<SessionCleanupService>();

        return services;
    }
}
