// <copyright file="SimpleDomainDelayManager.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;

    /// <summary>
    /// 增强的域名延迟管理器，支持动态延迟调整和请求类型感知.
    /// </summary>
    public class SimpleDomainDelayManager : IDomainDelayManager
    {
        /// <summary>
        /// 记录每个域名的最后访问时间.
        /// </summary>
        private readonly ConcurrentDictionary<string, DateTime> lastAccessTimes = [];

        /// <summary>
        /// 记录每个域名的延迟时间.
        /// </summary>
        private readonly ConcurrentDictionary<string, TimeSpan> domainDelays = [];

        /// <summary>
        /// 记录每个域名不同请求类型的延迟时间.
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TimeSpan>> domainRequestTypeDelays = [];

        /// <summary>
        /// 默认延迟时间.
        /// </summary>
        private readonly TimeSpan defaultDelay = TimeSpan.FromMilliseconds(1000);

        /// <summary>
        /// 最小延迟时间.
        /// </summary>
        private readonly TimeSpan minimumDelay = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// 最大延迟时间.
        /// </summary>
        private readonly TimeSpan maximumDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 动态延迟因子，用于增加延迟时的乘法因子.
        /// </summary>
        private readonly double dynamicDelayFactor = 1.2;

        /// <summary>
        /// 恢复因子，用于减少延迟时的乘法因子.
        /// </summary>
        private readonly double recoveryFactor = 0.9;

        /// <summary>
        /// 记录每个域名的最大并发数.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> domainMaxConcurrency = [];

        /// <summary>
        /// 记录每个域名的当前并发数.
        /// </summary>
        private readonly ConcurrentDictionary<string, int> domainCurrentConcurrency = [];

        /// <summary>
        /// 用于控制并发的锁对象字典.
        /// </summary>
        private readonly ConcurrentDictionary<string, object> concurrencyLocks = [];

        /// <summary>
        /// 默认最大并发数.
        /// </summary>
        private readonly int defaultMaxConcurrency = 5;

        /// <summary>
        /// 检查是否可以处理指定域名的请求.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <returns>如果可以处理则返回true，否则返回false.</returns>
        public Task<bool> CanProcessAsync(string domain)
        {
            return this.CanProcessAsync(domain, "default");
        }

        /// <summary>
        /// 检查是否可以处理指定域名和请求类型的请求.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="requestType">请求类型.</param>
        /// <returns>如果可以处理则返回true，否则返回false.</returns>
        public Task<bool> CanProcessAsync(string domain, string requestType)
        {
            if (!this.lastAccessTimes.TryGetValue(domain, out DateTime value))
            {
                return Task.FromResult(true);
            }

            var delay = this.GetDelayForRequestType(domain, requestType);
            var nextAccessTime = value + delay;

            return Task.FromResult(DateTime.UtcNow >= nextAccessTime);
        }

        /// <summary>
        /// 记录域名访问时间.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <returns>异步任务.</returns>
        public Task RecordAccessAsync(string domain)
        {
            return this.RecordAccessAsync(domain, "default");
        }

        /// <summary>
        /// 记录域名和请求类型的访问时间.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="requestType">请求类型.</param>
        /// <returns>异步任务.</returns>
        public Task RecordAccessAsync(string domain, string requestType)
        {
            this.lastAccessTimes[domain] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置域名延迟.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="delay">延迟时间.</param>
        public void SetDelay(string domain, TimeSpan delay)
        {
            var clampedDelay = this.ClampDelay(delay);
            this.domainDelays[domain] = clampedDelay;
        }

        /// <summary>
        /// 设置域名特定请求类型的延迟.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="requestType">请求类型.</param>
        /// <param name="delay">延迟时间.</param>
        public void SetDelay(string domain, string requestType, TimeSpan delay)
        {
            var clampedDelay = this.ClampDelay(delay);
            var requestTypeDelays = this.domainRequestTypeDelays.GetOrAdd(domain, _ => new ConcurrentDictionary<string, TimeSpan>());
            requestTypeDelays[requestType] = clampedDelay;
        }

        /// <summary>
        /// 动态增加域名延迟.
        /// </summary>
        /// <param name="domain">域名.</param>
        public void IncreaseDelay(string domain)
        {
            if (this.domainDelays.TryGetValue(domain, out TimeSpan currentDelay))
            {
                var newDelay = this.ClampDelay(currentDelay * this.dynamicDelayFactor);
                this.domainDelays[domain] = newDelay;
            }
            else
            {
                var newDelay = this.ClampDelay(this.defaultDelay * this.dynamicDelayFactor);
                this.domainDelays[domain] = newDelay;
            }
        }

        /// <summary>
        /// 动态减少域名延迟（恢复）.
        /// </summary>
        /// <param name="domain">域名.</param>
        public void DecreaseDelay(string domain)
        {
            if (this.domainDelays.TryGetValue(domain, out TimeSpan currentDelay))
            {
                var newDelay = this.ClampDelay(currentDelay * this.recoveryFactor);
                this.domainDelays[domain] = newDelay;
            }
        }

        /// <summary>
        /// 设置域名的最大并发数.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="maxConcurrency">最大并发数.</param>
        public void SetMaxConcurrency(string domain, int maxConcurrency)
        {
            if (maxConcurrency <= 0)
            {
                maxConcurrency = 1;
            }

            this.domainMaxConcurrency[domain] = maxConcurrency;
        }

        /// <summary>
        /// 尝试获取域名的并发许可.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <returns>是否获取到许可.</returns>
        public Task<bool> TryAcquireConcurrencyPermitAsync(string domain)
        {
            // 获取域名的锁对象
            var lockObj = this.concurrencyLocks.GetOrAdd(domain, _ => new object());

            lock (lockObj)
            {
                // 获取当前域名的最大并发数
                int maxConcurrency = this.domainMaxConcurrency.GetValueOrDefault(domain, this.defaultMaxConcurrency);

                // 获取当前并发数
                int currentConcurrency = this.domainCurrentConcurrency.GetValueOrDefault(domain, 0);

                // 检查是否可以获取新的许可
                if (currentConcurrency < maxConcurrency)
                {
                    // 增加当前并发数
                    this.domainCurrentConcurrency[domain] = currentConcurrency + 1;
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 释放域名的并发许可.
        /// </summary>
        /// <param name="domain">域名.</param>
        public void ReleaseConcurrencyPermit(string domain)
        {
            // 获取域名的锁对象
            var lockObj = this.concurrencyLocks.GetOrAdd(domain, _ => new object());

            lock (lockObj)
            {
                if (this.domainCurrentConcurrency.TryGetValue(domain, out int currentConcurrency))
                {
                    if (currentConcurrency > 0)
                    {
                        currentConcurrency--;
                        this.domainCurrentConcurrency[domain] = currentConcurrency;
                    }

                    // 如果并发数为0，可以考虑清理
                    if (currentConcurrency == 0)
                    {
                        this.domainCurrentConcurrency.TryRemove(domain, out _);
                    }
                }
            }
        }

        /// <summary>
        /// 获取域名当前的并发数.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <returns>当前并发数.</returns>
        public int GetCurrentConcurrency(string domain)
        {
            return this.domainCurrentConcurrency.GetValueOrDefault(domain, 0);
        }

        /// <summary>
        /// 获取特定域名和请求类型的延迟.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="requestType">请求类型.</param>
        /// <returns>延迟时间.</returns>
        private TimeSpan GetDelayForRequestType(string domain, string requestType)
        {
            if (this.domainRequestTypeDelays.TryGetValue(domain, out var requestTypeDelays))
            {
                if (requestTypeDelays.TryGetValue(requestType, out TimeSpan typeDelay))
                {
                    return typeDelay;
                }
            }

            return this.domainDelays.GetValueOrDefault(domain, this.defaultDelay);
        }

        /// <summary>
        /// 限制延迟在最小和最大之间.
        /// </summary>
        /// <param name="delay">延迟时间.</param>
        /// <returns>限制后的延迟时间.</returns>
        private TimeSpan ClampDelay(TimeSpan delay)
        {
            if (delay < this.minimumDelay)
            {
                return this.minimumDelay;
            }

            if (delay > this.maximumDelay)
            {
                return this.maximumDelay;
            }

            return delay;
        }
    }
}