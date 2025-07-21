using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using DistributedQRClipboard.Core.Interfaces;
using DistributedQRClipboard.Core.Services;
using DistributedQRClipboard.Core.Models;

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

    /// <summary>
    /// Adds clipboard management services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddClipboardManagement(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure clipboard manager options
        services.Configure<ClipboardManagerOptions>(configuration.GetSection("ClipboardManager"));

        // Register clipboard notification service (stub implementation)
        services.AddSingleton<IClipboardNotificationService, StubClipboardNotificationService>();

        // Register clipboard manager
        services.AddSingleton<IClipboardManager, ClipboardManager>();

        // Ensure memory cache is available
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds clipboard management services with custom options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure clipboard manager options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddClipboardManagement(this IServiceCollection services, Action<ClipboardManagerOptions> configureOptions)
    {
        // Configure clipboard manager options
        services.Configure(configureOptions);

        // Register clipboard notification service (stub implementation)
        services.AddSingleton<IClipboardNotificationService, StubClipboardNotificationService>();

        // Register clipboard manager
        services.AddSingleton<IClipboardManager, ClipboardManager>();

        // Ensure memory cache is available
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds QR code generation services with default options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQrCodeGeneration(this IServiceCollection services)
    {
        // Register QR code generator with default options
        services.Configure<QrCodeGeneratorOptions>(options => { });
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();
        
        // Ensure memory cache is available
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds QR code generation services with custom options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure QR code generator options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddQrCodeGeneration(this IServiceCollection services, Action<QrCodeGeneratorOptions> configureOptions)
    {
        // Configure QR code generator options
        services.Configure(configureOptions);

        // Register QR code generator
        services.AddSingleton<IQrCodeGenerator, QrCodeGenerator>();

        // Ensure memory cache is available
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds core services for the distributed QR clipboard system.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration instance</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDistributedQRClipboard(this IServiceCollection services, IConfiguration configuration)
    {
        // Add session management
        services.AddSessionManagement(configuration);

        // Add clipboard management
        services.AddClipboardManagement(configuration);

        // Add QR code generation
        services.AddQrCodeGeneration();

        return services;
    }

    /// <summary>
    /// Adds core services for the distributed QR clipboard system with default configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Add session management with default options
        services.AddSessionManagement(options => { });

        // Add clipboard management with default options
        services.AddClipboardManagement(options => { });

        // Add QR code generation with default options
        services.AddQrCodeGeneration(options => { });

        return services;
    }
}
