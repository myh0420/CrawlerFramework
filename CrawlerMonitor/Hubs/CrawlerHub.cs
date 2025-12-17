// <copyright file="CrawlerHub.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerMonitor.Hubs
{
    using CrawlerCore;
    using CrawlerEntity.Configuration;
    using CrawlerEntity.Enums;
    using CrawlerEntity.Events;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 爬虫监控中心.
    /// </summary>
    public class CrawlerHub : Hub
    {
        /// <summary>
        /// 爬虫引擎.
        /// </summary>
        private readonly CrawlerEngine crawlerEngine;

        /// <summary>
        /// 日志记录器.
        /// </summary>
        private readonly ILogger<CrawlerHub> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrawlerHub"/> class.
        /// 构造函数.
        /// </summary>
        /// <param name="crawlerEngine">爬虫引擎.</param>
        /// <param name="logger">日志记录器.</param>
        public CrawlerHub(CrawlerEngine crawlerEngine, ILogger<CrawlerHub> logger)
        {
            this.crawlerEngine = crawlerEngine;
            this.logger = logger;

            // 注册所有事件处理器
            this.crawlerEngine.OnCrawlCompleted += this.OnCrawlCompleted;
            this.crawlerEngine.OnCrawlError += this.OnCrawlError;
            this.crawlerEngine.OnUrlDiscovered += this.OnUrlDiscovered;
            this.crawlerEngine.OnStatusChanged += this.OnStatusChanged;
        }

        /// <summary>
        /// 爬取完成事件处理程序.
        /// </summary>
        /// <param name="sender">事件发送者.</param>
        /// <param name="e">爬取完成事件参数.</param>
        private async void OnCrawlCompleted(object? sender, CrawlCompletedEventArgs e)
        {
            try
            {
                await this.Clients.All.SendAsync("CrawlCompleted", new
                {
                    e.Url,
                    e.Depth,
                    e.ContentType,
                    e.ContentLength,
                    LinksFound = e.DiscoveredUrls,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to send crawl completed notification");
            }
        }

        /// <summary>
        /// 爬取错误事件处理程序.
        /// </summary>
        /// <param name="sender">事件发送者.</param>
        /// <param name="e">爬取错误事件参数.</param>
        private async void OnCrawlError(object? sender, CrawlErrorEventArgs e)
        {
            try
            {
                await this.Clients.All.SendAsync("CrawlError", new
                {
                    e.Url,
                    e.Depth,
                    e.ErrorMessage,
                    e.Exception.Message,
                    e.Exception.StackTrace,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to send crawl error notification");
            }
        }

        /// <summary>
        /// URL发现事件处理程序.
        /// </summary>
        /// <param name="sender">事件发送者.</param>
        /// <param name="e">URL发现事件参数.</param>
        private async void OnUrlDiscovered(object? sender, UrlDiscoveredEventArgs e)
        {
            try
            {
                await this.Clients.All.SendAsync("UrlDiscovered", new
                {
                    e.SourceUrl,
                    e.DiscoveredUrls,
                    e.AddedUrls,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to send URL discovered notification");
            }
        }

        /// <summary>
        /// 爬虫状态改变事件处理程序.
        /// </summary>
        /// <param name="sender">事件发送者.</param>
        /// <param name="e">爬虫状态改变事件参数.</param>
        private async void OnStatusChanged(object? sender, CrawlerStatusChangedEventArgs e)
        {
            try
            {
                await this.Clients.All.SendAsync("StatusChanged", new
                {
                    e.PreviousStatus,
                    e.CurrentStatus,
                    e.Message,
                    Timestamp = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to send status changed notification");
            }
        }

        /// <summary>
        /// 客户端连接事件处理程序.
        /// </summary>
        /// <returns>异步任务.</returns>
        public override async Task OnConnectedAsync()
        {
            this.logger.LogInformation("Client connected: {ConnectionId}", this.Context.ConnectionId);

            // 发送当前状态给新连接的客户端
            await this.Clients.Caller.SendAsync("StatusChanged", new
            {
                PreviousStatus = CrawlerStatus.Idle,
                this.crawlerEngine.CurrentStatus,
                Message = "Client connected",
                Timestamp = DateTime.UtcNow,
            });

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// 客户端断开连接事件处理程序.
        /// </summary>
        /// <param name="exception">异常信息.</param>
        /// <returns>异步任务.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            this.logger.LogInformation("Client disconnected: {ConnectionId}", this.Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // 新增：客户端可以请求当前状态

        /// <summary>
        /// 请求当前状态事件处理程序.
        /// </summary>
        /// <returns>异步任务.</returns>
        public async Task RequestCurrentStatus()
        {
            var statistics = await this.crawlerEngine.GetStatisticsAsync();
            var crawlState = await this.crawlerEngine.GetCurrentCrawlStateAsync();

            await this.Clients.Caller.SendAsync("CurrentStatus", new
            {
                Status = this.crawlerEngine.CurrentStatus,
                this.crawlerEngine.IsRunning,
                Uptime = this.crawlerEngine.Uptime.TotalMinutes,
                JobId = this.crawlerEngine.CurrentJobId,
                Statistics = statistics,
                CrawlState = crawlState,
            });
        }

        // 新增：客户端可以控制爬虫

        /// <summary>
        /// 控制爬虫事件处理程序.
        /// </summary>
        /// <param name="action">控制操作（start/stop/pause/resume）.</param>
        /// <returns>异步任务.</returns>
        public async Task ControlCrawler(string action)
        {
            try
            {
                switch (action.ToLower())
                {
                    case "start":
                        // 使用简化配置启动爬虫
                        var crawlConfig = new AdvancedCrawlConfiguration
                        {
                            MaxConcurrentTasks = 5,
                            MaxDepth = 2,
                            SeedUrls = ["https://example.com"],
                            RetryPolicy = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromMilliseconds(1000) },
                        };
                        await this.crawlerEngine.StartAsync(crawlConfig);
                        await this.Clients.Caller.SendAsync(
                            "ControlResult",
                            new { Success = true, Message = "Crawler started with default configuration" });
                        break;

                    case "stop":
                        await this.crawlerEngine.StopAsync();
                        await this.Clients.Caller.SendAsync(
                            "ControlResult",
                            new { Success = true, Message = "Crawler stopped" });
                        break;

                    case "pause":
                        await this.crawlerEngine.PauseAsync();
                        await this.Clients.Caller.SendAsync(
                            "ControlResult",
                            new { Success = true, Message = "Crawler paused" });
                        break;

                    case "resume":
                        await this.crawlerEngine.ResumeAsync();
                        await this.Clients.Caller.SendAsync(
                            "ControlResult",
                            new { Success = true, Message = "Crawler resumed" });
                        break;

                    default:
                        await this.Clients.Caller.SendAsync(
                            "ControlResult",
                            new { Success = false, Message = $"Unknown action: {action}" });
                        break;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to execute control action: {Action}", action);
                await this.Clients.Caller.SendAsync(
                    "ControlResult",
                    new { Success = false, ex.Message });
            }
        }
    }
}