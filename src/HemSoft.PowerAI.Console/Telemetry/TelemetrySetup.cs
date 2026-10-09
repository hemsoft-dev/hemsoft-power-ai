// <copyright file="TelemetrySetup.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// Configures OpenTelemetry for traces, metrics, and logs.
/// Exports to OTLP endpoint (Aspire Dashboard) when OTEL_EXPORTER_OTLP_ENDPOINT is set.
/// Also exports traces to JSON files for debugging (7-day retention).
/// Console output can be enabled via OTEL_CONSOLE_EXPORTER=true.
/// </summary>
internal sealed partial class TelemetrySetup : IDisposable
{
    private const string ServiceName = "HemSoft.PowerAI.Console";
    private const string ServiceVersion = "1.0.0";
    private const int TraceRetentionDays = 7;

    private static readonly string TraceDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HemSoft.PowerAI",
        "Traces");

    private readonly TracerProvider? tracerProvider;
    private readonly MeterProvider? meterProvider;
    private readonly ServiceProvider serviceProvider;
    private readonly FileTraceExporter fileTraceExporter;
    private readonly SimpleActivityExportProcessor fileTraceProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetrySetup"/> class.
    /// </summary>
    /// <param name="sourceName">The source name for tracing and metrics.</param>
    public TelemetrySetup(string sourceName)
    {
        var config = ExporterConfig.FromEnvironment();

        this.ActivitySource = new ActivitySource(sourceName, ServiceVersion);
        this.Meter = new Meter(sourceName, ServiceVersion);

        var resourceBuilder = CreateResourceBuilder();

        this.fileTraceExporter = new FileTraceExporter(TraceDirectory, TraceRetentionDays);
        this.fileTraceProcessor = new SimpleActivityExportProcessor(this.fileTraceExporter);
        this.tracerProvider = BuildTracerProvider(sourceName, resourceBuilder, config, this.fileTraceProcessor);
        this.meterProvider = BuildMeterProvider(sourceName, resourceBuilder, config);
        this.serviceProvider = BuildLoggingServiceProvider(resourceBuilder, config);
        this.LoggerFactory = this.serviceProvider.GetRequiredService<ILoggerFactory>();

        LogConfiguration(this.LoggerFactory.CreateLogger<TelemetrySetup>(), config);
    }

    /// <summary>
    /// Gets the activity source for creating spans.
    /// </summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>
    /// Gets the meter for creating metrics.
    /// </summary>
    public Meter Meter { get; }

    /// <summary>
    /// Gets the logger factory.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create a logger for.</typeparam>
    /// <returns>A logger instance.</returns>
    public ILogger<T> CreateLogger<T>() => this.LoggerFactory.CreateLogger<T>();

    /// <inheritdoc/>
    public void Dispose()
    {
        this.ActivitySource.Dispose();
        this.Meter.Dispose();
        this.tracerProvider?.Dispose();
        this.meterProvider?.Dispose();
        this.fileTraceProcessor.Dispose();
        this.fileTraceExporter.Dispose();
        this.serviceProvider.Dispose();
    }

    private static ResourceBuilder CreateResourceBuilder() =>
        ResourceBuilder
            .CreateDefault()
            .AddService(ServiceName, serviceVersion: ServiceVersion)
            .AddAttributes(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["service.instance.id"] = Environment.MachineName,
                ["deployment.environment"] = "development",
            });

    private static TracerProvider BuildTracerProvider(
        string sourceName,
        ResourceBuilder resourceBuilder,
        ExporterConfig config,
        SimpleActivityExportProcessor fileProcessor)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(sourceName)
            .AddSource("SpamFilterAgent")
            .AddSource("HemSoft.PowerAI.GraphApi")
            .AddSource("HemSoft.PowerAI.LLM")
            .AddSource("HemSoft.PowerAI.FunctionCalls")
            .AddSource("*Microsoft.Agents.AI")
            .AddHttpClientInstrumentation()
            .AddProcessor(fileProcessor);

        if (config.HasOtlpEndpoint)
        {
            _ = builder.AddOtlpExporter(options => options.Endpoint = new Uri(config.OtlpEndpoint!));
        }

        if (config.EnableConsoleExporter)
        {
            _ = builder.AddConsoleExporter();
        }

        return builder.Build();
    }

    private static MeterProvider BuildMeterProvider(string sourceName, ResourceBuilder resourceBuilder, ExporterConfig config)
    {
        var builder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(sourceName)
            .AddMeter("*Microsoft.Agents.AI")
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        if (config.HasOtlpEndpoint)
        {
            _ = builder.AddOtlpExporter(options => options.Endpoint = new Uri(config.OtlpEndpoint!));
        }

        if (config.EnableConsoleExporter)
        {
            _ = builder.AddConsoleExporter();
        }

        return builder.Build();
    }

    private static ServiceProvider BuildLoggingServiceProvider(ResourceBuilder resourceBuilder, ExporterConfig config)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(logging =>
        {
            _ = logging.SetMinimumLevel(LogLevel.Debug);
            _ = logging.AddOpenTelemetry(options =>
            {
                _ = options.SetResourceBuilder(resourceBuilder);
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;

                if (config.HasOtlpEndpoint)
                {
                    _ = options.AddOtlpExporter(otlpOptions => otlpOptions.Endpoint = new Uri(config.OtlpEndpoint!));
                }

                if (config.EnableConsoleExporter)
                {
                    _ = options.AddConsoleExporter();
                }
            });
        });

        return services.BuildServiceProvider();
    }

    private static void LogConfiguration(ILogger logger, ExporterConfig config)
    {
        LogTraceFileExportConfigured(logger, TraceDirectory, TraceRetentionDays);

        if (config.HasOtlpEndpoint)
        {
            LogOtlpConfigured(logger, config.OtlpEndpoint!);
        }

        if (config.EnableConsoleExporter)
        {
            LogConsoleExporterConfigured(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenTelemetry configured with OTLP endpoint: {Endpoint}")]
    private static partial void LogOtlpConfigured(ILogger logger, string endpoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "OpenTelemetry console exporter enabled for traces, metrics, and logs")]
    private static partial void LogConsoleExporterConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Traces exported to files in {Directory} (retention: {Days} days)")]
    private static partial void LogTraceFileExportConfigured(ILogger logger, string directory, int days);

    private sealed record ExporterConfig(string? OtlpEndpoint, bool EnableConsoleExporter)
    {
        public bool HasOtlpEndpoint => !string.IsNullOrWhiteSpace(this.OtlpEndpoint);

        public static ExporterConfig FromEnvironment()
        {
            var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
            var enableConsole = string.Equals(
                Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            return new ExporterConfig(otlpEndpoint, enableConsole);
        }
    }
}
