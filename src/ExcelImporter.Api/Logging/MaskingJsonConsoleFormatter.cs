using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace ExcelImporter.Api.Logging;

/// <summary>Strukturisan (JSON, jedan red po zapisu) izlaz na konzolu u kome su tajne maskirane.</summary>
public sealed class MaskingJsonConsoleFormatter(SecretMasker masker) : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "masked-json";

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
        {
            return;
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("timestamp", DateTimeOffset.UtcNow);
            writer.WriteString("level", logEntry.LogLevel.ToString());
            writer.WriteString("category", logEntry.Category);
            writer.WriteNumber("eventId", logEntry.EventId.Id);
            writer.WriteString("message", masker.Apply(message));
            if (logEntry.Exception is not null)
            {
                writer.WriteString("exception", masker.Apply(logEntry.Exception.ToString()));
            }

            writer.WriteEndObject();
        }

        textWriter.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }
}