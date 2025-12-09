using Microsoft.Extensions.Hosting.WindowsServices;

using Template.ApiServer.Host.Application;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default
});

// System
builder.ConfigureSystem();

// Host
builder.ConfigureHost();

// Logging
builder.ConfigureLogging();

// Http
builder.ConfigureHttp();
// API
builder.ConfigureApi();
// Compress
builder.ConfigureCompression();
// TODO
//// Swagger
//builder.ConfigureSwagger();

// Health
builder.ConfigureHealth();
// Metrics
builder.ConfigureTelemetry();

// Components
builder.ConfigureComponents();

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline.
//--------------------------------------------------------------------------------
var app = builder.Build();

// Startup information
app.LogStartupInformation();

// TODO
// Logging
//app.UseLogging();
//app.UseLoggingContext();

// TODO order

// TODO
//// Forwarded headers
//app.UseForwardedHeaders();

// TODO
//// Buffered response
//app.UseBufferedResponse();

// Error handler
app.UseErrorHandler();

// Compression
app.UseCompression();

// End point
app.MapEndpoints();

// Initialize
await app.InitializeApplicationAsync();

// Run
await app.RunAsync();
