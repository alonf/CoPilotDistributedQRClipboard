using System.Collections.Concurrent;
using System.Net;

namespace DistributedQRClipboard.Api.Middleware;

/// <summary>
/// Simple in-memory rate limiting middleware.
/// </summary>
public class RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RateLimitingMiddleware> _logger = logger;
    private readonly ConcurrentDictionary<string, ClientRequestTracker> _clients = new();

    /// <summary>
    /// Rate limiting configuration options.
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Maximum requests per window (default: 100).
        /// </summary>
        public int MaxRequests { get; set; } = 100;

        /// <summary>
        /// Time window in minutes (default: 15).
        /// </summary>
        public int WindowMinutes { get; set; } = 15;

        /// <summary>
        /// Whether to use IP address for client identification (default: true).
        /// </summary>
        public bool UseIpAddress { get; set; } = true;

        /// <summary>
        /// Custom headers to use for client identification.
        /// </summary>
        public string[] IdentificationHeaders { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Tracks request count and timestamps for a client.
    /// </summary>
    private sealed class ClientRequestTracker
    {
        private readonly Queue<DateTime> _requestTimes = new();
        private readonly object _lock = new();

        public bool IsAllowed(int maxRequests, TimeSpan window)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.Subtract(window);
                
                // Remove old requests outside the window
                while (_requestTimes.Count > 0 && _requestTimes.Peek() < cutoff)
                {
                    _requestTimes.Dequeue();
                }

                // Check if we're under the limit
                if (_requestTimes.Count >= maxRequests)
                {
                    return false;
                }

                // Add current request
                _requestTimes.Enqueue(DateTime.UtcNow);
                return true;
            }
        }

        public int CurrentRequestCount
        {
            get
            {
                lock (_lock)
                {
                    return _requestTimes.Count;
                }
            }
        }
    }

    /// <summary>
    /// Processes the rate limiting for the request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var options = new RateLimitOptions(); // In production, this would come from configuration
        
        var clientId = GetClientIdentifier(context, options);
        var client = _clients.GetOrAdd(clientId, _ => new ClientRequestTracker());

        var window = TimeSpan.FromMinutes(options.WindowMinutes);
        
        if (!client.IsAllowed(options.MaxRequests, window))
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId}. Current requests: {RequestCount}/{MaxRequests}", 
                clientId, client.CurrentRequestCount, options.MaxRequests);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = window.TotalSeconds.ToString();
            
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Gets a unique identifier for the client making the request.
    /// </summary>
    private static string GetClientIdentifier(HttpContext context, RateLimitOptions options)
    {
        var identifiers = new List<string>();

        // Use IP address if configured
        if (options.UseIpAddress)
        {
            var ipAddress = GetClientIpAddress(context);
            if (!string.IsNullOrEmpty(ipAddress))
            {
                identifiers.Add($"ip:{ipAddress}");
            }
        }

        // Use custom headers if configured
        foreach (var header in options.IdentificationHeaders)
        {
            if (context.Request.Headers.TryGetValue(header, out var value))
            {
                identifiers.Add($"{header}:{value}");
            }
        }

        // Fallback to a generic identifier
        if (identifiers.Count == 0)
        {
            identifiers.Add("anonymous");
        }

        return string.Join("|", identifiers);
    }

    /// <summary>
    /// Extracts the client IP address from the HTTP context.
    /// </summary>
    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check X-Forwarded-For header first (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        // Check X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Use connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}

/// <summary>
/// Extension methods for adding rate limiting middleware.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds rate limiting middleware to the pipeline.
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <returns>The application builder for method chaining</returns>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
