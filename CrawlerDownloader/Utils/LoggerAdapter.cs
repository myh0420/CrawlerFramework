// CrawlerDownloader/Utils/LoggerAdapter.cs
using Microsoft.Extensions.Logging;
using System;

namespace CrawlerDownloader.Utils
{
    /// <summary>
    /// ILogger 适配器，将泛型 ILogger<T> 转换为非泛型 ILogger
    /// </summary>
    public class LoggerAdapter(ILogger logger) : ILogger
    {
        private readonly ILogger _logger = logger;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _logger?.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger?.IsEnabled(logLevel) ?? false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logger?.Log(logLevel, eventId, state, exception, formatter);
        }
    }

    /// <summary>
    /// 泛型版本的 LoggerAdapter
    /// </summary>
    public class LoggerAdapter<T>(ILogger<T> logger) : ILogger
    {
        private readonly ILogger<T> _logger = logger;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _logger?.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger?.IsEnabled(logLevel) ?? false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _logger?.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}