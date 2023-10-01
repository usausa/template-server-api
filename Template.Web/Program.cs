using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

using Prometheus;

using Serilog;

using Smart.AspNetCore;
using Smart.AspNetCore.ApplicationModels;

using Swashbuckle.AspNetCore.SwaggerGen;

using Template.Web.Application.HealthChecks;
using Template.Web.Application.RateLimiting;
using Template.Web.Settings;

using TMN.TerminalEmulator.Service.Application.Swagger;

#pragma warning disable CA1852

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
});

// Service
builder.Host
    .UseWindowsService()
    .UseSystemd();

// Log
builder.Logging.ClearProviders();
builder.Services.AddSerilog(option =>
{
    option.ReadFrom.Configuration(builder.Configuration);
});

// Add services to the container.
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue;
});

// Feature management
builder.Services.AddFeatureManagement();

// Settings
var serverSetting = builder.Configuration.GetSection("Server").Get<ServerSetting>()!;
builder.Services.AddSingleton(serverSetting);

// Route
builder.Services.Configure<RouteOptions>(options =>
{
    options.AppendTrailingSlash = true;
});

// Filter
builder.Services.AddTimeLogging(options =>
{
    options.Threshold = serverSetting.LongTimeThreshold;
});

// API
builder.Services
    .AddControllers(options =>
    {
        options.Filters.AddTimeLogging();
        options.Conventions.Add(new LowercaseControllerModelConvention());
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        options.JsonSerializerOptions.Converters.Add(new Template.Web.Infrastructure.Json.DateTimeConverter());
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

builder.Services.AddApiVersioning(options =>
{
    options.ReportApiVersions = true;
});
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Profiler
if (!builder.Environment.IsProduction())
{
    builder.Services.AddMiniProfiler(options =>
    {
        options.RouteBasePath = "/profiler";
    });
}

// Rate limit
builder.Services.AddRateLimiter(builder.Configuration.GetSection("RateLimit").Get<RateLimitSetting>()!);

// Health
builder.Services
    .AddHealthChecks()
    .AddCheck<CustomHealthCheck>("custom_check", tags: new[] { "app" });

// Swagger
builder.Services.AddSwaggerGen();

// Profiler
builder.Services.AddMiniProfiler(options =>
{
    options.RouteBasePath = "/profiler";
});

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline
//--------------------------------------------------------------------------------
var app = builder.Build();

// Serilog
if (!app.Environment.IsProduction())
{
    app.UseSerilogRequestLogging(options =>
    {
        options.IncludeQueryInRequestPath = true;
    });
}

// HTTPS redirection
app.UseHttpsRedirection();

// Health
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckWriter.WriteResponse
});

// Metrics
app.UseHttpMetrics();

if (!app.Environment.IsProduction())
{
    // Profiler
    app.UseMiniProfiler();

    // Swagger
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
#pragma warning disable S3267
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
#pragma warning restore S3267
    });
}

// Authentication
app.UseAuthorization();

// API
app.MapControllers();
app.MapGet("/", async context => await context.Response.WriteAsync("API Service"));

// Metrics
app.MapMetrics();

// Rate limit
app.UseRateLimiter();

// Run
app.Run();
