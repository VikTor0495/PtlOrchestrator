
using System;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;

namespace PtlOrchestrator.Configuration.Formatter;

public sealed class CustomLogFormatter : ConsoleFormatter
{
    public CustomLogFormatter() : base("custom") { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        if (logEntry.Formatter == null)
            return;

        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (string.IsNullOrWhiteSpace(message))
            return;

        var (levelText, color) = logEntry.LogLevel switch
        {
            LogLevel.Information => ("info:", "\x1b[32m"),   // green
            LogLevel.Warning     => ("warn:", "\x1b[33m"),   // yellow
            LogLevel.Error       => ("err:",  "\x1b[31m"),   // red
            LogLevel.Critical    => ("crit:", "\x1b[31m"),   // red
            LogLevel.Debug       => ("dbg:",  "\x1b[90m"),   // gray
            LogLevel.Trace       => ("trc:",  "\x1b[90m"),
            _                    => ("log:",  "\x1b[37m")
        };

        const string reset = "\x1b[0m";

        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
        const string timeColor = "\x1b[90m"; // gray

        textWriter.WriteLine($"{timeColor}{timestamp}{reset} {color}{levelText}{reset} {message}");
    }
}