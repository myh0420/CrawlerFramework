// CrawlerCore/Retry/AdaptiveRetryStrategy.cs
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;

namespace CrawlerCore.Retry
{
    /// <summary>
    /// 智能重试策略
    /// </summary>
    public class AdaptiveRetryStrategy(ILogger<AdaptiveRetryStrategy>? logger, int baseMaxRetries = 3) : ICrawlerComponent
    {
        private readonly ILogger<AdaptiveRetryStrategy> _logger = logger ?? new Logger<AdaptiveRetryStrategy>(new LoggerFactory());
        private readonly Dictionary<string, DomainRetryInfo> _domainRetryInfo = [];
        private readonly Lock _lock = new();
        private readonly int _baseMaxRetries = baseMaxRetries;
        private bool _isInitialized = false;

        public async Task<bool> ShouldRetryAsync(string domain, Exception exception, int currentRetryCount)
        {
            DomainRetryInfo? retryInfo;

            lock (_lock)
            {
                if (!_domainRetryInfo.TryGetValue(domain, out retryInfo))
                {
                retryInfo = new DomainRetryInfo();
                _domainRetryInfo[domain] = retryInfo;
            }

            retryInfo.LastError = DateTime.UtcNow;
            retryInfo.ConsecutiveErrors++;
                retryInfo.TotalErrors++;
                retryInfo.RecordError(exception.GetType().Name); // 记录错误类型
            }

            // 基于错误类型和频率决定是否重试
            var shouldRetry = EvaluateRetry(exception, currentRetryCount, retryInfo);

            if (shouldRetry)
            {
                var delay = CalculateRetryDelay(currentRetryCount, retryInfo);
                _logger.LogDebug("Retrying {Domain} after {Delay}ms (attempt {Attempt}, consecutive errors: {Errors}, error type: {ErrorType})",
                    domain, delay, currentRetryCount + 1, retryInfo.ConsecutiveErrors, exception.GetType().Name);
                
                await Task.Delay(delay);
            }
            else
            {
                _logger.LogWarning("Giving up on {Domain} after {Attempts} attempts (consecutive errors: {Errors}, error type: {ErrorType})",
                    domain, currentRetryCount, retryInfo.ConsecutiveErrors, exception.GetType().Name);
            }

            return shouldRetry;
        }

        /// <summary>
        /// 评估重试 - 基于错误类型和域名历史数据
        /// </summary>
        private bool EvaluateRetry(Exception exception, int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 1. 基础检查：超过最大重试次数
            if (currentRetryCount >= GetMaxRetryCount(retryInfo))
                return false;

            // 2. 检查连续错误次数过多
            if (retryInfo.ConsecutiveErrors >= 10) // 连续10次错误，暂停重试
                return false;

            // 3. 检查最近是否有成功记录（冷却期检查）
            if (retryInfo.ConsecutiveErrors > 0 &&
                DateTime.UtcNow - retryInfo.LastSuccess < TimeSpan.FromMinutes(5))
            {
                // 在成功后的5分钟内，如果还有错误，减少重试机会
                if (currentRetryCount >= 1)
                    return false;
            }

            // 4. 基于错误类型的智能决策
            var shouldRetry = exception switch
            {
                System.Net.Http.HttpRequestException httpEx =>
                    EvaluateHttpException(httpEx, currentRetryCount, retryInfo),

                System.Net.WebException webEx =>
                    EvaluateWebException(webEx, currentRetryCount, retryInfo),

                TaskCanceledException =>
                    EvaluateTimeoutException(currentRetryCount, retryInfo),

                System.TimeoutException =>
                    EvaluateTimeoutException(currentRetryCount, retryInfo),

                _ => EvaluateGenericException(exception, currentRetryCount, retryInfo)
            };

            return shouldRetry;
        }

        /// <summary>
        /// 计算重试延迟
        /// </summary>
        private static int CalculateRetryDelay(int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 基础指数退避
            var baseDelay = Math.Pow(2, currentRetryCount) * 1000; // 1s, 2s, 4s...
            var jitter = new Random().Next(0, 500); // 最多500ms抖动
            
            // 基于连续错误次数和总错误次数调整延迟
            var errorMultiplier = 1.0;
            
            if (retryInfo.ConsecutiveErrors > 5)
                errorMultiplier = 2.0; // 频繁错误，加倍延迟
            else if (retryInfo.ConsecutiveErrors > 2)
                errorMultiplier = 1.5; // 有一定错误，增加延迟

            // 对于限流错误，使用更长的延迟
            if (retryInfo.LastErrorType?.Contains("429") == true)
            {
                errorMultiplier = 3.0;
        }

            return (int)(baseDelay * errorMultiplier) + jitter;
        }

        public void RecordSuccess(string domain)
        {
            lock (_lock)
            {
            if (_domainRetryInfo.TryGetValue(domain, out DomainRetryInfo? value))
            {
                value.ConsecutiveErrors = 0;
                value.LastSuccess = DateTime.UtcNow;
                    value.TotalSuccess++;
            }
        }

            _logger.LogDebug("Success recorded for {Domain}, consecutive errors reset", domain);
        }

        /// <summary>
        /// 获取域名统计信息（用于监控和调试）
        /// </summary>
        public DomainRetryInfo? GetDomainStats(string domain)
        {
            lock (_lock)
            {
                _domainRetryInfo.TryGetValue(domain, out var retryInfo);
                return retryInfo;
            }
        }

        /// <summary>
        /// 重置域名统计（用于手动恢复）
        /// </summary>
        public void ResetDomainStats(string domain)
        {
            lock (_lock)
            {
                if (_domainRetryInfo.ContainsKey(domain))
                {
                    _domainRetryInfo[domain] = new DomainRetryInfo();
                    _logger.LogInformation("Statistics reset for domain: {Domain}", domain);
                }
            }
        }

        public class DomainRetryInfo
        {
            public int ConsecutiveErrors { get; set; }
            public int TotalErrors { get; set; }
            public int TotalSuccess { get; set; }
            public DateTime LastError { get; set; }
            public DateTime LastSuccess { get; set; }
            public string? LastErrorType { get; set; }
            public Dictionary<string, int> ErrorTypeCounts { get; set; } = [];

            public double SuccessRate => TotalSuccess + TotalErrors > 0
                ? (double)TotalSuccess / (TotalSuccess + TotalErrors)
                : 1.0;

            public void RecordError(string errorType)
            {
                LastErrorType = errorType;
                if (ErrorTypeCounts.TryGetValue(errorType, out int value))
                    ErrorTypeCounts[errorType] = ++value;
                else
                    ErrorTypeCounts[errorType] = 1;
            }
        }

        /// <summary>
        /// 根据域名状态获取最大重试次数
        /// </summary>
        private int GetMaxRetryCount(DomainRetryInfo retryInfo)
        {
            // 基础最大重试次数
            //var baseMaxRetries = 3;

            // 根据连续错误次数动态调整
            if (retryInfo.ConsecutiveErrors > 5)
                return 1; // 频繁错误，只重试1次
            else if (retryInfo.ConsecutiveErrors > 2)
                return 2; // 有一定错误，重试2次

            return _baseMaxRetries;
        }

        /// <summary>
        /// 评估HTTP异常
        /// </summary>
        private static bool EvaluateHttpException(HttpRequestException httpEx, int currentRetryCount, DomainRetryInfo retryInfo)
        {
            var message = httpEx.Message.ToLowerInvariant();

            return message switch
            {
                string msg when msg.Contains("429") =>
                    EvaluateRateLimit(currentRetryCount, retryInfo), // 限流

                string msg when msg.Contains("5xx") =>
                    EvaluateServerError(currentRetryCount, retryInfo), // 服务器错误

                string msg when msg.Contains("408") =>
                    EvaluateTimeoutException(currentRetryCount, retryInfo), // 请求超时

                string msg when msg.Contains("502") || msg.Contains("503") || msg.Contains("504") =>
                    true, // 网关错误，通常可以重试

                string msg when msg.Contains("401") || msg.Contains("403") =>
                    false, // 认证错误，重试无意义

                _ => EvaluateGenericException(httpEx, currentRetryCount, retryInfo) // 其他HTTP错误
            };
        }

        /// <summary>
        /// 评估Web异常
        /// </summary>
        private static bool EvaluateWebException(WebException webEx, int currentRetryCount, DomainRetryInfo retryInfo)
        {
            return webEx.Status switch
            {
                WebExceptionStatus.Timeout =>
                    EvaluateTimeoutException(currentRetryCount, retryInfo),

                WebExceptionStatus.ConnectFailure =>
                    currentRetryCount < 2, // 连接失败可以重试

                WebExceptionStatus.NameResolutionFailure =>
                    false, // DNS解析失败，重试无意义

                WebExceptionStatus.ProtocolError =>
                    EvaluateProtocolError(webEx, currentRetryCount),

                _ => EvaluateGenericException(webEx, currentRetryCount, retryInfo) // 其他Web异常
            };
        }

        /// <summary>
        /// 评估超时异常
        /// </summary>
        private static bool EvaluateTimeoutException(int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 超时错误可以重试，但连续超时需要谨慎
            if (retryInfo.ConsecutiveErrors > 3)
            {
                // 频繁超时，减少重试次数
                return currentRetryCount < 1;
            }

            return currentRetryCount < 3; // 正常情况最多重试3次
        }

        /// <summary>
        /// 评估协议错误
        /// </summary>
        private static bool EvaluateProtocolError(WebException webEx, int currentRetryCount)
        {
            if (webEx.Response is HttpWebResponse response)
            {
                return response.StatusCode switch
                {
                    HttpStatusCode.BadGateway => currentRetryCount < 3, // 502，最多重试3次
                    HttpStatusCode.ServiceUnavailable => currentRetryCount < 2, // 503，最多重试2次
                    HttpStatusCode.GatewayTimeout => currentRetryCount < 2, // 504，最多重试2次
                    HttpStatusCode.InternalServerError => currentRetryCount < 1, // 500，最多重试1次
                    _ => false
                };
            }
            return false;
        }

        /// <summary>
        /// 评估限流错误
        /// </summary>
        private static bool EvaluateRateLimit(int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 限流错误：使用指数退避，但限制最大重试次数
            var maxRetries = 2;

            // 如果该域名频繁被限流，减少重试次数
            if (retryInfo.ConsecutiveErrors > 3)
            {
                maxRetries = 1;
            }

            return currentRetryCount < maxRetries;
        }

        /// <summary>
        /// 评估服务器错误
        /// </summary>
        private static bool EvaluateServerError(int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 服务器错误：可以重试，但需要谨慎
            if (retryInfo.ConsecutiveErrors > 5)
            {
                return currentRetryCount < 1; // 频繁服务器错误，只重试1次
            }

            return currentRetryCount < 2;
        }

        /// <summary>
        /// 评估通用异常
        /// </summary>
        private static bool EvaluateGenericException(Exception exception, int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 记录异常类型以便分析
            //var exceptionType = exception.GetType().Name;

            // 对于特定类型的未知异常，可以有不同的策略
            if (exception is System.IO.IOException || exception is System.Net.Sockets.SocketException)
            {
                // I/O和网络相关异常可以重试
                return currentRetryCount < 2;
            }

            if (exception is System.Security.SecurityException || exception is UnauthorizedAccessException)
            {
                // 安全相关异常，重试无意义
                return false;
            }

            // 对于其他未知异常，根据域名历史决定是否重试
            if (retryInfo.ConsecutiveErrors > 5)
            {
                // 该域名频繁出错，减少重试机会
                return currentRetryCount < 1;
            }

            // 新域名或错误较少的域名，给更多重试机会
            return currentRetryCount < 2;
        }

        public Task InitializeAsync()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _logger.LogDebug("AdaptiveRetryStrategy initialized successfully with base max retries: {BaseMaxRetries}", _baseMaxRetries);
            }
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            if (_isInitialized)
            {
                _isInitialized = false;
                // 清理资源
                lock (_lock)
                {
                    _domainRetryInfo.Clear();
                }
                _logger.LogDebug("AdaptiveRetryStrategy shutdown successfully");
            }
            return Task.CompletedTask;
        }
    }
}