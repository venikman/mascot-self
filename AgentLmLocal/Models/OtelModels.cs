using System.Text.Json.Serialization;

namespace AgentLmLocal.Models;

/// <summary>
/// Models for OpenTelemetry Protocol (OTLP) trace data from frontend
/// Based on OTLP/HTTP JSON format
/// </summary>
public sealed record OtelTraceRequest
{
    [JsonPropertyName("resourceSpans")]
    public List<ResourceSpans> ResourceSpans { get; init; } = [];
}

public sealed record ResourceSpans
{
    [JsonPropertyName("resource")]
    public Resource? Resource { get; init; }

    [JsonPropertyName("scopeSpans")]
    public List<ScopeSpans> ScopeSpans { get; init; } = [];
}

public sealed record Resource
{
    [JsonPropertyName("attributes")]
    public List<KeyValue> Attributes { get; init; } = [];
}

public sealed record ScopeSpans
{
    [JsonPropertyName("scope")]
    public Scope? Scope { get; init; }

    [JsonPropertyName("spans")]
    public List<Span> Spans { get; init; } = [];
}

public sealed record Scope
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

public sealed record Span
{
    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = string.Empty;

    [JsonPropertyName("spanId")]
    public string SpanId { get; init; } = string.Empty;

    [JsonPropertyName("parentSpanId")]
    public string? ParentSpanId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public int Kind { get; init; }

    [JsonPropertyName("startTimeUnixNano")]
    public string StartTimeUnixNano { get; init; } = string.Empty;

    [JsonPropertyName("endTimeUnixNano")]
    public string EndTimeUnixNano { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public List<KeyValue> Attributes { get; init; } = [];

    [JsonPropertyName("status")]
    public SpanStatus? Status { get; init; }

    [JsonPropertyName("events")]
    public List<SpanEvent> Events { get; init; } = [];
}

public sealed record KeyValue
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public AttributeValue Value { get; init; } = new();
}

public sealed record AttributeValue
{
    [JsonPropertyName("stringValue")]
    public string? StringValue { get; init; }

    [JsonPropertyName("intValue")]
    public long? IntValue { get; init; }

    [JsonPropertyName("doubleValue")]
    public double? DoubleValue { get; init; }

    [JsonPropertyName("boolValue")]
    public bool? BoolValue { get; init; }
}

public sealed record SpanStatus
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed record SpanEvent
{
    [JsonPropertyName("timeUnixNano")]
    public string TimeUnixNano { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public List<KeyValue> Attributes { get; init; } = [];
}
