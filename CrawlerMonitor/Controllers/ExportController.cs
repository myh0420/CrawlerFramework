// <copyright file="ExportController.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerMonitor.Controllers
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using CrawlerCore;
    using CrawlerCore.Export;
    using CrawlerInterFaces.Interfaces;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    /// 导出控制器.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ExportController : ControllerBase
    {
        /// <summary>
        /// 导出URL控制器.
        /// </summary>
        private readonly CrawlerEngine crawlerEngine;

        /// <summary>
        /// 存储提供程序.
        /// </summary>
        private readonly IStorageProvider storageProvider;

        /// <summary>
        /// 初始化 <see cref="ExportController"/> 类的新实例.
        /// </summary>
        /// <param name="crawlerEngine">爬虫引擎.</param>
        /// <param name="storageProvider">存储提供程序.</param>
        public ExportController(CrawlerEngine crawlerEngine, IStorageProvider storageProvider)
        {
            this.crawlerEngine = crawlerEngine ?? throw new ArgumentNullException(nameof(crawlerEngine), "爬虫引擎参数不能为空");
            this.storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider), "存储提供程序参数不能为空");
        }

        /// <summary>
        /// 导出URL.
        /// </summary>
        /// <param name="format">导出格式（json或csv）.</param>
        /// <param name="domain">要导出的域名（可选）.</param>
        /// <returns>导出结果.</returns>
        [HttpPost("urls")]
        public async Task<IActionResult> ExportUrls([FromQuery] string format = "json", [FromQuery] string? domain = null)
        {
            try
            {
                if (string.IsNullOrEmpty(domain))
                {
                    return this.StatusCode(500, new { success = false, error = "domain参数有误！为空或为null" });
                }

                var urls = await this.storageProvider.GetByDomainAsync(domain ?? string.Empty, 1000);

                var fileName = $"crawled_urls_{domain ?? "all"}_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
                var tempPath = Path.GetTempFileName();

                // 这里需要实现数据导出逻辑
                // 暂时返回成功消息
                return this.Ok(new
                {
                    success = true,
                    message = $"Export started for {urls.Count()} URLs",
                    fileName,
                });
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// 获取爬虫统计信息.
        /// </summary>
        /// <returns>爬虫统计信息.</returns>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var statistics = await this.crawlerEngine.GetStatisticsAsync();
                return this.Ok(statistics);
            }
            catch (Exception ex)
            {
                return this.StatusCode(500, new { error = ex.Message });
            }
        }
    }
}