using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Services;

namespace DistributedQRClipboard.Core.Configuration;

/// <summary>
/// Extension methods for configuring core services in dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core session management services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSessionManagement(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure session manager options
        services.Configure<SessionManagerOptions>(configuration.GetSection("SessionManager"));

        // Register session manager as singleton to maintain session state
        services.AddSingleton<ISessionManager, SessionManager>();

        // Ensure memory cache is available
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds core session management services with custom options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure session manager options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddSessionManagement(this IServiceCollection services, Action<SessionManagerOptions> configureOptions)
    {
        // Configure session manager options
        services.Configure(configureOptions);

        // Register session manager as singleton to maintain session state
        services.AddSingleton<ISessionManager, SessionManager>();

        // Ensure memory cache is available
        services.AddMemoryCache();

        return services;
    }
}
