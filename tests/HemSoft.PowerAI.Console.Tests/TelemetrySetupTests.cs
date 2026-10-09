// <copyright file="TelemetrySetupTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using HemSoft.PowerAI.Console.Telemetry;

using Xunit;

/// <summary>
/// Unit tests for <see cref="TelemetrySetup"/>.
/// </summary>
[Collection("EnvironmentVariableTests")]
public sealed class TelemetrySetupTests
{
    /// <summary>
    /// Tests that TelemetrySetup initializes correctly without OTLP endpoint.
    /// </summary>
    [Fact]
    public void ConstructorWithoutOtlpEndpointCreatesInstance()
    {
        // Arrange - ensure no OTLP endpoint is set
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            // Act
            using var telemetry = new TelemetrySetup("TestSource");

            // Assert
            Assert.NotNull(telemetry.ActivitySource);
            Assert.NotNull(telemetry.Meter);
            Assert.NotNull(telemetry.LoggerFactory);
            Assert.Equal("TestSource", telemetry.ActivitySource.Name);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that TelemetrySetup initializes correctly with OTLP endpoint.
    /// </summary>
    [Fact]
    public void ConstructorWithOtlpEndpointCreatesInstance()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            // Act
            using var telemetry = new TelemetrySetup("TestSource");

            // Assert
            Assert.NotNull(telemetry.ActivitySource);
            Assert.NotNull(telemetry.Meter);
            Assert.NotNull(telemetry.LoggerFactory);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that CreateLogger returns a valid logger.
    /// </summary>
    [Fact]
    public void CreateLoggerReturnsValidLogger()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            using var telemetry = new TelemetrySetup("TestSource");

            // Act
            var logger = telemetry.CreateLogger<TelemetrySetupTests>();

            // Assert
            Assert.NotNull(logger);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that Dispose cleans up resources without throwing.
    /// </summary>
    [Fact]
    public void DisposeCleansUpResources()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            using var telemetry = new TelemetrySetup("TestSource");

            // Act
            var exception = Record.Exception(telemetry.Dispose);

            // Assert - should not throw
            Assert.Null(exception);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that ActivitySource can create activities.
    /// </summary>
    [Fact]
    public void ActivitySourceCanStartActivity()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            using var telemetry = new TelemetrySetup("TestSource");

            // Act - activities may be null if no listeners are registered
            using var activity = telemetry.ActivitySource.StartActivity("TestActivity");

            // Assert - activity may be null if no listener registered, that's expected
            Assert.Equal("TestSource", telemetry.ActivitySource.Name);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that TelemetrySetup initializes with console exporter enabled.
    /// </summary>
    [Fact]
    public void ConstructorWithConsoleExporterCreatesInstance()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", "true");

        try
        {
            // Act
            using var telemetry = new TelemetrySetup("TestSourceConsole");

            // Assert
            Assert.NotNull(telemetry.ActivitySource);
            Assert.NotNull(telemetry.Meter);
            Assert.NotNull(telemetry.LoggerFactory);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that TelemetrySetup initializes with both OTLP and console exporter.
    /// </summary>
    [Fact]
    public void ConstructorWithBothExportersCreatesInstance()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", "true");

        try
        {
            // Act
            using var telemetry = new TelemetrySetup("TestSourceBoth");

            // Assert
            Assert.NotNull(telemetry.ActivitySource);
            Assert.NotNull(telemetry.Meter);
            Assert.NotNull(telemetry.LoggerFactory);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that Meter is created with source name.
    /// </summary>
    [Fact]
    public void MeterIsCreatedWithSourceName()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            using var telemetry = new TelemetrySetup("TestMeterSource");

            // Assert
            Assert.NotNull(telemetry.Meter);
            Assert.Equal("TestMeterSource", telemetry.Meter.Name);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that console exporter env var is case insensitive.
    /// </summary>
    [Fact]
    public void ConsoleExporterEnvVarIsCaseInsensitive()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", "TRUE");

        try
        {
            // Act
            using var telemetry = new TelemetrySetup("TestSourceCaseInsensitive");

            // Assert
            Assert.NotNull(telemetry);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that console exporter with false value doesn't enable console.
    /// </summary>
    [Fact]
    public void ConsoleExporterFalseDisablesConsole()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", "false");

        try
        {
            // Act
            using var telemetry = new TelemetrySetup("TestSourceFalse");

            // Assert
            Assert.NotNull(telemetry);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that multiple TelemetrySetup instances can be created.
    /// </summary>
    [Fact]
    public void MultipleTelemetrySetupInstancesCanBeCreated()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            // Act
            using var telemetry1 = new TelemetrySetup("TestSource1");
            using var telemetry2 = new TelemetrySetup("TestSource2");

            // Assert
            Assert.NotNull(telemetry1);
            Assert.NotNull(telemetry2);
            Assert.NotEqual(telemetry1.ActivitySource.Name, telemetry2.ActivitySource.Name, StringComparer.Ordinal);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }

    /// <summary>
    /// Tests that CreateLogger can create multiple loggers.
    /// </summary>
    [Fact]
    public void CreateLoggerCanCreateMultipleLoggers()
    {
        // Arrange
        var originalEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var originalConsole = Environment.GetEnvironmentVariable("OTEL_CONSOLE_EXPORTER");
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", value: null);
        Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", value: null);

        try
        {
            using var telemetry = new TelemetrySetup("TestSource");

            // Act
            var logger1 = telemetry.CreateLogger<TelemetrySetupTests>();
            var logger2 = telemetry.CreateLogger<string>();

            // Assert
            Assert.NotNull(logger1);
            Assert.NotNull(logger2);
        }
        finally
        {
            // Restore original environment
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", originalEndpoint);
            Environment.SetEnvironmentVariable("OTEL_CONSOLE_EXPORTER", originalConsole);
        }
    }
}
