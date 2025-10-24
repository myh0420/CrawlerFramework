// CrawlerDownloader/Services/ProxyManager.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CrawlerDownloader.Services
{
    /// <summary>
    /// 代理服务器信息
    /// </summary>
    public class ProxyServer
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Protocol { get; set; } = "http";
        public int FailCount { get; set; }
        public int SuccessCount { get; set; }
        public DateTime LastUsed { get; set; }
        public DateTime LastFailed { get; set; }
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 代理服务器地址
        /// </summary>
        public string Address => $"{Protocol}://{Host}:{Port}";

        /// <summary>
        /// 成功率
        /// </summary>
        public double SuccessRate
        {
            get
            {
                var total = SuccessCount + FailCount;
                return total == 0 ? 0 : (double)SuccessCount / total;
            }
        }

        /// <summary>
        /// 创建 WebProxy 对象
        /// </summary>
        public WebProxy ToWebProxy()
        {
            var proxy = new WebProxy(Host, Port);
            
            if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
            {
                proxy.Credentials = new NetworkCredential(Username, Password);
            }

            return proxy;
        }

        public override string ToString()
        {
            return $"{Protocol}://{Host}:{Port} (Success: {SuccessCount}, Fail: {FailCount}, Rate: {SuccessRate:P2})";
        }
    }

    /// <summary>
    /// 代理管理器
    /// </summary>
    public class ProxyManager(ILogger? logger = null)
    {
        private readonly ILogger? _logger = logger;
        private readonly List<ProxyServer> _proxies = [];
        private readonly Random _random = new();
        private readonly Lock _lockObject = new();
        private int _currentIndex = 0;

        /// <summary>
        /// 代理选择策略
        /// </summary>
        public ProxySelectionStrategy Strategy { get; set; } = ProxySelectionStrategy.RoundRobin;

        /// <summary>
        /// 添加代理服务器
        /// </summary>
        public void AddProxy(ProxyServer proxy)
        {
            lock (_lockObject)
            {
                if (!_proxies.Any(p => p.Address == proxy.Address))
                {
                    _proxies.Add(proxy);
                    _logger?.LogInformation("Added proxy: {Proxy}", proxy.Address);
                }
            }
        }

        /// <summary>
        /// 批量添加代理服务器
        /// </summary>
        public void AddProxies(IEnumerable<ProxyServer> proxies)
        {
            foreach (var proxy in proxies)
            {
                AddProxy(proxy);
            }
        }

        /// <summary>
        /// 从字符串添加代理（格式：host:port 或 protocol://host:port）
        /// </summary>
        public void AddProxyFromString(string proxyString, string? username = null, string? password = null)
        {
            try
            {
                var proxy = ParseProxyString(proxyString, username, password);
                if (proxy != null)
                {
                    AddProxy(proxy);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse proxy string: {ProxyString}", proxyString);
            }
        }

        /// <summary>
        /// 获取下一个可用的代理
        /// </summary>
        public ProxyServer? GetNextProxy()
        {
            lock (_lockObject)
            {
                if (_proxies.Count == 0)
                {
                    _logger?.LogDebug("No proxies available");
                    return null;
                }

                var availableProxies = _proxies.Where(p => p.IsEnabled).ToList();
                if (availableProxies.Count == 0)
                {
                    _logger?.LogWarning("No enabled proxies available");
                    return null;
                }

                ProxyServer? selectedProxy = null;

                switch (Strategy)
                {
                    case ProxySelectionStrategy.RoundRobin:
                        selectedProxy = GetRoundRobinProxy(availableProxies);
                        break;
                    case ProxySelectionStrategy.Random:
                        selectedProxy = GetRandomProxy(availableProxies);
                        break;
                    case ProxySelectionStrategy.BySuccessRate:
                        selectedProxy = GetBestSuccessRateProxy(availableProxies);
                        break;
                    case ProxySelectionStrategy.ByUsage:
                        selectedProxy = GetLeastUsedProxy(availableProxies);
                        break;
                }

                if (selectedProxy != null)
                {
                    selectedProxy.LastUsed = DateTime.UtcNow;
                    _logger?.LogDebug("Selected proxy: {Proxy}", selectedProxy);
                }

                return selectedProxy;
            }
        }

        /// <summary>
        /// 轮询策略
        /// </summary>
        private ProxyServer GetRoundRobinProxy(List<ProxyServer> availableProxies)
        {
            _currentIndex = (_currentIndex + 1) % availableProxies.Count;
            return availableProxies[_currentIndex];
        }

        /// <summary>
        /// 随机策略
        /// </summary>
        private ProxyServer GetRandomProxy(List<ProxyServer> availableProxies)
        {
            var index = _random.Next(availableProxies.Count);
            return availableProxies[index];
        }

        /// <summary>
        /// 按成功率选择
        /// </summary>
        private static ProxyServer? GetBestSuccessRateProxy(List<ProxyServer> availableProxies)
        {
            return availableProxies
                .OrderByDescending(p => p.SuccessRate)
                .ThenBy(p => p.FailCount)
                .FirstOrDefault();
        }

        /// <summary>
        /// 按使用频率选择（最少使用）
        /// </summary>
        private static ProxyServer? GetLeastUsedProxy(List<ProxyServer> availableProxies)
        {
            return availableProxies
                .OrderBy(p => p.LastUsed)
                .ThenBy(p => p.SuccessCount)
                .FirstOrDefault();
        }

        /// <summary>
        /// 记录代理成功
        /// </summary>
        public void RecordSuccess(ProxyServer proxy)
        {
            if (proxy != null)
            {
                lock (_lockObject)
                {
                    proxy.SuccessCount++;
                    proxy.LastUsed = DateTime.UtcNow;
                    _logger?.LogDebug("Recorded success for proxy: {Proxy}", proxy.Address);
                }
            }
        }

        /// <summary>
        /// 记录代理失败
        /// </summary>
        public void RecordFailure(ProxyServer proxy, string? reason = null)
        {
            if (proxy != null)
            {
                lock (_lockObject)
                {
                    proxy.FailCount++;
                    proxy.LastFailed = DateTime.UtcNow;
                    
                    // 如果连续失败次数过多，暂时禁用代理
                    if (proxy.FailCount >= 5 && proxy.SuccessRate < 0.2)
                    {
                        proxy.IsEnabled = false;
                        _logger?.LogWarning("Disabled proxy due to poor performance: {Proxy}", proxy.Address);
                    }

                    _logger?.LogDebug("Recorded failure for proxy: {Proxy}, Reason: {Reason}", 
                        proxy.Address, reason ?? "Unknown");
                }
            }
        }

        /// <summary>
        /// 启用所有代理
        /// </summary>
        public void EnableAllProxies()
        {
            lock (_lockObject)
            {
                foreach (var proxy in _proxies)
                {
                    proxy.IsEnabled = true;
                }
                _logger?.LogInformation("Enabled all {Count} proxies", _proxies.Count);
            }
        }

        /// <summary>
        /// 禁用代理
        /// </summary>
        public void DisableProxy(ProxyServer proxy)
        {
            if (proxy != null)
            {
                proxy.IsEnabled = false;
                _logger?.LogInformation("Disabled proxy: {Proxy}", proxy.Address);
            }
        }

        /// <summary>
        /// 移除代理
        /// </summary>
        public void RemoveProxy(ProxyServer proxy)
        {
            lock (_lockObject)
            {
                _proxies.Remove(proxy);
                _logger?.LogInformation("Removed proxy: {Proxy}", proxy.Address);
            }
        }

        /// <summary>
        /// 获取所有代理状态
        /// </summary>
        public IReadOnlyList<ProxyServer> GetAllProxies()
        {
            lock (_lockObject)
            {
                return _proxies.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// 获取可用代理数量
        /// </summary>
        public int GetAvailableProxyCount()
        {
            lock (_lockObject)
            {
                return _proxies.Count(p => p.IsEnabled);
            }
        }

        /// <summary>
        /// 解析代理字符串
        /// </summary>
        private static ProxyServer? ParseProxyString(string proxyString, string? username, string? password)
        {
            if (string.IsNullOrWhiteSpace(proxyString))
                return null;

            // 移除协议前缀（如果有）
            var cleanString = proxyString.Replace("http://", "").Replace("https://", "");
            
            var parts = cleanString.Split(':');
            if (parts.Length < 2)
                return null;

            var host = parts[0];
            if (!int.TryParse(parts[1], out int port))
                return null;

            // 检测协议
            var protocol = proxyString.StartsWith("https://") ? "https" : "http";

            return new ProxyServer
            {
                Host = host,
                Port = port,
                Username = username ?? string.Empty,
                Password = password ?? string.Empty,
                Protocol = protocol
            };
        }

        /// <summary>
        /// 创建使用代理的 HttpClientHandler
        /// </summary>
        public  HttpClientHandler CreateHttpClientHandler(ProxyServer proxy)
        {
            var handler = new HttpClientHandler
            {
                UseProxy = proxy != null,
                Proxy = proxy?.ToWebProxy(),
                UseCookies = false,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            return handler;
        }

        /// <summary>
        /// 测试代理是否可用
        /// </summary>
        public async Task<bool> TestProxyAsync(ProxyServer proxy, string testUrl = "http://www.google.com", int timeoutSeconds = 10)
        {
            if (proxy == null)
                return false;

            try
            {
                using var handler = CreateHttpClientHandler(proxy);
                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
                };

                // 添加随机 User-Agent
                client.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var response = await client.GetAsync(testUrl, HttpCompletionOption.ResponseHeadersRead);
                var isSuccess = response.IsSuccessStatusCode;

                if (isSuccess)
                {
                    RecordSuccess(proxy);
                }
                else
                {
                    RecordFailure(proxy, $"HTTP {response.StatusCode}");
                }

                _logger?.LogDebug("Proxy test for {Proxy}: {Result}", proxy.Address, isSuccess ? "Success" : "Failed");
                return isSuccess;
            }
            catch (Exception ex)
            {
                RecordFailure(proxy, ex.Message);
                _logger?.LogDebug("Proxy test for {Proxy} failed: {Error}", proxy.Address, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 批量测试所有代理
        /// </summary>
        public async Task<List<ProxyServer>> TestAllProxiesAsync(string testUrl = "http://www.google.com", int timeoutSeconds = 10)
        {
            var workingProxies = new List<ProxyServer>();
            var proxies = GetAllProxies();

            _logger?.LogInformation("Testing {Count} proxies...", proxies.Count);

            var tasks = proxies.Select(async proxy =>
            {
                var isWorking = await TestProxyAsync(proxy, testUrl, timeoutSeconds);
                if (isWorking)
                {
                    lock (workingProxies)
                    {
                        workingProxies.Add(proxy);
                    }
                }
                return isWorking;
            });

            await Task.WhenAll(tasks);

            _logger?.LogInformation("Proxy test completed. {WorkingCount}/{TotalCount} proxies are working", 
                workingProxies.Count, proxies.Count);

            return workingProxies;
        }
    }

    /// <summary>
    /// 代理选择策略
    /// </summary>
    public enum ProxySelectionStrategy
    {
        /// <summary>
        /// 轮询
        /// </summary>
        RoundRobin,

        /// <summary>
        /// 随机
        /// </summary>
        Random,

        /// <summary>
        /// 按成功率
        /// </summary>
        BySuccessRate,

        /// <summary>
        /// 按使用频率
        /// </summary>
        ByUsage
    }
}