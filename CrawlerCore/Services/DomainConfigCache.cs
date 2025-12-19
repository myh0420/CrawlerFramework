// <copyright file="DomainConfigCache.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Services;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CrawlerFramework.CrawlerEntity.Configuration;
using CrawlerFramework.CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// 域名配置缓存服务，用于优化频繁访问的域名配置。
/// </summary>
public class DomainConfigCache : ICrawlerComponent
{
    /// <summary>
    /// 允许的域名缓存。
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> allowedDomainsCache = [];

    /// <summary>
    /// 阻塞的URL模式缓存。
    /// </summary>
    private readonly List<Regex> blockedPatternsCache = [];

    /// <summary>
    /// 日志记录器。
    /// </summary>
    private readonly ILogger<DomainConfigCache> logger;

    /// <summary>
    /// 配置版本号，用于检测配置变化。
    /// </summary>
    private long configVersion = 0;

    /// <summary>
    /// 当前配置。
    /// </summary>
    private CrawlConfiguration? currentConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainConfigCache"/> class.
    /// 初始化 <see cref="DomainConfigCache"/> 类的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public DomainConfigCache(ILogger<DomainConfigCache> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Gets 获取当前配置版本。
    /// </summary>
    public long ConfigVersion => this.configVersion;

    /// <summary>
    /// Gets 获取当前配置。
    /// </summary>
    public CrawlConfiguration? CurrentConfig => this.currentConfig;

    /// <summary>
    /// 初始化组件。
    /// </summary>
    /// <returns>异步任务。</returns>
    public Task InitializeAsync()
    {
        this.logger.LogInformation("DomainConfigCache initialized");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新域名配置缓存。
    /// </summary>
    /// <param name="config">爬取配置。</param>
    public void UpdateConfig(CrawlConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // 更新配置版本
        Interlocked.Increment(ref this.configVersion);
        this.currentConfig = config;

        // 清空现有缓存
        this.allowedDomainsCache.Clear();
        this.blockedPatternsCache.Clear();

        // 缓存允许的域名
        if (config.AllowedDomains != null)
        {
            foreach (var domain in config.AllowedDomains)
            {
                if (!string.IsNullOrWhiteSpace(domain))
                {
                    this.allowedDomainsCache.TryAdd(domain.ToLowerInvariant(), true);
                }
            }
        }

        // 编译并缓存阻塞的URL模式
        if (config.BlockedPatterns != null)
        {
            foreach (var pattern in config.BlockedPatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    try
                    {
                        this.blockedPatternsCache.Add(new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline));
                    }
                    catch (ArgumentException ex)
                    {
                        this.logger.LogWarning(ex, "Invalid blocked pattern: {Pattern}", pattern);
                    }
                }
            }
        }

        this.logger.LogDebug(
            "Domain config cache updated. Allowed domains: {AllowedCount}, Blocked patterns: {BlockedCount}",
            this.allowedDomainsCache.Count,
            this.blockedPatternsCache.Count);
    }

    /// <summary>
    /// 检查域名是否被允许。
    /// </summary>
    /// <param name="domain">域名。</param>
    /// <returns>如果允许则返回 true，否则返回 false。</returns>
    public bool IsDomainAllowed(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        // 如果没有配置允许的域名，则允许所有域名
        if (this.allowedDomainsCache.IsEmpty)
        {
            return true;
        }

        var normalizedDomain = domain.ToLowerInvariant();
        return this.allowedDomainsCache.TryGetValue(normalizedDomain, out bool isAllowed) && isAllowed;
    }

    /// <summary>
    /// 检查URL是否被阻塞。
    /// </summary>
    /// <param name="url">URL。</param>
    /// <returns>如果被阻塞则返回 true，否则返回 false。</returns>
    public bool IsUrlBlocked(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || this.blockedPatternsCache.Count == 0)
        {
            return false;
        }

        foreach (var pattern in this.blockedPatternsCache)
        {
            if (pattern.IsMatch(url))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 关闭组件。
    /// </summary>
    /// <returns>异步任务。</returns>
    public Task ShutdownAsync()
    {
        // 清空缓存
        this.allowedDomainsCache.Clear();
        this.blockedPatternsCache.Clear();

        this.logger.LogInformation("DomainConfigCache shutdown completed");
        return Task.CompletedTask;
    }
}
