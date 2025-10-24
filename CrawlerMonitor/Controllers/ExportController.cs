// CrawlerMonitor/Controllers/ExportController.cs
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CrawlerCore.Export;
using CrawlerCore;
using CrawlerInterFaces.Interfaces;

namespace CrawlerMonitor.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExportController(CrawlerEngine crawlerEngine, IStorageProvider storageProvider) : ControllerBase
    {
        private readonly CrawlerEngine _crawlerEngine = crawlerEngine;
        private readonly IStorageProvider _storageProvider = storageProvider;

        [HttpPost("urls")]
        public async Task<IActionResult> ExportUrls([FromQuery] string format = "json", [FromQuery] string? domain = null)
        {
            try
            {
                if (string.IsNullOrEmpty( domain )) { return StatusCode(500, new { success = false, error = "domain参数有误！为空或为null" }); }
                var urls = await _storageProvider.GetByDomainAsync(domain ?? "", 1000);
                
                var fileName = $"crawled_urls_{domain ?? "all"}_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
                var tempPath = Path.GetTempFileName();

                // 这里需要实现数据导出逻辑
                // 暂时返回成功消息
                return Ok(new { 
                    success = true, 
                    message = $"Export started for {urls.Count()} URLs", 
                    fileName 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var statistics = await _crawlerEngine.GetStatisticsAsync();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}