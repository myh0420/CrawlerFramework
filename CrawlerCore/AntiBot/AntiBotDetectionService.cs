// <copyright file="AntiBotDetectionService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.AntiBot;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerFramework.CrawlerInterFaces.Interfaces;
using CrawlerFramework.CrawlerInterFaces.Interfaces.AntiBot;
using CrawlerFramework.CrawlerInterFaces.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// 反爬虫检测服务类，用于检测网站的反爬虫措施并提供相应的处理建议.
/// </summary>
public class AntiBotDetectionService : ICrawlerComponent
{
    /// <summary>
    /// 日志记录器实例，用于记录反爬虫检测相关的日志信息.
    /// </summary>
    private readonly ILogger<AntiBotDetectionService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AntiBotDetectionService"/> class.
    /// 初始化 <see cref="AntiBotDetectionService"/> 类的新实例.
    /// </summary>
    /// <param name="logger">日志记录器实例，用于记录反爬虫检测相关的日志信息.如果为null，则使用默认的LoggerFactory创建新实例.</param>
    public AntiBotDetectionService(ILogger<AntiBotDetectionService>? logger)
    {
        this.logger = logger ?? new Logger<AntiBotDetectionService>(new LoggerFactory());
    }

    /// <summary>
    /// 反爬虫检测器列表，包含多种类型的反爬虫检测实现.
    /// </summary>
    private readonly List<IAntiBotDetector> detectors = [
            new CaptchaDetector(),
            new RateLimitDetector(),
            new IpBlockDetector(),
            new JsChallengeDetector(),
            new CookieTrackingDetector(),
            new UserAgentDetector(),
            new RequestDelayDetector()
        ];

    /// <summary>
    /// 表示反爬虫检测服务是否已初始化.
    /// </summary>
    private bool isInitialized = false;

    /// <summary>
    /// 检测HTTP响应和HTML内容中的反爬虫措施.
    /// </summary>
    /// <param name="response">HTTP响应消息.</param>
    /// <param name="htmlContent">HTML内容.</param>
    /// <returns>反爬虫检测结果，包含是否被封禁及相关信息.</returns>
    public async Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        foreach (var detector in this.detectors)
        {
            var result = await detector.DetectAsync(response, htmlContent);
            if (result.IsBlocked)
            {
                this.logger.LogWarning("Anti-bot detection triggered: {Reason}", result.BlockReason);
                return result;
            }
        }

        return new AntiBotDetectionResult { IsBlocked = false };
    }

    /// <summary>
    /// 添加自定义反爬虫检测器.
    /// </summary>
    /// <param name="detector">要添加的反爬虫检测器实现.</param>
    /// <remarks>
    /// 可以在运行时添加自定义的反爬虫检测器，以扩展反爬虫检测能力.
    /// </remarks>
    public void AddDetector(IAntiBotDetector detector)
    {
        this.detectors.Add(detector);
    }

    /// <summary>
    /// 检查是否应该处理给定的URL.
    /// </summary>
    /// <param name="url">要检查的URL.</param>
    /// <returns>如果URL应该被处理，则为true；否则为false.</returns>
    /// <remarks>
    /// 可以根据URL的特征（如域名、路径等）来决定是否应该处理.
    /// </remarks>
    public bool ShouldProcess(string url)
    {
        // 这里可以添加基于URL的过滤规则
        // 例如：检查是否是已知的反爬虫严格网站
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;

            // 示例：添加一些简单的规则
            var blockedHosts = new List<string> { "example.com" };
            return !blockedHosts.Contains(host);
        }
        catch (UriFormatException ex)
        {
            this.logger.LogWarning(ex, "Invalid URL: {Url}", url);
            return false;
        }
    }

    /// <inheritdoc/>
    /// <summary>
    /// 初始化反爬虫检测服务.
    /// </summary>
    /// <returns>表示异步操作的任务.</returns>
    public Task InitializeAsync()
    {
        if (!this.isInitialized)
        {
            this.isInitialized = true;
            this.logger.LogDebug("AntiBotDetectionService initialized successfully with {DetectorCount} detectors", this.detectors.Count);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <summary>
    /// 关闭反爬虫检测服务.
    /// </summary>
    /// <returns>表示异步操作的任务.</returns>
    public Task ShutdownAsync()
    {
        if (this.isInitialized)
        {
            this.isInitialized = false;

            // 清理检测器资源
            foreach (var detector in this.detectors)
            {
                if (detector is IDisposable disposableDetector)
                {
                    disposableDetector.Dispose();
                }
            }

            this.detectors.Clear();
            this.logger.LogDebug("AntiBotDetectionService shutdown successfully");
        }

        return Task.CompletedTask;
    }
}
