// CrawlerCore/Services/SimpleDomainDelayManager.cs
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;

namespace CrawlerCore.Services
{
    /// <summary>
    /// 增强的域名延迟管理器，支持动态延迟调整和请求类型感知
    /// </summary>
    public class SimpleDomainDelayManager : IDomainDelayManager
    {
        private readonly ConcurrentDictionary<string, DateTime> _lastAccessTimes = [];
        private readonly ConcurrentDictionary<string, TimeSpan> _domainDelays = [];
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TimeSpan>> _domainRequestTypeDelays = [];
        private readonly TimeSpan _defaultDelay = TimeSpan.FromMilliseconds(1000);
        private readonly TimeSpan _minimumDelay = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan _maximumDelay = TimeSpan.FromSeconds(10);
        private readonly double _dynamicDelayFactor = 1.2;
        private readonly double _recoveryFactor = 0.9;

        /// <summary>
        /// 检查是否可以处理指定域名的请求
        /// </summary>
        /// <param name="domain">域名</param>
        /// <returns>如果可以处理则返回true，否则返回false</returns>
        public Task<bool> CanProcessAsync(string domain)
        {
            return CanProcessAsync(domain, "default");
        }

        /// <summary>
        /// 检查是否可以处理指定域名和请求类型的请求
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="requestType">请求类型</param>
        /// <returns>如果可以处理则返回true，否则返回false</returns>
        public Task<bool> CanProcessAsync(string domain, string requestType)
        {
            if (!_lastAccessTimes.TryGetValue(domain, out DateTime value))
                return Task.FromResult(true);

            var delay = GetDelayForRequestType(domain, requestType);
            var nextAccessTime = value + delay;

            return Task.FromResult(DateTime.UtcNow >= nextAccessTime);
        }

        /// <summary>
        /// 记录域名访问时间
        /// </summary>
        /// <param name="domain">域名</param>
        /// <returns>异步任务</returns>
        public Task RecordAccessAsync(string domain)
        {
            return RecordAccessAsync(domain, "default");
        }

        /// <summary>
        /// 记录域名和请求类型的访问时间
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="requestType">请求类型</param>
        /// <returns>异步任务</returns>
        public Task RecordAccessAsync(string domain, string requestType)
        {
            _lastAccessTimes[domain] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置域名延迟
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="delay">延迟时间</param>
        public void SetDelay(string domain, TimeSpan delay)
        {
            var clampedDelay = ClampDelay(delay);
            _domainDelays[domain] = clampedDelay;
        }

        /// <summary>
        /// 设置域名特定请求类型的延迟
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="requestType">请求类型</param>
        /// <param name="delay">延迟时间</param>
        public void SetDelay(string domain, string requestType, TimeSpan delay)
        {
            var clampedDelay = ClampDelay(delay);
            var requestTypeDelays = _domainRequestTypeDelays.GetOrAdd(domain, _ => new ConcurrentDictionary<string, TimeSpan>());
            requestTypeDelays[requestType] = clampedDelay;
        }

        /// <summary>
        /// 动态增加域名延迟
        /// </summary>
        /// <param name="domain">域名</param>
        public void IncreaseDelay(string domain)
        {
            if (_domainDelays.TryGetValue(domain, out TimeSpan currentDelay))
            {
                var newDelay = ClampDelay(currentDelay * _dynamicDelayFactor);
                _domainDelays[domain] = newDelay;
            }
            else
            {
                var newDelay = ClampDelay(_defaultDelay * _dynamicDelayFactor);
                _domainDelays[domain] = newDelay;
            }
        }

        /// <summary>
        /// 动态减少域名延迟（恢复）
        /// </summary>
        /// <param name="domain">域名</param>
        public void DecreaseDelay(string domain)
        {
            if (_domainDelays.TryGetValue(domain, out TimeSpan currentDelay))
            {
                var newDelay = ClampDelay(currentDelay * _recoveryFactor);
                _domainDelays[domain] = newDelay;
            }
        }

        /// <summary>
        /// 获取特定域名和请求类型的延迟
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="requestType">请求类型</param>
        /// <returns>延迟时间</returns>
        private TimeSpan GetDelayForRequestType(string domain, string requestType)
        {
            if (_domainRequestTypeDelays.TryGetValue(domain, out var requestTypeDelays))
            {
                if (requestTypeDelays.TryGetValue(requestType, out TimeSpan typeDelay))
                {
                    return typeDelay;
                }
            }

            return _domainDelays.GetValueOrDefault(domain, _defaultDelay);
        }

        /// <summary>
        /// 限制延迟在最小和最大之间
        /// </summary>
        /// <param name="delay">延迟时间</param>
        /// <returns>限制后的延迟时间</returns>
        private TimeSpan ClampDelay(TimeSpan delay)
        {
            if (delay < _minimumDelay)
                return _minimumDelay;
            if (delay > _maximumDelay)
                return _maximumDelay;
            return delay;
        }
    }
}