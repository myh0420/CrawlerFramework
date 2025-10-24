// CrawlerCore/Services/SimpleDomainDelayManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;

namespace CrawlerCore.Services
{
    /// <summary>
    /// 简单的域名延迟管理器
    /// </summary>
    public class SimpleDomainDelayManager : IDomainDelayManager
    {
        private readonly Dictionary<string, DateTime> _lastAccessTimes = [];
        private readonly Dictionary<string, TimeSpan> _domainDelays = [];
        private readonly TimeSpan _defaultDelay = TimeSpan.FromMilliseconds(1000);

        public Task<bool> CanProcessAsync(string domain)
        {
            if (!_lastAccessTimes.TryGetValue(domain, out DateTime value))
                return Task.FromResult(true);

            var delay = _domainDelays.GetValueOrDefault(domain, _defaultDelay);
            var nextAccessTime = value + delay;

            return Task.FromResult(DateTime.UtcNow >= nextAccessTime);
        }

        public Task RecordAccessAsync(string domain)
        {
            _lastAccessTimes[domain] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public void SetDelay(string domain, TimeSpan delay)
        {
            _domainDelays[domain] = delay;
        }
    }
}