using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.FeatureManagement;

using Serilog;

using Smart.AspNetCore;
using Smart.AspNetCore.ApplicationModels;

using Template.Web.Application.HealthChecks;

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

// Route
builder.Services.Configure<RouteOptions>(options =>
{
    options.AppendTrailingSlash = true;
});

// Filter
builder.Services.AddTimeLogging(options =>
{
    options.Threshold = 5000;
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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Swagger
builder.Services.AddSwaggerGen();

// Health
builder.Services
    .AddHealthChecks()
    .AddCheck<CustomHealthCheck>("custom_check", tags: new[] { "app" });

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline
//--------------------------------------------------------------------------------
var app = builder.Build();

// Serilog
if (app.Environment.IsDevelopment())
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication
app.UseAuthorization();

// API
app.MapControllers();
app.MapGet("/", async context => await context.Response.WriteAsync("API Service"));

// Run
app.Run();
