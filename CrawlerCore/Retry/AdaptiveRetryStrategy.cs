// <copyright file="AdaptiveRetryStrategy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Retry
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;

        /// <summary>
        /// 电路状态枚举.
        /// </summary>
    public enum CircuitState
        {
            /// <summary>
            /// 关闭状态，允许请求.
            /// </summary>
            Closed,

            /// <summary>
            /// 开启状态，拒绝请求.
            /// </summary>
            Open,

            /// <summary>
            /// 半开状态，允许部分请求测试.
            /// </summary>
            HalfOpen,
        }

    /// <summary>
    /// 智能重试策略.
    /// </summary>
    public class AdaptiveRetryStrategy : ICrawlerComponent
    {
        /// <summary>
        /// 日志记录器实例.
        /// </summary>
        private readonly ILogger<AdaptiveRetryStrategy> logger;

        /// <summary>
        /// 域名重试信息字典，键为域名，值为对应的重试信息.
        /// </summary>
        private readonly Dictionary<string, DomainRetryInfo> domainRetryInfo = [];

        /// <summary>
        /// 用于保护域名重试信息的线程锁，确保多线程访问时的数据一致性.
        /// </summary>
        private readonly Lock @lock = new ();

        /// <summary>
        /// 基础最大重试次数.
        /// </summary>
        private readonly int baseMaxRetries;

        /// <summary>
        /// 表示重试策略是否已初始化.
        /// </summary>
        private bool isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdaptiveRetryStrategy"/> class.
        /// 初始化 <see cref="AdaptiveRetryStrategy"/> 类的新实例.
        /// </summary>
        /// <param name="logger">日志记录器实例.</param>
        /// <param name="baseMaxRetries">基础最大重试次数，默认值为3.</param>
        public AdaptiveRetryStrategy(ILogger<AdaptiveRetryStrategy>? logger, int baseMaxRetries = 3)
        {
            this.logger = logger ?? new Logger<AdaptiveRetryStrategy>(new LoggerFactory());
            this.baseMaxRetries = baseMaxRetries;
        }

        /// <summary>
        /// 异步决定是否应该重试请求.
        /// </summary>
        /// <param name="domain">请求的域名.</param>
        /// <param name="exception">发生的异常.</param>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
        public async Task<bool> ShouldRetryAsync(string domain, Exception exception, int currentRetryCount)
        {
            DomainRetryInfo? retryInfo;

            lock (this.@lock)
            {
                if (!this.domainRetryInfo.TryGetValue(domain, out retryInfo))
                {
                    retryInfo = new DomainRetryInfo();
                    this.domainRetryInfo[domain] = retryInfo;
                }

                // 检查电路状态
                var circuitState = this.CheckCircuitState(retryInfo);
                if (circuitState == CircuitState.Open)
                {
                    this.logger.LogWarning(
                        "Circuit breaker is open for {Domain}, skipping retry attempt (current: {Attempt}, consecutive errors: {Errors})",
                        domain,
                        currentRetryCount,
                        retryInfo.ConsecutiveErrors);
                    return false;
                }

                // 如果是半开状态，记录测试尝试
                if (circuitState == CircuitState.HalfOpen)
                {
                    retryInfo.HalfOpenAttempts++;
                }

                retryInfo.LastError = DateTime.UtcNow;
                retryInfo.ConsecutiveErrors++;
                retryInfo.TotalErrors++;
                retryInfo.RecordError(exception.GetType().Name); // 记录错误类型

                // 更新电路状态
                this.UpdateCircuitState(retryInfo);
            }

            // 基于错误类型和频率决定是否重试
            var shouldRetry = this.EvaluateRetry(exception, currentRetryCount, retryInfo);

            if (shouldRetry)
            {
                var delay = CalculateRetryDelay(currentRetryCount, retryInfo);
                this.logger.LogDebug(
                    "Retrying {Domain} after {Delay}ms (attempt {Attempt}, consecutive errors: {Errors}, error type: {ErrorType}, circuit state: {CircuitState})",
                    domain,
                    delay,
                    currentRetryCount + 1,
                    retryInfo.ConsecutiveErrors,
                    exception.GetType().Name,
                    retryInfo.CircuitState);

                await Task.Delay(delay);
            }
            else
            {
                this.logger.LogWarning(
                    "Giving up on {Domain} after {Attempts} attempts (consecutive errors: {Errors}, error type: {ErrorType}, circuit state: {CircuitState})",
                    domain,
                    currentRetryCount,
                    retryInfo.ConsecutiveErrors,
                    exception.GetType().Name,
                    retryInfo.CircuitState);
            }

            return shouldRetry;
        }

        /// <summary>
        /// 记录域名请求成功，用于重置连续错误计数和更新统计信息.
        /// </summary>
        /// <param name="domain">请求的域名.</param>
        public void RecordSuccess(string domain)
        {
            lock (this.@lock)
            {
                if (this.domainRetryInfo.TryGetValue(domain, out DomainRetryInfo? value))
                {
                    value.ConsecutiveErrors = 0;
                    value.LastSuccess = DateTime.UtcNow;
                    value.TotalSuccess++;

                    // 处理电路状态
                    if (value.CircuitState == CircuitState.HalfOpen)
                    {
                        value.HalfOpenSuccesses++;

                        // 半开状态下，如果成功次数达到阈值，关闭电路
                        if (value.HalfOpenSuccesses >= 2) // 连续2次成功关闭电路
                        {
                            value.CircuitState = CircuitState.Closed;
                            value.HalfOpenAttempts = 0;
                            value.HalfOpenSuccesses = 0;
                            this.logger.LogInformation("Circuit breaker closed for {Domain} after successful test requests", domain);
                        }
                    }
                    else if (value.CircuitState == CircuitState.Open)
                    {
                        // 如果电路处于打开状态但请求成功，直接关闭电路
                        value.CircuitState = CircuitState.Closed;
                        value.HalfOpenAttempts = 0;
                        value.HalfOpenSuccesses = 0;
                        this.logger.LogInformation("Circuit breaker closed for {Domain} after unexpected successful request", domain);
                    }
                }
            }

            this.logger.LogDebug("Success recorded for {Domain}, consecutive errors reset", domain);
        }

        /// <summary>
        /// 获取域名的重试统计信息（用于监控和调试）.
        /// </summary>
        /// <param name="domain">请求的域名.</param>
        /// <returns>域名的重试统计信息，如果不存在则返回null.</returns>
        public DomainRetryInfo? GetDomainStats(string domain)
        {
            lock (this.@lock)
            {
                this.domainRetryInfo.TryGetValue(domain, out var retryInfo);
                return retryInfo;
            }
        }

        /// <summary>
        /// 重置域名的重试统计信息（用于手动恢复）.
        /// </summary>
        /// <param name="domain">请求的域名.</param>
        public void ResetDomainStats(string domain)
        {
            lock (this.@lock)
            {
                if (this.domainRetryInfo.ContainsKey(domain))
                {
                    this.domainRetryInfo[domain] = new DomainRetryInfo();
                    this.logger.LogInformation("Statistics reset for domain: {Domain}", domain);
                }
            }
        }

        /// <inheritdoc/>
        /// <summary>
        /// 初始化异步.
        /// </summary>
        public Task InitializeAsync()
        {
            if (!this.isInitialized)
            {
                this.isInitialized = true;
                this.logger.LogDebug("AdaptiveRetryStrategy initialized successfully with base max retries: {BaseMaxRetries}", this.baseMaxRetries);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        /// <summary>
        /// 关闭异步.
        /// </summary>
        public Task ShutdownAsync()
        {
            if (this.isInitialized)
            {
                this.isInitialized = false;

                // 清理资源
                lock (this.@lock)
                {
                    this.domainRetryInfo.Clear();
                }

                this.logger.LogDebug("AdaptiveRetryStrategy shutdown successfully");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 计算重试延迟.
        /// </summary>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>计算得到的重试延迟时间（毫秒）.</returns>
        private static int CalculateRetryDelay(int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 基础指数退避
            var baseDelay = Math.Pow(2, currentRetryCount) * 1000; // 1s, 2s, 4s...
            var jitter = new Random().Next(0, 500); // 最多500ms抖动

            // 基于连续错误次数和总错误次数调整延迟
            var errorMultiplier = 1.0;

            if (retryInfo.ConsecutiveErrors > 5)
            {
                errorMultiplier = 2.0; // 频繁错误，加倍延迟
            }
            else if (retryInfo.ConsecutiveErrors > 2)
            {
                errorMultiplier = 1.5; // 有一定错误，增加延迟
            }

            // 对于限流错误，使用更长的延迟
            if (retryInfo.LastErrorType?.Contains("429") == true)
            {
                errorMultiplier = 3.0;
            }

            return (int)(baseDelay * errorMultiplier) + jitter;
        }

        /// <summary>
        /// 评估HTTP异常.
        /// </summary>
        /// <param name="httpEx">HTTP请求异常.</param>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
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

                _ => EvaluateGenericException(httpEx, currentRetryCount, retryInfo), // 其他HTTP错误
            };
        }

        /// <summary>
        /// 评估Web异常.
        /// </summary>
        /// <param name="webEx">Web异常.</param>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
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

                _ => EvaluateGenericException(webEx, currentRetryCount, retryInfo), // 其他Web异常
            };
        }

        /// <summary>
        /// 评估超时异常.
        /// </summary>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
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
        /// 评估协议错误.
        /// </summary>
        /// <param name="webEx">Web异常.</param>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
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
                    _ => false,
                };
            }

            return false;
        }

        /// <summary>
        /// 评估限流错误.
        /// </summary>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
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
        /// 评估服务器错误.
        /// </summary>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
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
        /// 评估通用异常.
        /// </summary>
        /// <param name="exception">发生的异常.</param>
        /// <param name="currentRetryCount">当前重试次数.</param>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>如果应该重试则返回true，否则返回false.</returns>
        private static bool EvaluateGenericException(Exception exception, int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 记录异常类型以便分析
            // var exceptionType = exception.GetType().Name;

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

        /// <summary>
        /// 评估重试 - 基于错误类型和域名历史数据.
        /// </summary>
        private bool EvaluateRetry(Exception exception, int currentRetryCount, DomainRetryInfo retryInfo)
        {
            // 1. 基础检查：超过最大重试次数
            if (currentRetryCount >= this.GetMaxRetryCount(retryInfo))
            {
                return false;
            }

            // 2. 检查连续错误次数过多
            if (retryInfo.ConsecutiveErrors >= 10) // 连续10次错误，暂停重试
            {
                return false;
            }

            // 3. 检查最近是否有成功记录（冷却期检查）
            if (retryInfo.ConsecutiveErrors > 0 &&
                DateTime.UtcNow - retryInfo.LastSuccess < TimeSpan.FromMinutes(5))
            {
                // 在成功后的5分钟内，如果还有错误，减少重试机会
                if (currentRetryCount >= 1)
                {
                    return false;
                }
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

                _ => EvaluateGenericException(exception, currentRetryCount, retryInfo),
            };

            return shouldRetry;
        }

        /// <summary>
        /// 检查电路状态，处理从Open到HalfOpen的转换.
        /// </summary>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>当前电路状态.</returns>
        private CircuitState CheckCircuitState(DomainRetryInfo retryInfo)
        {
            if (retryInfo.CircuitState == CircuitState.Open)
            {
                // 检查是否已经过了冷却时间（30秒）
                if (DateTime.UtcNow - retryInfo.CircuitOpenTime > TimeSpan.FromSeconds(30))
                {
                    // 进入半开状态，允许部分请求测试
                    retryInfo.CircuitState = CircuitState.HalfOpen;
                    retryInfo.HalfOpenAttempts = 0;
                    retryInfo.HalfOpenSuccesses = 0;
                    this.logger.LogInformation("Circuit breaker moved to HalfOpen state for domain");
                }
            }

            return retryInfo.CircuitState;
        }

        /// <summary>
        /// 更新电路状态，处理从Closed到Open的转换.
        /// </summary>
        /// <param name="retryInfo">域名重试信息.</param>
        private void UpdateCircuitState(DomainRetryInfo retryInfo)
        {
            // 如果连续错误达到10次，打开电路
            if (retryInfo.ConsecutiveErrors >= 10 && retryInfo.CircuitState == CircuitState.Closed)
            {
                retryInfo.CircuitState = CircuitState.Open;
                retryInfo.CircuitOpenTime = DateTime.UtcNow;
                this.logger.LogWarning("Circuit breaker opened for domain due to {ConsecutiveErrors} consecutive errors", retryInfo.ConsecutiveErrors);
            }

            // 如果处于半开状态，并且测试失败次数过多，重新打开电路
            else if (retryInfo.CircuitState == CircuitState.HalfOpen && retryInfo.HalfOpenAttempts >= 3)
            {
                retryInfo.CircuitState = CircuitState.Open;
                retryInfo.CircuitOpenTime = DateTime.UtcNow;
                retryInfo.HalfOpenAttempts = 0;
                retryInfo.HalfOpenSuccesses = 0;
                this.logger.LogWarning("Circuit breaker reopened for domain after failed tests in HalfOpen state");
            }
        }

        /// <summary>
        /// 根据域名状态获取最大重试次数.
        /// </summary>
        /// <param name="retryInfo">域名重试信息.</param>
        /// <returns>计算得到的最大重试次数.</returns>
        private int GetMaxRetryCount(DomainRetryInfo retryInfo)
        {
            // 基础最大重试次数
            // var baseMaxRetries = 3;

            // 根据连续错误次数动态调整
            if (retryInfo.ConsecutiveErrors > 5)
            {
                return 1; // 频繁错误，只重试1次
            }
            else if (retryInfo.ConsecutiveErrors > 2)
            {
                return 2; // 有一定错误，重试2次
            }

            return this.baseMaxRetries;
        }

        /// <summary>
        /// 域名重试信息.
        /// </summary>
        public class DomainRetryInfo
        {
            /// <summary>
            /// Gets or sets 获取或设置最近连续发生的错误次数.
            /// </summary>
            public int ConsecutiveErrors { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置错误请求的总次数.
            /// </summary>
            public int TotalErrors { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置成功请求的总次数.
            /// </summary>
            public int TotalSuccess { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置最近发生错误的时间.
            /// </summary>
            public DateTime LastError { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置最近成功请求的时间.
            /// </summary>
            public DateTime LastSuccess { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置最近发生的错误类型.
            /// </summary>
            public string? LastErrorType { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置错误类型的统计信息，键为错误类型名称，值为该错误类型发生的次数.
            /// </summary>
            public Dictionary<string, int> ErrorTypeCounts { get; set; } = [];

            /// <summary>
            /// Gets 获取当前域名的成功率，计算方式为成功次数/(成功次数+错误次数)，如果没有记录则返回1.0.
            /// </summary>
            public double SuccessRate => this.TotalSuccess + this.TotalErrors > 0
                ? (double)this.TotalSuccess / (this.TotalSuccess + this.TotalErrors)
                : 1.0;

            /// <summary>
            /// Gets or sets 获取或设置电路状态.
            /// </summary>
            public CircuitState CircuitState { get; set; } = CircuitState.Closed;

            /// <summary>
            /// Gets or sets 获取或设置电路开启的时间.
            /// </summary>
            public DateTime CircuitOpenTime { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置半开状态下已测试的请求数.
            /// </summary>
            public int HalfOpenAttempts { get; set; }

            /// <summary>
            /// Gets or sets 获取或设置半开状态下成功的请求数.
            /// </summary>
            public int HalfOpenSuccesses { get; set; }

            /// <summary>
            /// 记录错误.
            /// </summary>
            /// <param name="errorType">错误类型.</param>
            public void RecordError(string errorType)
            {
                this.LastErrorType = errorType;
                if (this.ErrorTypeCounts.TryGetValue(errorType, out int value))
                {
                    this.ErrorTypeCounts[errorType] = ++value;
                }
                else
                {
                    this.ErrorTypeCounts[errorType] = 1;
                }
            }
        }
    }
}