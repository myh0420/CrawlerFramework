// CrawlerDownloader/Services/RotatingUserAgentService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CrawlerFramework.CrawlerDownloader.Services
{
    /// <summary>
    /// User-Agent 轮换服务
    /// </summary>
    public class RotatingUserAgentService
    {
        /// <summary>
        /// 内部 ILogger 实例
        /// </summary>
        private readonly ILogger<RotatingUserAgentService>? _logger;
        /// <summary>
        /// User-Agent 列表
        /// </summary>
        private readonly List<string> _userAgents;
        /// <summary>
        /// 随机数生成器
        /// </summary>
        private readonly Random _random;
        /// <summary>
        /// 当前 User-Agent 索引
        /// </summary>
        private int _currentIndex;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">ILogger 实例（可选）</param>
        public RotatingUserAgentService(ILogger<RotatingUserAgentService>? logger = null)
        {
            _logger = logger ?? new Logger<RotatingUserAgentService>(new LoggerFactory());
            _userAgents = [];
            _random = new Random();
            _currentIndex = 0;

            InitializeDefaultUserAgents();
        }

        /// <summary>
        /// 初始化默认 User-Agent 列表
        /// </summary>
        private void InitializeDefaultUserAgents()
        {
            // 现代浏览器 User-Agent 列表
            var userAgents = new[]
            {
                // Chrome
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                
                // Firefox
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:121.0) Gecko/20100101 Firefox/121.0",
                "Mozilla/5.0 (X11; Linux i686; rv:121.0) Gecko/20100101 Firefox/121.0",
                
                // Safari
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Safari/605.1.15",
                "Mozilla/5.0 (iPad; CPU OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1",
                
                // Edge
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0",
                
                // 移动设备
                "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1",
                "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36",
                "Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36"
            };

            _userAgents.AddRange(userAgents);
            _logger?.LogInformation("Initialized RotatingUserAgentService with {Count} user agents", _userAgents.Count);
        }

        /// <summary>
        /// 获取随机 User-Agent
        /// </summary>
        public string GetRandomUserAgent()
        {
            if (_userAgents.Count == 0)
            {
                _logger?.LogWarning("No user agents available, using default");
                return GetDefaultUserAgent();
            }

            var index = _random.Next(_userAgents.Count);
            var userAgent = _userAgents[index];

            _logger?.LogDebug("Selected user agent: {UserAgent}", userAgent);
            return userAgent;
        }

        /// <summary>
        /// 获取下一个 User-Agent（顺序轮换）
        /// </summary>
        public string GetNextUserAgent()
        {
            if (_userAgents.Count == 0)
            {
                return GetDefaultUserAgent();
            }

            _currentIndex = (_currentIndex + 1) % _userAgents.Count;
            var userAgent = _userAgents[_currentIndex];

            _logger?.LogDebug("Next user agent: {UserAgent}", userAgent);
            return userAgent;
        }

        /// <summary>
        /// 添加自定义 User-Agent
        /// </summary>
        public void AddUserAgent(string userAgent)
        {
            if (!string.IsNullOrWhiteSpace(userAgent) && !_userAgents.Contains(userAgent))
            {
                _userAgents.Add(userAgent);
                _logger?.LogDebug("Added custom user agent: {UserAgent}", userAgent);
            }
        }

        /// <summary>
        /// 添加多个 User-Agent
        /// </summary>
        public void AddUserAgents(IEnumerable<string> userAgents)
        {
            foreach (var userAgent in userAgents)
            {
                AddUserAgent(userAgent);
            }
        }

        /// <summary>
        /// 获取默认 User-Agent
        /// </summary>
        private static string GetDefaultUserAgent()
        {
            return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        }

        /// <summary>
        /// 获取 User-Agent 数量
        /// </summary>
        public int Count => _userAgents.Count;

        /// <summary>
        /// 清空 User-Agent 列表
        /// </summary>
        public void Clear()
        {
            _userAgents.Clear();
            _logger?.LogInformation("Cleared all user agents");
        }
    }
}