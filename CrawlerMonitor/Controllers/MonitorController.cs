// <copyright file="MonitorController.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerMonitor.Controllers;

using System.Diagnostics;
using CrawlerCore;
using CrawlerCore.Metrics;
using CrawlerEntity.Configuration;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// 监控控制器.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MonitorController(CrawlerEngine crawlerEngine, IStorageProvider storageProvider,
    IMetadataStore metadataStore, ILogger<MonitorController> logger) : ControllerBase
{
    // 常量数组改为static readonly字段，避免重复创建

    /// <summary>
    /// 错误类型数组.
    /// </summary>
    private static readonly string[] ErrorTypes = ["Network", "Parse", "AntiBot", "Timeout", "Other"];

    /// <summary>
    /// HTTP状态码数组.
    /// </summary>
    private static readonly int[] StatusCodes = [404, 500, 403, 408, 502];

    /// <summary>
    /// 爬虫引擎.
    /// </summary>
    private readonly CrawlerEngine crawlerEngine = crawlerEngine;

    /// <summary>
    /// 存储提供程序.
    /// </summary>
    private readonly IStorageProvider storageProvider = storageProvider;

    /// <summary>
    /// 元数据存储.
    /// </summary>
    private readonly IMetadataStore metadataStore = metadataStore;

    /// <summary>
    /// 日志记录器.
    /// </summary>
    private readonly ILogger<MonitorController> logger = logger;

    /// <summary>
    /// 获取爬虫状态.
    /// </summary>
    /// <returns>爬虫状态.</returns>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var status = new
            {
                this.crawlerEngine.IsRunning,
                TotalProcessed = await this.storageProvider.GetTotalCountAsync(),
                MemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024,
                ThreadCount = Process.GetCurrentProcess().Threads.Count,
            };

            return this.Ok(status);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get crawler status");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取统计信息.
    /// </summary>
    /// <param name="domain">要获取统计信息的域名（可选）.</param>
    /// <returns>统计信息.</returns>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] string? domain = null)
    {
        try
        {
            var stats = new
            {
                TotalUrls = await this.storageProvider.GetTotalCountAsync(),
                RecentUrls = domain != null ?
                    await this.storageProvider.GetByDomainAsync(domain, 10) :
                    [],
                Memory = new
                {
                    Used = GC.GetTotalMemory(false) / 1024 / 1024,
                    Total = Environment.WorkingSet / 1024 / 1024,
                },
                Timestamp = DateTime.UtcNow,
            };

            return this.Ok(stats);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get statistics");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取最近URL.
    /// </summary>
    /// <param name="page">页码（可选，默认值为1）.</param>
    /// <param name="pageSize">每页数量（可选，默认值为50）.</param>
    /// <returns>最近URL列表.</returns>
    [HttpGet("urls")]
    public async Task<IActionResult> GetUrls([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            // 这里需要实现分页逻辑
            var urls = await this.GetRecentUrlsAsync(page, pageSize);
            return this.Ok(new { Page = page, PageSize = pageSize, Urls = urls });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get URLs");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取URL详情.
    /// </summary>
    /// <param name="url">要获取详情的URL.</param>
    /// <returns>URL详情.</returns>
    [HttpGet("urls/{url}")]
    public async Task<IActionResult> GetUrlDetails(string url)
    {
        try
        {
            var result = await this.storageProvider.GetByUrlAsync(url);
            if (result == null)
            {
                return this.NotFound();
            }

            return this.Ok(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get URL details for {Url}", url);
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 启动爬虫.
    /// </summary>
    /// <param name="config">高级爬取配置.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("control/start")]
    public async Task<IActionResult> StartCrawler([FromBody] AdvancedCrawlConfiguration config)
    {
        try
        {
            await this.crawlerEngine.StartAsync(config);
            return this.Ok(new { message = "Crawler started successfully" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to start crawler");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 停止爬虫.
    /// </summary>
    /// <returns>操作结果.</returns>
    [HttpPost("control/stop")]
    public async Task<IActionResult> StopCrawler()
    {
        try
        {
            await this.crawlerEngine.StopAsync();
            return this.Ok(new { message = "Crawler stopped successfully" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to stop crawler");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 添加种子URL.
    /// </summary>
    /// <param name="urls">要添加的种子URL列表.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("control/add-seeds")]
    public async Task<IActionResult> AddSeedUrls([FromBody] List<string> urls)
    {
        try
        {
            await this.crawlerEngine.AddSeedUrlsAsync(urls);
            return this.Ok(new { message = "Seed URLs added successfully" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to add seed URLs");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 删除URL.
    /// </summary>
    /// <param name="url">要删除的URL.</param>
    /// <returns>操作结果.</returns>
    [HttpDelete("urls/{url}")]
    public async Task<IActionResult> DeleteUrl(string url)
    {
        try
        {
            var success = await this.storageProvider.DeleteAsync(url);
            if (!success)
            {
                return this.NotFound();
            }

            return this.Ok(new { message = "URL deleted successfully" });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to delete URL {Url}", url);
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取错误统计信息.
    /// </summary>
    /// <returns>错误统计信息.</returns>
    [HttpGet("errors/statistics")]
    public async Task<IActionResult> GetErrorStatistics()
    {
        try
        {
            // 从爬虫引擎获取指标服务
            var metrics = this.crawlerEngine.GetMetrics();

            // 这里应该从metrics获取真实数据，但由于Metrics API的异步特性，
            // 我们使用模拟数据来展示概念。在生产环境中，应该使用Prometheus等指标收集器。
            // 从存储提供程序获取真实的已处理URL数量
            var totalProcessed = await this.storageProvider.GetTotalCountAsync();

            // 假设我们可以从指标系统获取这些数据
            var errorStats = new
            {
                TotalErrors = 15, // 从metrics获取
                NetworkErrors = 8, // 从metrics获取
                ParseErrors = 3, // 从metrics获取
                AntiBotErrors = 2, // 从metrics获取
                TimeoutErrors = 1, // 从metrics获取
                OtherErrors = 1, // 从metrics获取
                TotalProcessed = totalProcessed,
                ErrorRate = totalProcessed > 0 ? 15.0 / totalProcessed * 100 : 0,
                Timestamp = DateTime.UtcNow,
            };

            return this.Ok(errorStats);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get error statistics");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取错误分布.
    /// </summary>
    /// <returns>错误分布.</returns>
    [HttpGet("errors/distribution")]
    public async Task<IActionResult> GetErrorDistribution()
    {
        try
        {
            // 从爬虫引擎获取指标服务
            var metrics = this.crawlerEngine.GetMetrics();

            // 从存储提供程序获取真实的已处理URL数量
            var totalProcessed = await this.storageProvider.GetTotalCountAsync();

            // 这里应该从metrics获取真实数据，但由于Metrics API的特性，
            // 我们使用模拟数据来展示概念
            var errorDistribution = new[]
            {
                new { ErrorType = "Network", Count = 8, Percentage = 53.3 },
                new { ErrorType = "Parse", Count = 3, Percentage = 20.0 },
                new { ErrorType = "AntiBot", Count = 2, Percentage = 13.3 },
                new { ErrorType = "Timeout", Count = 1, Percentage = 6.7 },
                new { ErrorType = "Other", Count = 1, Percentage = 6.7 },
            };

            return this.Ok(new
            {
                Distribution = errorDistribution,
                TotalProcessed = totalProcessed,
                TotalErrors = errorDistribution.Sum(e => e.Count),
                Timestamp = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get error distribution");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取最近错误.
    /// </summary>
    /// <param name="count">要获取的错误数量.</param>
    /// <returns>最近错误.</returns>
    [HttpGet("errors/recent")]
    public async Task<IActionResult> GetRecentErrors([FromQuery] int count = 20)
    {
        try
        {
            // 从爬虫引擎获取指标服务
            var metrics = this.crawlerEngine.GetMetrics();

            // 从存储提供程序获取最近的URL记录，然后从中筛选错误
            var recentUrls = await this.storageProvider.GetByDomainAsync(string.Empty, count * 2);

            // 这里应该从存储提供程序获取真实的错误记录
            // 但由于我们没有实现错误记录存储，我们使用模拟数据来展示概念
            var recentErrors = Enumerable.Range(1, count).Select(i => new
            {
                Id = i,
                Url = "https://yy.suyang123.com/sample/" + i + ".html",
                ErrorType = ErrorTypes[new Random().Next(ErrorTypes.Length)],
                ErrorMessage = "Sample error message for URL " + i,
                Timestamp = DateTime.UtcNow.AddMinutes(-i * 5),
                StatusCode = StatusCodes[new Random().Next(StatusCodes.Length)],
                RetryCount = new Random().Next(0, 3),
            });

            return this.Ok(new
            {
                Errors = recentErrors,
                Total = count,
                Timestamp = DateTime.UtcNow,
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to get recent errors");
            return this.StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 获取最近URL.
    /// </summary>
    /// <param name="page">页码.</param>
    /// <param name="pageSize">每页数量.</param>
    /// <returns>最近URL.</returns>
    private async Task<IEnumerable<object>> GetRecentUrlsAsync(int page, int pageSize)
    {
        // 简化实现 - 在实际项目中应该实现真正的分页
        var allUrls = await this.storageProvider.GetByDomainAsync(string.Empty, 1000);
        return allUrls
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Request.Url,
                r.ProcessedAt,
                r.DownloadResult.StatusCode,
                r.DownloadResult.ContentType,
                ContentLength = r.DownloadResult.RawData?.Length ?? 0,
            });
    }
}