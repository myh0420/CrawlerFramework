// CrawlerMonitor/Hubs/CrawlerHub.cs
using CrawlerCore;
using CrawlerEntity.Configuration;
using CrawlerEntity.Events;
using CrawlerEntity.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CrawlerMonitor.Hubs
{
    /// <summary>
    /// 爬虫监控中心
    /// </summary>
    public class CrawlerHub : Hub
    {
        /// <summary>
        /// 爬虫引擎
        /// </summary>
        private readonly CrawlerEngine _crawlerEngine;
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<CrawlerHub> _logger;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="crawlerEngine">爬虫引擎</param>
        /// <param name="logger">日志记录器</param>
        public CrawlerHub(CrawlerEngine crawlerEngine, ILogger<CrawlerHub> logger)
        {
            _crawlerEngine = crawlerEngine;
            _logger = logger;

            // 注册所有事件处理器
            _crawlerEngine.OnCrawlCompleted += OnCrawlCompleted;
            _crawlerEngine.OnCrawlError += OnCrawlError;
            _crawlerEngine.OnUrlDiscovered += OnUrlDiscovered;
            _crawlerEngine.OnStatusChanged += OnStatusChanged;
        }
        /// <summary>
        /// 爬取完成事件处理程序
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">爬取完成事件参数</param>
        private async void OnCrawlCompleted(object? sender, CrawlCompletedEventArgs e)
        {
            try
            {
                await Clients.All.SendAsync("CrawlCompleted", new
                {
                    e.Url,
                    e.Depth,
                    e.ContentType,
                    e.ContentLength,
                    LinksFound = e.DiscoveredUrls,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send crawl completed notification");
            }
        }
        /// <summary>
        /// 爬取错误事件处理程序
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">爬取错误事件参数</param>
        private async void OnCrawlError(object? sender, CrawlErrorEventArgs e)
        {
            try
            {
                await Clients.All.SendAsync("CrawlError", new
                {
                    e.Url,
                    e.Depth,
                    e.ErrorMessage,
                    e.Exception.Message,
                    e.Exception.StackTrace,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send crawl error notification");
            }
        }
        /// <summary>
        /// URL发现事件处理程序
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">URL发现事件参数</param> 
        private async void OnUrlDiscovered(object? sender, UrlDiscoveredEventArgs e)
        {
            try
            {
                await Clients.All.SendAsync("UrlDiscovered", new
                {
                    e.SourceUrl,
                    e.DiscoveredUrls,
                    e.AddedUrls,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send URL discovered notification");
            }
        }
        /// <summary>
        /// 爬虫状态改变事件处理程序
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">爬虫状态改变事件参数</param> 
        private async void OnStatusChanged(object? sender, CrawlerStatusChangedEventArgs e)
        {
            try
            {
                await Clients.All.SendAsync("StatusChanged", new
                {
                    e.PreviousStatus,
                    e.CurrentStatus,
                    e.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status changed notification");
            }
        }
        /// <summary>
        /// 客户端连接事件处理程序
        /// </summary>
        /// <returns>异步任务</returns>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

            // 发送当前状态给新连接的客户端
            await Clients.Caller.SendAsync("StatusChanged", new
            {
                PreviousStatus = CrawlerStatus.Idle,
                _crawlerEngine.CurrentStatus,
                Message = "Client connected",
                Timestamp = DateTime.UtcNow
            });

            await base.OnConnectedAsync();
        }
        /// <summary>
        /// 客户端断开连接事件处理程序
        /// </summary>
        /// <param name="exception">异常信息</param>
        /// <returns>异步任务</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // 新增：客户端可以请求当前状态
        /// <summary>
        /// 请求当前状态事件处理程序
        /// </summary>
        /// <returns>异步任务</returns>
        public async Task RequestCurrentStatus()
        {
            var statistics = await _crawlerEngine.GetStatisticsAsync();
            var crawlState = await _crawlerEngine.GetCurrentCrawlStateAsync();

            await Clients.Caller.SendAsync("CurrentStatus", new
            {
                Status = _crawlerEngine.CurrentStatus,
                _crawlerEngine.IsRunning,
                Uptime = _crawlerEngine.Uptime.TotalMinutes,
                JobId = _crawlerEngine.CurrentJobId,
                Statistics = statistics,
                CrawlState = crawlState
            });
        }

        // 新增：客户端可以控制爬虫
        /// <summary>
        /// 控制爬虫事件处理程序
        /// </summary>
        /// <param name="action">控制操作（start/stop/pause/resume）</param>
        /// <returns>异步任务</returns>
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
                            SeedUrls = [ "https://example.com" ],
                            RetryPolicy = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromMilliseconds(1000) }
                        };
                        await _crawlerEngine.StartAsync(crawlConfig);
                        await Clients.Caller.SendAsync("ControlResult",
                            new { Success = true, Message = "Crawler started with default configuration" });
                        break;

                    case "stop":
                        await _crawlerEngine.StopAsync();
                        await Clients.Caller.SendAsync("ControlResult",
                            new { Success = true, Message = "Crawler stopped" });
                        break;

                    case "pause":
                        await _crawlerEngine.PauseAsync();
                        await Clients.Caller.SendAsync("ControlResult",
                            new { Success = true, Message = "Crawler paused" });
                        break;

                    case "resume":
                        await _crawlerEngine.ResumeAsync();
                        await Clients.Caller.SendAsync("ControlResult",
                            new { Success = true, Message = "Crawler resumed" });
                        break;

                    default:
                        await Clients.Caller.SendAsync("ControlResult",
                            new { Success = false, Message = $"Unknown action: {action}" });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute control action: {Action}", action);
                await Clients.Caller.SendAsync("ControlResult",
                    new { Success = false, Message = ex.Message });
            }
        }
    }
}