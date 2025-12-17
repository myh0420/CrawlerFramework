// <copyright file="ErrorHandlingService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.ErrorHandling
{
    using System;
    using System.Threading.Tasks;
    using CrawlerCore.Exceptions;
    using CrawlerEntity.Enums;
    using CrawlerEntity.Models;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 错误类型枚举.
    /// </summary>
    public enum ErrorType
    {
        /// <summary>
        /// 网络错误.
        /// </summary>
        Network,

        /// <summary>
        /// 解析错误.
        /// </summary>
        Parse,

        /// <summary>
        /// 反爬错误.
        /// </summary>
        AntiBot,

        /// <summary>
        /// 存储错误.
        /// </summary>
        Storage,

        /// <summary>
        /// 配置错误.
        /// </summary>
        Config,

        /// <summary>
        /// 超时错误.
        /// </summary>
        Timeout,

        /// <summary>
        /// 并发错误.
        /// </summary>
        Concurrency,

        /// <summary>
        /// 其他错误.
        /// </summary>
        Other,
    }

    /// <summary>
    /// 错误处理服务接口.
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// 处理异常并返回下载结果.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="ex">异常对象.</param>
        /// <returns>下载结果.</returns>
        DownloadResult HandleDownloadException(string url, Exception ex);

        /// <summary>
        /// 处理异常并返回解析结果.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="ex">异常对象.</param>
        /// <returns>解析结果.</returns>
        ParseResult HandleParseException(string url, Exception ex);

        /// <summary>
        /// 处理通用异常.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <param name="context">上下文信息.</param>
        void HandleException(Exception ex, string context = "");

        /// <summary>
        /// 记录错误信息.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <param name="context">上下文信息.</param>
        void LogError(Exception ex, string context = "");

        /// <summary>
        /// 获取错误类型.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <returns>错误类型.</returns>
        ErrorType GetErrorType(Exception ex);

        /// <summary>
        /// 判断是否可以自动恢复.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <returns>是否可以自动恢复.</returns>
        bool CanAutoRecover(Exception ex);

        /// <summary>
        /// 获取推荐的重试延迟时间.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <param name="retryCount">当前重试次数.</param>
        /// <returns>推荐的重试延迟时间.</returns>
        TimeSpan GetRecommendedDelay(Exception ex, int retryCount = 0);
    }

    /// <summary>
    /// 错误处理服务实现.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ErrorHandlingService"/> class.
    /// 初始化 <see cref="ErrorHandlingService"/> 类的新实例.
    /// </remarks>
    /// <param name="logger">日志记录器实例.</param>
    public class ErrorHandlingService(ILogger<ErrorHandlingService> logger) : IErrorHandlingService
    {
        /// <summary>
        /// 日志记录器实例，用于记录错误和异常信息.
        /// </summary>
        private readonly ILogger<ErrorHandlingService> logger = logger;

        /// <summary>
        /// 处理异常并返回下载结果.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="ex">异常对象.</param>
        /// <returns>下载结果.</returns>
        public DownloadResult HandleDownloadException(string url, Exception ex)
        {
            this.LogError(ex, $"Downloading {url}");

            var result = new DownloadResult
            {
                Url = url,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                StatusCode = 500,
                ErrorType = this.GetErrorType(ex).ToString(),
            };

            // 根据异常类型设置更详细的错误信息
            if (ex is DownloadException downloadEx)
            {
                result.StatusCode = downloadEx.StatusCode ?? 500;
                result.ErrorMessage = downloadEx.Message;
            }
            else if (ex is AntiBotException antiBotEx)
            {
                result.StatusCode = antiBotEx.IsBlocked ? 403 : 500;
                result.ErrorMessage = antiBotEx.Message;
            }
            else if (ex is TimeoutException)
            {
                result.StatusCode = 408;
                result.ErrorMessage = "Request timed out";
            }
            else if (ex is HttpRequestException httpEx)
            {
                result.StatusCode = (int)(httpEx.StatusCode ?? System.Net.HttpStatusCode.InternalServerError);
            }

            return result;
        }

        /// <summary>
        /// 处理异常并返回解析结果.
        /// </summary>
        /// <param name="url">目标URL.</param>
        /// <param name="ex">异常对象.</param>
        /// <returns>解析结果.</returns>
        public ParseResult HandleParseException(string url, Exception ex)
        {
            this.LogError(ex, $"Parsing {url}");

            return new ParseResult
            {
                Url = url,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                ErrorType = this.GetErrorType(ex).ToString(),
            };
        }

        /// <summary>
        /// 处理通用异常.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <param name="context">上下文信息.</param>
        public void HandleException(Exception ex, string context = "")
        {
            this.LogError(ex, context);

            // 根据错误类型执行不同的处理策略
            var errorType = this.GetErrorType(ex);
            this.logger.LogInformation(
                "{Context} - Error Type: {ErrorType}, CanAutoRecover: {CanAutoRecover}", context, errorType, this.CanAutoRecover(ex));
        }

        /// <summary>
        /// 记录错误信息.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <param name="context">上下文信息.</param>
        public void LogError(Exception ex, string context = "")
        {
            // 基于异常类型进行智能日志记录
            if (ex is CrawlerException crawlerEx)
            {
                var errorType = this.GetErrorType(ex);

                // 使用结构化日志记录爬虫异常，包含错误代码和详情
                this.logger.LogError(ex, "{Context} - {ExceptionType} Error [{ErrorCode}, {ErrorType}]: {Message}", context, ex.GetType().Name.Replace("Exception", string.Empty), crawlerEx.ErrorCode, errorType, crawlerEx.Message);
            }
            else
            {
                var errorType = this.GetErrorType(ex);

                // 记录非爬虫自定义异常
                this.logger.LogError(ex, "{Context} - Unexpected Error [{ErrorType}]: {Message}", context, errorType, ex.Message);
            }
        }

        /// <summary>
        /// 获取错误类型.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <returns>错误类型.</returns>
        public ErrorType GetErrorType(Exception ex)
        {
            return ex switch
            {
                DownloadException => ErrorType.Network,
                ParseException => ErrorType.Parse,
                AntiBotException => ErrorType.AntiBot,
                StorageException => ErrorType.Storage,
                ConfigException => ErrorType.Config,
                TimeoutException => ErrorType.Timeout,
                HttpRequestException => ErrorType.Network,
                OperationCanceledException => ErrorType.Concurrency,
                AggregateException => ErrorType.Concurrency,
                _ => ErrorType.Other,
            };
        }

        /// <summary>
        /// 判断是否可以自动恢复.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <returns>是否可以自动恢复.</returns>
        public bool CanAutoRecover(Exception ex)
        {
            var errorType = this.GetErrorType(ex);
            return errorType switch
            {
                ErrorType.Network or ErrorType.Timeout or ErrorType.AntiBot => true,
                ErrorType.Concurrency => !(ex is AggregateException aggregateEx && aggregateEx.InnerExceptions.Any(e => e is ConfigException)), // 部分并发错误可以恢复
                _ => false,
            };
        }

        /// <summary>
        /// 获取推荐的重试延迟时间.
        /// </summary>
        /// <param name="ex">异常对象.</param>
        /// <param name="retryCount">当前重试次数.</param>
        /// <returns>推荐的重试延迟时间.</returns>
        public TimeSpan GetRecommendedDelay(Exception ex, int retryCount = 0)
        {
            var errorType = this.GetErrorType(ex);
            var baseDelay = errorType switch
            {
                ErrorType.Network => TimeSpan.FromSeconds(2),
                ErrorType.Timeout => TimeSpan.FromSeconds(5),
                ErrorType.AntiBot => TimeSpan.FromSeconds(10),
                ErrorType.Concurrency => TimeSpan.FromSeconds(1),
                _ => TimeSpan.FromSeconds(3),
            };

            // 指数退避策略
            var delay = baseDelay * Math.Pow(1.5, retryCount);

            // 限制最大延迟时间
            return TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, 60000));
        }
    }
}
