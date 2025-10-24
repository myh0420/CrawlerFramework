// CrawlerMonitor/Hubs/CrawlerHub.cs
using CrawlerCore;
using CrawlerEntity.Events;
using CrawlerEntity.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CrawlerMonitor.Hubs
{
    public class CrawlerHub : Hub
    {
        private readonly CrawlerEngine _crawlerEngine;
        private readonly ILogger<CrawlerHub> _logger;

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

        private async void OnCrawlCompleted(object? sender, CrawlCompletedEventArgs e)
        {
            try
            {
                await Clients.All.SendAsync("CrawlCompleted", new
                {
                    e.Result.Request.Url,
                    e.Result.ProcessedAt,
                    e.Result.DownloadResult.StatusCode,
                    e.Result.DownloadResult.ContentType,
                    LinksFound = e.Result.ParseResult?.Links?.Count ?? 0,
                    ProcessingTime = e.Result.ProcessingTime.TotalMilliseconds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send crawl completed notification");
            }
        }

        private async void OnCrawlError(object? sender, CrawlErrorEventArgs e)
        {
            try
            {
                await Clients.All.SendAsync("CrawlError", new
                {
                    e.Request.Url,
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

        private async void OnUrlDiscovered(object? sender, UrlDiscoveredEventArgs e)
        {
            try
            {
                await Clients.All.SendAsync("UrlDiscovered", new
                {
                    e.SourceUrl,
                    e.DiscoveredUrl,
                    e.Depth,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send URL discovered notification");
            }
        }

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

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // 新增：客户端可以请求当前状态
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
        public async Task ControlCrawler(string action)
        {
            try
            {
                switch (action.ToLower())
                {
                    case "start":
                        // 需要配置信息，这里简化处理
                        await Clients.Caller.SendAsync("ControlResult",
                            new { Success = false, Message = "Start action requires configuration" });
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
                    new { Success = false, ex.Message });
            }
        }
    }
}