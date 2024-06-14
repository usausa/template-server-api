using System.IO.Compression;
using System.Net.Mime;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

using Prometheus;

using Serilog;

using Smart.AspNetCore;
using Smart.AspNetCore.ApplicationModels;

using Swashbuckle.AspNetCore.SwaggerGen;

using Template.Web;
using Template.Web.Application.HealthChecks;
using Template.Web.Application.RateLimiting;
using Template.Web.Application.Swagger;
using Template.Web.Settings;

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

// Configuration
var serverSetting = builder.Configuration.GetSection("Server").Get<ServerSetting>()!;
builder.Services.AddSingleton(serverSetting);

// Log
builder.Logging.ClearProviders();
builder.Services.AddSerilog(option =>
{
    option.ReadFrom.Configuration(builder.Configuration);
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpLogging(static options =>
    {
        //options.LoggingFields = HttpLoggingFields.All | HttpLoggingFields.RequestQuery;
        options.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders |
                                HttpLoggingFields.RequestQuery |
                                HttpLoggingFields.ResponsePropertiesAndHeaders;
    });
}

// Add services to the container.
builder.Services.AddHttpContextAccessor();

// Feature management
builder.Services.AddFeatureManagement();

// Size limit
builder.Services.Configure<KestrelServerOptions>(static options =>
{
    options.Limits.MaxRequestBodySize = Int32.MaxValue;
});

// Route
builder.Services.Configure<RouteOptions>(static options =>
{
    options.AppendTrailingSlash = true;
});

// XForward
builder.Services.Configure<ForwardedHeadersOptions>(static options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Do not restrict to local network/proxy
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS
//builder.Services.Configure<CorsOptions>(static options =>
//{
//});

// Filter
builder.Services.AddTimeLogging(options =>
{
    options.Threshold = serverSetting.LongTimeThreshold;
});

// API
builder.Services
    .AddControllers(static options =>
    {
        options.Conventions.Add(new KebabControllerModelConvention());
        options.Filters.AddTimeLogging();
    })
    .AddJsonOptions(static options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        options.JsonSerializerOptions.Converters.Add(new Template.Web.Infrastructure.Json.DateTimeConverter());
    });

builder.Services.AddEndpointsApiExplorer();

builder.Services
    .AddApiVersioning(static options =>
    {
        options.DefaultApiVersion = new ApiVersion(1.0);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(static options =>
    {
        options.SubstituteApiVersionInUrl = true;
        options.GroupNameFormat = "'v'VVV";
        options.AssumeDefaultVersionWhenUnspecified = true;
    });

// Swagger
if (!builder.Environment.IsProduction())
{
    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    builder.Services.AddSwaggerGen();
}

// Rate limit
builder.Services.AddRateLimiter(builder.Configuration.GetSection("RateLimit").Get<RateLimitSetting>()!);

// Error handler
builder.Services.AddProblemDetails(static options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.Add("nodeId", Environment.MachineName);
    };
});

// Compress
builder.Services.AddRequestDecompression();
builder.Services.AddResponseCompression(static options =>
{
    // Default false (for CRIME and BREACH attacks)
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = [MediaTypeNames.Application.Json];
});
builder.Services.Configure<GzipCompressionProviderOptions>(static options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Health
builder.Services
    .AddHealthChecks()
    .AddCheck<CustomHealthCheck>("custom_check", tags: ["app"]);

// Profiler
if (!builder.Environment.IsProduction())
{
    builder.Services.AddMiniProfiler(static options =>
    {
        options.RouteBasePath = "/profiler";
    });
}

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline
//--------------------------------------------------------------------------------
var app = builder.Build();

// Startup information
ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
app.Logger.InfoServiceStart();
app.Logger.InfoServiceSettingsEnvironment(typeof(Program).Assembly.GetName().Version, Environment.Version, Environment.CurrentDirectory);
app.Logger.InfoServiceSettingsGC(GCSettings.IsServerGC, GCSettings.LatencyMode, GCSettings.LargeObjectHeapCompactionMode);
app.Logger.InfoServiceSettingsThreadPool(workerThreads, completionPortThreads);

// Log
if (app.Environment.IsDevelopment())
{
    // Serilog
    app.UseSerilogRequestLogging(static options =>
    {
        options.IncludeQueryInRequestPath = true;
    });

    // HTTP log
    app.UseHttpLogging();
}

// Forwarded headers
app.UseForwardedHeaders();

// Error handler
app.UseExceptionHandler();

// Develop
if (!app.Environment.IsProduction())
{
    // Profiler
    app.UseMiniProfiler();

    // Swagger
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in app.DescribeApiVersions())
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName);
        }
    });
}

// HSTS
//if (!app.Environment.IsDevelopment())
//{
//    app.UseHsts();
//}

// HTTPS redirection
//app.UseHttpsRedirection();

// Routing
app.UseRouting();

// Rate limit
app.UseRateLimiter();

// CORS
//app.UseCors();

// Metrics
app.UseHttpMetrics();

// Authentication
app.UseAuthorization();

// Compression
app.UseResponseCompression();
//app.UseRequestDecompression();
//app.UseResponseCaching();

// API
app.MapControllers();
app.MapGet("/", static context => context.Response.WriteAsync("API Service"));

// Health
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckWriter.WriteResponse
});

// Metrics
app.MapMetrics();

// Initialize
await app.InitializeAsync();

// Run
await app.RunAsync();
