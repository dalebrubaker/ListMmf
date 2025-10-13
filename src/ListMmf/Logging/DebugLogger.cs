using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BruSoftware.ListMmf.Logging;

/// <summary>
/// A minimal logger that writes to Debug and Console for development use.
/// </summary>
internal sealed class DebugLogger : ILogger
{
    private readonly string _category;

    public static DebugLogger Create(string category = "BruSoftware.ListMmf") => new DebugLogger(category);

    public DebugLogger(string category)
    {
        _category = category ?? "BruSoftware.ListMmf";
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (formatter == null)
        {
            return;
        }
        var message = formatter(state, exception);
        var line = $"[{DateTimeOffset.Now:O}] {_category} {logLevel}: {message}" + (exception != null ? $"{Environment.NewLine}{exception}" : string.Empty);
        try
        {
            Debug.WriteLine(line);
        }
        catch
        {
            // ignored
        }
        try
        {
            Console.WriteLine(line);
        }
        catch
        {
            // ignored
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

