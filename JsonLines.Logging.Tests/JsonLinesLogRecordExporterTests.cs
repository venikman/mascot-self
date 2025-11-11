using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using JsonLines.Logging;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace JsonLines.Logging.Tests;

public class JsonLinesLogRecordExporterTests
{
    public static TheoryData<LogLevel, string, string, object?[], string, Dictionary<string, object?>> StructuredLogTestCases => new()
    {
        {
            LogLevel.Warning,
            "OrderWorkflow",
            "Order {OrderId} total {Total}",
            new object?[] { 42, 19.99 },
            "Order 42 total 19.99",
            new Dictionary<string, object?>
            {
                ["OrderId"] = 42,
                ["Total"] = 19.99
            }
        },
        {
            LogLevel.Information,
            "Planner",
            "Planner {Name} completed step {Step}",
            new object?[] { "alpha", 3 },
            "Planner alpha completed step 3",
            new Dictionary<string, object?>
            {
                ["Name"] = "alpha",
                ["Step"] = 3
            }
        }
    };

    [Theory]
    [MemberData(nameof(StructuredLogTestCases))]
    public void StructuredLog_WritesSingleJsonLine(
        LogLevel logLevel,
        string category,
        string messageTemplate,
        object?[] templateValues,
        string expectedMessage,
        Dictionary<string, object?> expectedAttributes)
    {
        var writer = new StringWriter();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                options.AddJsonLinesExporter(o =>
                {
                    o.Writer = writer;
                });
            });
        });

        using var activity = new Activity("test-log");
        activity.Start();

        var logger = loggerFactory.CreateLogger(category);
        logger.Log(logLevel, messageTemplate, templateValues);

        activity.Stop();

        var lines = writer.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var json = Assert.Single(lines);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(logLevel.ToString(), root.GetProperty("severity").GetString());
        Assert.Equal(category, root.GetProperty("category").GetString());
        Assert.Equal(expectedMessage, root.GetProperty("message").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("spanId").GetString()));

        var attributes = root.GetProperty("attributes");
        foreach (var kvp in expectedAttributes)
        {
            Assert.True(attributes.TryGetProperty(kvp.Key, out var element), $"Missing attribute '{kvp.Key}'");
            Assert.Equal(kvp.Value?.ToString(), element.ToString());
        }
    }

    [Fact]
    public void DisposeWriterOption_IsHonored()
    {
        var disposableWriter = new TrackingWriter();

        using (var exporter = new JsonLinesLogRecordExporter(new JsonLinesExporterOptions
        {
            Writer = disposableWriter,
            DisposeWriter = true
        }))
        {
        }

        Assert.True(disposableWriter.Disposed);

        var reusableWriter = new TrackingWriter();

        using (var exporter = new JsonLinesLogRecordExporter(new JsonLinesExporterOptions
        {
            Writer = reusableWriter,
            DisposeWriter = false
        }))
        {
        }

        Assert.False(reusableWriter.Disposed);
    }

    [Fact]
    public void ForceFlush_FlushesWriter()
    {
        var writer = new TrackingWriter();

        using var exporter = new JsonLinesLogRecordExporter(new JsonLinesExporterOptions
        {
            Writer = writer
        });

        exporter.ForceFlush();

        Assert.True(writer.Flushed);
    }

    [Fact]
    public void AddJsonLinesExporter_GuardsAgainstNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            JsonLinesLoggerOptionsExtensions.AddJsonLinesExporter(null!);
        });
    }

    private sealed class TrackingWriter : StringWriter
    {
        public bool Disposed { get; private set; }
        public bool Flushed { get; private set; }

        public override void Flush()
        {
            Flushed = true;
            base.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
