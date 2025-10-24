// CrawlerMonitor/Controllers/MonitorController.cs
using CrawlerCore;
using CrawlerEntity.Configuration;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CrawlerMonitor.Controllers;
[ApiController]
[Route("api/[controller]")]
public class MonitorController(CrawlerEngine crawlerEngine, IStorageProvider storageProvider,
    IMetadataStore metadataStore, ILogger<MonitorController> logger) : ControllerBase
{
    private readonly CrawlerEngine _crawlerEngine = crawlerEngine;
    private readonly IStorageProvider _storageProvider = storageProvider;
    private readonly IMetadataStore _metadataStore = metadataStore;
    private readonly ILogger<MonitorController> _logger = logger;

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var status = new
            {
                IsRunning = true, // 需要从CrawlerEngine获取实际状态
                StartTime = DateTime.UtcNow.AddHours(-1),
                TotalProcessed = await _storageProvider.GetTotalCountAsync(),
                MemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024,
                ThreadCount = Process.GetCurrentProcess().Threads.Count
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get crawler status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] string? domain = null)
    {
        try
        {
            var stats = new
            {
                TotalUrls = await _storageProvider.GetTotalCountAsync(),
                RecentUrls = domain != null ? 
                    await _storageProvider.GetByDomainAsync(domain, 10) : 
                    [],
                Memory = new
                {
                    Used = GC.GetTotalMemory(false) / 1024 / 1024,
                    Total = Environment.WorkingSet / 1024 / 1024
                },
                Timestamp = DateTime.UtcNow
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("urls")]
    public async Task<IActionResult> GetUrls([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            // 这里需要实现分页逻辑
            var urls = await GetRecentUrlsAsync(page, pageSize);
            return Ok(new { Page = page, PageSize = pageSize, Urls = urls });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get URLs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("urls/{url}")]
    public async Task<IActionResult> GetUrlDetails(string url)
    {
        try
        {
            var result = await _storageProvider.GetByUrlAsync(url);
            if (result == null)
                return NotFound();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get URL details for {Url}", url);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("control/start")]
    public async Task<IActionResult> StartCrawler([FromBody] AdvancedCrawlConfiguration config)
    {
        try
        {
            await _crawlerEngine.StartAsync(config);
            return Ok(new { message = "Crawler started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start crawler");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("control/stop")]
    public async Task<IActionResult> StopCrawler()
    {
        try
        {
            await _crawlerEngine.StopAsync();
            return Ok(new { message = "Crawler stopped successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop crawler");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("urls/{url}")]
    public async Task<IActionResult> DeleteUrl(string url)
    {
        try
        {
            var success = await _storageProvider.DeleteAsync(url);
            if (!success)
                return NotFound();

            return Ok(new { message = "URL deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete URL {Url}", url);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<IEnumerable<object>> GetRecentUrlsAsync(int page, int pageSize)
    {
        // 简化实现 - 在实际项目中应该实现真正的分页
        var allUrls = await _storageProvider.GetByDomainAsync("", 1000);
        return allUrls
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Request.Url,
                r.ProcessedAt,
                r.DownloadResult.StatusCode,
                r.DownloadResult.ContentType,
                ContentLength = r.DownloadResult.RawData?.Length ?? 0
            });
    }
}