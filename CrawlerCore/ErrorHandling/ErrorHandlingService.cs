// CrawlerCore/ErrorHandling/ErrorHandlingService.cs
using CrawlerCore.Exceptions;
using CrawlerEntity.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CrawlerCore.ErrorHandling
{
    /// <summary>
    /// 错误处理服务接口
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// 处理异常并返回下载结果
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="ex">异常对象</param>
        /// <returns>下载结果</returns>
        DownloadResult HandleDownloadException(string url, Exception ex);

        /// <summary>
        /// 处理异常并返回解析结果
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="ex">异常对象</param>
        /// <returns>解析结果</returns>
        ParseResult HandleParseException(string url, Exception ex);

        /// <summary>
        /// 处理通用异常
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">上下文信息</param>
        void HandleException(Exception ex, string context = "");

        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">上下文信息</param>
        void LogError(Exception ex, string context = "");
    }

    /// <summary>
    /// 错误处理服务实现
    /// </summary>
    public class ErrorHandlingService(ILogger<ErrorHandlingService> logger) : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger = logger;

        /// <summary>
        /// 处理异常并返回下载结果
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="ex">异常对象</param>
        /// <returns>下载结果</returns>
        public DownloadResult HandleDownloadException(string url, Exception ex)
        {
            LogError(ex, $"Downloading {url}");

            var result = new DownloadResult
            {
                Url = url,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                StatusCode = 500
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
        /// 处理异常并返回解析结果
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <param name="ex">异常对象</param>
        /// <returns>解析结果</returns>
        public ParseResult HandleParseException(string url, Exception ex)
        {
            LogError(ex, $"Parsing {url}");

            return new ParseResult
            {
                Url = url,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }

        /// <summary>
        /// 处理通用异常
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">上下文信息</param>
        public void HandleException(Exception ex, string context = "")
        {
            LogError(ex, context);

            // 可以在这里添加更多的错误处理逻辑，比如：
            // 1. 发送错误通知
            // 2. 记录错误到数据库
            // 3. 根据错误类型执行不同的恢复策略
        }

        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="context">上下文信息</param>
        public void LogError(Exception ex, string context = "")
        {
            // 基于异常类型进行智能日志记录
            if (ex is CrawlerException crawlerEx)
            {
                // 使用结构化日志记录爬虫异常，包含错误代码和详情
                _logger.LogError(ex, "{Context} - {ExceptionType} Error [{ErrorCode}]: {Message}", 
                    context, ex.GetType().Name.Replace("Exception", ""), crawlerEx.ErrorCode, crawlerEx.Message);
            }
            else
            {
                // 记录非爬虫自定义异常
                _logger.LogError(ex, "{Context} - Unexpected Error: {Message}", 
                    context, ex.Message);
            }
        }
    }
}
