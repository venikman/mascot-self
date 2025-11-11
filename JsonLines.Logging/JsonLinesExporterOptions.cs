using System.IO;
using System.Text.Json;

namespace JsonLines.Logging;

/// <summary>
/// Configuration for the <see cref="JsonLinesLogRecordExporter"/>.
/// </summary>
public sealed class JsonLinesExporterOptions
{
    /// <summary>
    /// Gets or sets the <see cref="TextWriter"/> the exporter should write to. Defaults to <see cref="System.Console.Out"/>.
    /// </summary>
    public TextWriter? Writer { get; set; }

    /// <summary>
    /// Gets or sets whether the exporter should dispose the provided writer when disposed itself.
    /// </summary>
    public bool DisposeWriter { get; set; }

    /// <summary>
    /// Gets or sets custom serializer options for the JSON payload.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }
}
