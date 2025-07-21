using DistributedQRClipboard.Core.Configuration;
using DistributedQRClipboard.Infrastructure.Configuration;
using DistributedQRClipboard.Api.Endpoints;
using DistributedQRClipboard.Api.Middleware;
using DistributedQRClipboard.Api.Hubs;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Distributed QR Clipboard API", 
        Version = "v1",
        Description = "A distributed clipboard system using QR codes for seamless data sharing across devices",
        Contact = new() { Name = "API Support" }
    });
});

// Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

// Add Core services
builder.Services.AddCoreServices();

// Add Infrastructure services
builder.Services.AddInfrastructureServices();

// Register SignalR-based notification service
builder.Services.AddSingleton<DistributedQRClipboard.Core.Interfaces.IClipboardNotificationService, DistributedQRClipboard.Infrastructure.Services.ClipboardNotificationService>();

// Add global exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
    
    options.AddPolicy("SignalRPolicy", builder =>
    {
        builder
            .SetIsOriginAllowed(_ => true) // Allow any origin for development
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Required for SignalR
    });
});

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
    
    // Add Swagger and Swagger UI
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Distributed QR Clipboard API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Distributed QR Clipboard API";
        options.DefaultModelsExpandDepth(2);
        options.DefaultModelExpandDepth(2);
    });
}

app.UseHttpsRedirection();
app.UseCors("SignalRPolicy");

// Add middleware
app.UseRateLimiting();
app.UseRequestValidation();
app.UseExceptionHandler();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health");

// Basic API info endpoint
app.MapGet("/", () => Results.Ok(new { 
    Name = "Distributed QR Clipboard API", 
    Version = "1.0.0",
    Description = "A distributed clipboard system using QR codes for seamless data sharing across devices" 
}))
    .WithName("ApiInfo")
    .WithTags("Info");

// Map API endpoints
app.MapSessionEndpoints();
app.MapClipboardEndpoints();

// Map SignalR hub
app.MapHub<ClipboardHub>("/clipboardhub");

app.Run();
