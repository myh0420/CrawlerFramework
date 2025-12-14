// CrawlerCore/Robots/RobotsTxtParser.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

namespace CrawlerCore.Robots
{
    /// <summary>
    /// Robots.txt 遵守
    /// </summary>
    public class RobotsTxtParser(ILogger<RobotsTxtParser>? logger, HttpClient? httpClient) : ICrawlerComponent
    {
        private readonly ILogger _logger = logger ?? new Logger<RobotsTxtParser>(new LoggerFactory());
        private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
        private readonly Dictionary<string, RobotsTxt> _cache = [];
        private bool _isInitialized = false;
        private readonly bool _httpClientCreatedInternally = httpClient == null;

        public async Task<bool> IsAllowedAsync(string url, string userAgent = "*")
        {
            try
            {
                var uri = new Uri(url);
                var domain = $"{uri.Scheme}://{uri.Host}";

                if (!_cache.ContainsKey(domain))
                {
                    await LoadRobotsTxtAsync(domain);
                }

                if (_cache.TryGetValue(domain, out var robotsTxt))
                {
                    return robotsTxt.IsAllowed(url, userAgent);
                }

                return true; // 如果没有 robots.txt，默认允许
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check robots.txt for {Url}", url);
                return true; // 出错时默认允许
            }
        }

        private async Task LoadRobotsTxtAsync(string domain)
        {
            try
            {
                var robotsUrl = $"{domain}/robots.txt";
                var response = await _httpClient.GetAsync(robotsUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _cache[domain] = new RobotsTxt(content);
                    _logger.LogDebug("Loaded robots.txt for {Domain}", domain);
                }
                else
                {
                    _cache[domain] = new RobotsTxt(""); // 空的 robots.txt
                    _logger.LogDebug("No robots.txt found for {Domain}", domain);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load robots.txt for {Domain}", domain);
                _cache[domain] = new RobotsTxt(""); // 出错时创建空的
            }
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        public Task InitializeAsync()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                _logger.LogDebug("RobotsTxtParser initialized successfully");
            }
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            if (_isInitialized)
            {
                _isInitialized = false;
                ClearCache();
                
                if (_httpClientCreatedInternally)
                {
                    _httpClient.Dispose();
                    _logger.LogDebug("Disposed internal HttpClient");
                }
                
                _logger.LogDebug("RobotsTxtParser shutdown successfully");
            }
            return Task.CompletedTask;
        }
    }

    public class RobotsTxt(string content)
    {
        private readonly List<RobotsRule> _rules = ParseContent(content);

        public bool IsAllowed(string url, string userAgent = "*")
        {
            var path = new Uri(url).AbsolutePath;
            var applicableRules = _rules.Where(r => 
                r.UserAgent == "*" || r.UserAgent.Equals(userAgent, StringComparison.OrdinalIgnoreCase));

            foreach (var rule in applicableRules)
            {
                if (path.StartsWith(rule.Path))
                {
                    return rule.Allow;
                }
            }

            return true; // 默认允许
        }

        private static List<RobotsRule> ParseContent(string content)
        {
            var rules = new List<RobotsRule>();
            var lines = content.Split('\n');
            string currentUserAgent = "*";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (trimmed.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                {
                    currentUserAgent = trimmed[11..].Trim();
                }
                else if (trimmed.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = trimmed[9..].Trim();
                    rules.Add(new RobotsRule { UserAgent = currentUserAgent, Path = path, Allow = false });
                }
                else if (trimmed.StartsWith("Allow:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = trimmed[6..].Trim();
                    rules.Add(new RobotsRule { UserAgent = currentUserAgent, Path = path, Allow = true });
                }
            }

            return rules;
        }
    }

    public class RobotsRule
    {
        public string UserAgent { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool Allow { get; set; }
    }
}