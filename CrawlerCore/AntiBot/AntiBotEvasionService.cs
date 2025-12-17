// <copyright file="AntiBotEvasionService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.AntiBot;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// 反爬规避服务，用于管理反爬应对策略.
/// </summary>
public class AntiBotEvasionService : ICrawlerComponent
{
    /// <summary>
    /// 日志记录器实例.
    /// </summary>
    private readonly ILogger<AntiBotEvasionService> logger;

    /// <summary>
    /// 随机数生成器.
    /// </summary>
    private readonly Random random;

    /// <summary>
    /// 常用浏览器的User-Agent列表.
    /// </summary>
    private readonly List<string> userAgents;

    /// <summary>
    /// Cookie存储，用于管理不同域名的Cookie.
    /// </summary>
    private readonly Dictionary<string, CookieContainer> cookieContainers;

    /// <summary>
    /// 表示反爬规避服务是否已初始化.
    /// </summary>
    private bool isInitialized = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="AntiBotEvasionService"/> class.
    /// 初始化 <see cref="AntiBotEvasionService"/> 类的新实例.
    /// </summary>
    /// <param name="logger">日志记录器实例.如果为null，则使用默认的LoggerFactory创建新实例.</param>
    public AntiBotEvasionService(ILogger<AntiBotEvasionService>? logger)
    {
        this.logger = logger ?? new Logger<AntiBotEvasionService>(new LoggerFactory());
        this.random = new Random();
        this.cookieContainers = new Dictionary<string, CookieContainer>();

        // 初始化常用浏览器的User-Agent列表
        this.userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Firefox/119.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/120.0.0.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Edge/120.0.0.0",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Firefox/119.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) AppleWebKit/537.36 (KHTML, like Gecko) Firefox/119.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:119.0) Gecko/20100101 Firefox/119.0",
        };
    }

    /// <summary>
    /// 为HTTP请求配置反爬规避策略.
    /// </summary>
    /// <param name="request">要配置的HTTP请求.</param>
    /// <param name="url">请求的URL.</param>
    public void ConfigureRequest(HttpRequestMessage request, string url)
    {
        // 设置随机User-Agent
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd(this.GetRandomUserAgent());

        // 添加常见的浏览器头信息
        this.AddBrowserHeaders(request);

        // 配置Cookie
        this.ConfigureCookies(request, url);

        this.logger.LogDebug("Request configured with anti-bot evasion strategies: {Url}", url);
    }

    /// <summary>
    /// 获取随机的User-Agent字符串.
    /// </summary>
    /// <returns>随机的User-Agent字符串.</returns>
    public string GetRandomUserAgent()
    {
        int index = this.random.Next(0, this.userAgents.Count);
        return this.userAgents[index];
    }

    /// <summary>
    /// 执行随机延迟，模拟人类用户的行为.
    /// </summary>
    /// <param name="minDelayMs">最小延迟时间（毫秒）.</param>
    /// <param name="maxDelayMs">最大延迟时间（毫秒）.</param>
    /// <returns>表示异步操作的任务.</returns>
    public async Task RandomDelayAsync(int minDelayMs = 1000, int maxDelayMs = 5000)
    {
        int delay = this.random.Next(minDelayMs, maxDelayMs);
        await Task.Delay(delay);
        this.logger.LogDebug("Random delay applied: {Delay}ms", delay);
    }

    /// <summary>
    /// 根据检测结果应用相应的反爬策略.
    /// </summary>
    /// <param name="result">反爬检测结果.</param>
    /// <returns>表示异步操作的任务.</returns>
    public async Task ApplyStrategyAsync(AntiBotDetectionResult result)
    {
        if (result.IsBlocked)
        {
            this.logger.LogWarning("Applying anti-bot evasion strategy: {Reason}", result.BlockReason);

            // 根据不同的检测结果应用不同的策略
            if (result.RetryAfterSeconds > 0)
            {
                // 如果有明确的重试时间，使用该时间
                await Task.Delay(result.RetryAfterSeconds * 1000);
                this.logger.LogDebug("Applied retry delay: {Delay}s", result.RetryAfterSeconds);
            }
            else
            {
                // 否则使用随机延迟
                await this.RandomDelayAsync(3000, 10000);
            }

            // 其他策略可以根据SuggestedAction来应用
            if (result.SuggestedAction.Contains("cookie", StringComparison.OrdinalIgnoreCase))
            {
                // 清理或重置Cookie
                this.ResetCookies();
                this.logger.LogDebug("Cookies reset as per strategy");
            }
        }
    }

    /// <inheritdoc/>
    public Task InitializeAsync()
    {
        if (!this.isInitialized)
        {
            this.isInitialized = true;
            this.logger.LogDebug("AntiBotEvasionService initialized successfully");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <summary>
    /// 关闭反爬规避服务，释放资源.
    /// </summary>
    /// <returns>表示异步操作的任务.</returns>
    public Task ShutdownAsync()
    {
        if (this.isInitialized)
        {
            this.isInitialized = false;
            this.cookieContainers.Clear();
            this.logger.LogDebug("AntiBotEvasionService shutdown successfully");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 为请求添加常见的浏览器头信息.
    /// </summary>
    /// <param name="request">要添加头信息的HTTP请求.</param>
    private void AddBrowserHeaders(HttpRequestMessage request)
    {
        // 添加Accept头
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.5");

        // 添加其他常见头
        request.Headers.Connection.ParseAdd("keep-alive");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
    }

    /// <summary>
    /// 为请求配置Cookie.
    /// </summary>
    /// <param name="request">要配置Cookie的HTTP请求.</param>
    /// <param name="url">请求的URL.</param>
    private void ConfigureCookies(HttpRequestMessage request, string url)
    {
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;

            // 获取或创建该域名的Cookie容器
            if (!this.cookieContainers.TryGetValue(host, out var cookieContainer))
            {
                cookieContainer = new CookieContainer();
                this.cookieContainers[host] = cookieContainer;
            }

            // 目前仅作为示例，实际应用中需要更复杂的Cookie管理
            this.logger.LogDebug("Using cookie container for host: {Host}", host);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Failed to configure cookies for URL: {Url}", url);
        }
    }

    /// <summary>
    /// 重置所有Cookie容器.
    /// </summary>
    private void ResetCookies()
    {
        this.cookieContainers.Clear();
        this.logger.LogDebug("All cookie containers reset");
    }
}