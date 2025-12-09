namespace Template.ApiServer.Host.Application;

using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using Microsoft.OpenApi;

using MiniDataProfiler;
using MiniDataProfiler.Listener.Logging;
using MiniDataProfiler.Listener.OpenTelemetry;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

using Smart.Data;
using Smart.Data.Accessor.Extensions.DependencyInjection;

using Template.ApiServer.Host.Application.Telemetry;
using Template.ApiServer.Host.Settings;

public static class ApplicationExtensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // TODO
    //private const string MetricsEndpointPath = "/metrics";

    //--------------------------------------------------------------------------------
    // System
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureSystem(this WebApplicationBuilder builder)
    {
        // Path
        builder.Configuration.SetBasePath(AppContext.BaseDirectory);

        // Encoding
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Host
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHost(this WebApplicationBuilder builder)
    {
        // Service
        builder.Host
            .UseWindowsService()
            .UseSystemd();

        // Feature management
        builder.Services.AddFeatureManagement();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Logging
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureLogging(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = builder.Configuration.IsOtelExporterEnabled();

        // Application log
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(options => options.ReadFrom.Configuration(builder.Configuration), writeToProviders: useOtlpExporter);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Http
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHttp(this IHostApplicationBuilder builder)
    {
        // Add services to the container.
        builder.Services.AddHttpContextAccessor();

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

        // TODO
        // XForward
        builder.Services.Configure<ForwardedHeadersOptions>(static options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Do not restrict to local network/proxy
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return builder;
    }

    //--------------------------------------------------------------------------------
    // API
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureApi(this IHostApplicationBuilder builder)
    {
        // TODO Filter

        // TODO
        builder.Services
            .AddControllers(options =>
            {
                options.Conventions.Add(NamingPolicy.PathNaming);
            })
            //.ConfigureApiBehaviorOptions(static options =>
            //{
            //})
            .AddJsonOptions(static options =>
            {
                options.AllowInputFormatterExceptionMessages = false;
                options.JsonSerializerOptions.PropertyNamingPolicy = NamingPolicy.JsonPropertyNaming;
                options.JsonSerializerOptions.DictionaryKeyPolicy = NamingPolicy.JsonDictionaryKeyNaming;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            });

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // TODO
        // Error handler
        builder.Services.AddProblemDetails();

        return builder;
    }

    public static WebApplication UseErrorHandler(this WebApplication app)
    {
        // Exception handler
        app.UseExceptionHandler();

        return app;
    }

    //--------------------------------------------------------------------------------
    // Compress
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureCompression(this IHostApplicationBuilder builder)
    {
        // TODO
        return builder;
    }

    public static WebApplication UseCompression(this WebApplication app)
    {
        // TODO
        return app;
    }

    //--------------------------------------------------------------------------------
    // OpenApi
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureOpenApi(this IHostApplicationBuilder builder)
    {
        // TODO
        // ReSharper disable UnusedParameter.Local
        builder.Services.AddOpenApi(options =>
        {
            // TODO
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                // ドキュメント情報の設定
                document.Info.Title = "My API";
                document.Info.Version = "v1";
                document.Info.Description = "説明をここに書く";
                document.Info.Contact = new OpenApiContact
                {
                    Name = "Team",
                    Email = "team@example.com"
                };
                document.Servers!.Add(new OpenApiServer
                {
                    Url = "https://api.example.com",
                    Description = "Production"
                });

                // 認証スキームの追加例
                //var bearerScheme = new OpenApiSecurityScheme
                //{
                //    Type = SecuritySchemeType.Http,
                //    Scheme = "bearer",
                //    BearerFormat = "JWT",
                //    Description = "JWT Authorization header using the Bearer scheme."
                //};
                //document.Components.SecuritySchemes["Bearer"] = bearerScheme;
                //...

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                operation.Description = "Custom operation description";

                // context.ApiDescriptionからController情報が取れるのでそれを使用する
                // 情報の書き換え
                operation.Summary = "サマリ";
                operation.Description = "Custom operation description";
                operation.Responses ??= new OpenApiResponses();
                operation.Responses["200"].Description = "成功";

                // ヘッダパラメータの追加
                operation.Parameters ??= new List<IOpenApiParameter>();
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-Correlation-ID",
                    In = ParameterLocation.Header,
                    Required = false,
                    Description = "トレース用"
                });

                return Task.CompletedTask;
            });
        });
        // ReSharper restore UnusedParameter.Local

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Health
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureHealth(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Telemetry
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureTelemetry(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = builder.Configuration.IsOtelExporterEnabled();

        var prometheusSection = builder.Configuration.GetSection("Prometheus");
        var prometheusUri = prometheusSection.GetValue<string>("Uri")!;
        var usePrometheusExporter = !String.IsNullOrEmpty(prometheusUri);

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(config =>
            {
                config.AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName);
            });

        // Log
        if (useOtlpExporter)
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });
            builder.Services.Configure<OpenTelemetryLoggerOptions>(static logging =>
            {
                logging.AddOtlpExporter();
            });
        }

        // Metrics
        if (useOtlpExporter || usePrometheusExporter)
        {
            telemetry
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddRuntimeInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation()
                        .AddApplicationInstrumentation();

                    if (useOtlpExporter)
                    {
                        metrics.AddOtlpExporter();
                    }

                    if (usePrometheusExporter)
                    {
                        metrics.AddPrometheusHttpListener(config =>
                        {
                            config.UriPrefixes = [prometheusUri];
                        });
                    }
                });
        }

        // Trace
        if (useOtlpExporter)
        {
            telemetry
                .WithTracing(tracing =>
                {
                    tracing
                        .AddSource(builder.Environment.ApplicationName)
                        .AddAspNetCoreInstrumentation()
                        .AddGrpcClientInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddMiniDataProfilerInstrumentation()
                        .AddApplicationInstrumentation();

                    tracing.AddOtlpExporter();

                    // TODO swagger condition
                    tracing
                        .AddAspNetCoreInstrumentation(options =>
                        {
                            options.Filter = context =>
                            {
                                var path = context.Request.Path;
                                return !path.StartsWithSegments(AlivenessEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments(HealthEndpointPath, StringComparison.OrdinalIgnoreCase) &&
                                       !path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
                            };
                        });
                });
        }

        // Custom instrument
        builder.Services.AddApplicationInstrument();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Components
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureComponents(this IHostApplicationBuilder builder)
    {
        // System
        builder.Services.AddSingleton(TimeProvider.System);

        // Data
        builder.Services.AddSingleton<IDbProvider>(static p =>
        {
            var configuration = p.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString("Default");

            var settings = p.GetRequiredService<ProfilerSetting>();
            if (settings.SqlTrace)
            {
                var logListener = new LoggingListener(p.GetRequiredService<ILogger<LoggingListener>>(), new LoggingListenerOption());
                var telemetryListener = new OpenTelemetryListener(new OpenTelemetryListenerOption());
                var listener = new ChainListener(logListener, telemetryListener);
                return new DelegateDbProvider(() => new ProfileDbConnection(listener, new SqliteConnection(connectionString)));
            }

            return new DelegateDbProvider(() => new SqliteConnection(connectionString));
        });

        // TODO Dialect

        // TODO option
        builder.Services.AddDataAccessor();

        // Cache
        builder.Services.AddMemoryCache();

        // Setting
        builder.Services.Configure<ProfilerSetting>(builder.Configuration.GetSection("Profiler"));
        builder.Services.AddSingleton<ProfilerSetting>(static p => p.GetRequiredService<IOptions<ProfilerSetting>>().Value);
        builder.Services.Configure<ServerSetting>(builder.Configuration.GetSection("Server"));
        builder.Services.AddSingleton<ServerSetting>(static p => p.GetRequiredService<IOptions<ServerSetting>>().Value);

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Information
    //--------------------------------------------------------------------------------

    public static void LogStartupInformation(this WebApplication app)
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);

        var prometheusSection = app.Configuration.GetSection("Prometheus");
        var prometheusUri = prometheusSection.GetValue("Uri", string.Empty);

        app.Logger.InfoServiceStart();
        app.Logger.InfoServiceSettingsRuntime(RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
        app.Logger.InfoServiceSettingsEnvironment(typeof(Program).Assembly.GetName().Version, Environment.CurrentDirectory);
        app.Logger.InfoServiceSettingsGC(GCSettings.IsServerGC, GCSettings.LatencyMode, GCSettings.LargeObjectHeapCompactionMode);
        app.Logger.InfoServiceSettingsThreadPool(workerThreads, completionPortThreads);
        app.Logger.InfoServiceSettingsTelemetry(app.Configuration.GetOtelExporterEndpoint(), prometheusUri);
    }

    //--------------------------------------------------------------------------------
    // Middleware
    //--------------------------------------------------------------------------------

    public static WebApplication UseMiddlewares(this WebApplication app)
    {
        // TODO Auth
        app.UseAuthorization();

        return app;
    }

    //--------------------------------------------------------------------------------
    // End point
    //--------------------------------------------------------------------------------

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            // TODO
            app.MapOpenApi();
            // [MEMO] Add yaml support
            app.MapOpenApi("/openapi/{documentName}.yaml");

            // Enable Swagger UI to use MapOpenApi generated specification
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "My API v1");
            });
        }

        // TODO Route

        // Controller
        app.MapControllers();

        // Health
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }

    //--------------------------------------------------------------------------------
    // Startup
    //--------------------------------------------------------------------------------

    public static ValueTask InitializeApplicationAsync(this WebApplication app)
    {
        // Prepare instrument
        app.Services.GetRequiredService<ApplicationInstrument>();

        // TODO data initialize
        return ValueTask.CompletedTask;
    }

    //--------------------------------------------------------------------------------
    // Configuration
    //--------------------------------------------------------------------------------

    private static bool IsOtelExporterEnabled(this IConfiguration configuration) =>
        !String.IsNullOrWhiteSpace(configuration.GetOtelExporterEndpoint());

    private static string GetOtelExporterEndpoint(this IConfiguration configuration) =>
        configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? string.Empty;
}
