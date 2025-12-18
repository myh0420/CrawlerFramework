// <copyright file="LoggerAdapter{T}.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerDownloader.Utils
{
    using System;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 泛型版本的 LoggerAdapter.
    /// </summary>
    /// <typeparam name="T">日志记录器的类型参数.</typeparam>
    /// <param name="logger">要封装的 ILogger T 实例.</param>
    public class LoggerAdapter<T>(ILogger<T> logger) : ILogger
    {
        /// <summary>
        /// 内部 ILogger T 实例.
        /// </summary>
        private readonly ILogger<T> logger = logger;

        /// <summary>
        /// 开始一个日志范围.
        /// </summary>
        /// <typeparam name="TState">范围状态的类型.</typeparam>
        /// <param name="state">范围状态.</param>
        /// <returns>IDisposable 对象，用于结束范围.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return this.logger?.BeginScope(state);
        }

        /// <summary>
        /// 检查指定日志级别是否已启用.
        /// </summary>
        /// <param name="logLevel">要检查的日志级别.</param>
        /// <returns>如果已启用，则为 true；否则为 false.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return this.logger?.IsEnabled(logLevel) ?? false;
        }

        /// <summary>
        /// 记录日志消息.
        /// </summary>
        /// <typeparam name="TState">日志状态的类型.</typeparam>
        /// <param name="logLevel">日志级别.</param>
        /// <param name="eventId">事件ID.</param>
        /// <param name="state">日志状态.</param>
        /// <param name="exception">关联的异常（可选）.</param>
        /// <param name="formatter">用于格式化日志消息的函数.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            this.logger?.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}