// CrawlerCore/Retry/AdaptiveRetryStrategy.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CrawlerCore.Retry
{
    /// <summary>
    /// 智能重试策略
    /// </summary>
    public class AdaptiveRetryStrategy(ILogger<AdaptiveRetryStrategy>? logger)
    {
        private readonly ILogger<AdaptiveRetryStrategy> _logger = logger ?? new Logger<AdaptiveRetryStrategy>(new LoggerFactory());
        private readonly Dictionary<string, DomainRetryInfo> _domainRetryInfo = [];

        public async Task<bool> ShouldRetryAsync(string domain, Exception exception, int currentRetryCount)
        {
            if (!_domainRetryInfo.TryGetValue(domain, out DomainRetryInfo? retryInfo))
            {
                retryInfo = new DomainRetryInfo();
                _domainRetryInfo[domain] = retryInfo;
            }

            retryInfo.LastError = DateTime.UtcNow;
            retryInfo.ConsecutiveErrors++;

            // 基于错误类型和频率决定是否重试
            var shouldRetry = EvaluateRetry(exception, currentRetryCount, retryInfo);

            if (shouldRetry)
            {
                var delay = CalculateRetryDelay(currentRetryCount, retryInfo);
                _logger.LogDebug("Retrying {Domain} after {Delay}ms (attempt {Attempt})", 
                    domain, delay, currentRetryCount + 1);
                
                await Task.Delay(delay);
            }
            else
            {
                _logger.LogWarning("Giving up on {Domain} after {Attempts} attempts", 
                    domain, currentRetryCount);
            }

            return shouldRetry;
        }

        private static bool EvaluateRetry(Exception exception, int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 最大重试次数
            if (currentRetryCount >= 3)
                return false;

            // 基于错误类型决定
            return exception switch
            {
                System.Net.Http.HttpRequestException httpEx => httpEx.Message.Contains("429") || httpEx.Message.Contains("5xx"),
                System.Net.WebException webEx => webEx.Status == System.Net.WebExceptionStatus.Timeout,
                TaskCanceledException => true,// 超时通常可以重试
                _ => currentRetryCount < 2,// 其他错误最多重试2次
            };
        }

        private static int CalculateRetryDelay(int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 指数退避 + 抖动
            var baseDelay = Math.Pow(2, currentRetryCount) * 1000; // 1s, 2s, 4s...
            var jitter = new Random().Next(0, 500); // 最多500ms抖动
            
            // 基于连续错误次数增加延迟
            var multiplier = Math.Min(10, 1 + (retryInfo.ConsecutiveErrors * 0.5));
            
            return (int)(baseDelay * multiplier) + jitter;
        }

        public void RecordSuccess(string domain)
        {
            if (_domainRetryInfo.TryGetValue(domain, out DomainRetryInfo? value))
            {
                value.ConsecutiveErrors = 0;
                value.LastSuccess = DateTime.UtcNow;
            }
        }

        private class DomainRetryInfo
        {
            public int ConsecutiveErrors { get; set; }
            public DateTime LastError { get; set; }
            public DateTime LastSuccess { get; set; }
        }
    }
}