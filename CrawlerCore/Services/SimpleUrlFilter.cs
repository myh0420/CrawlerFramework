// CrawlerCore/Services/SimpleUrlFilter.cs
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CrawlerInterFaces.Interfaces;

namespace CrawlerCore.Services
{
    /// <summary>
    /// 简单的URL过滤器实现
    /// </summary>
    public class SimpleUrlFilter : IUrlFilter
    {
        private readonly HashSet<string> _allowedDomains = [];
        private readonly List<Regex> _blockedPatterns = [];

        public bool IsAllowed(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            // 检查协议
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                return false;

            // 检查域名
            if (_allowedDomains.Count != 0)
            {
                var domain = GetDomain(url);
                if (!_allowedDomains.Contains(domain))
                    return false;
            }

            // 检查阻止模式
            if (_blockedPatterns.Any(pattern => pattern.IsMatch(url)))
                return false;

            return true;
        }

        public void AddAllowedDomain(string domain)
        {
            _allowedDomains.Add(domain.ToLowerInvariant());
        }

        public void AddBlockedPattern(string pattern)
        {
            _blockedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase));
        }

        private static string GetDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

