// <copyright file="ExportController.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerMonitor.Controllers
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using CrawlerFramework.CrawlerCore;
    using CrawlerFramework.CrawlerCore.Export;
    using CrawlerFramework.CrawlerEntity.Models;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
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
        /// 数据导出服务.
        /// </summary>
        private readonly DataExportService exportService;

        /// <summary>
        /// 初始化 <see cref="ExportController"/> 类的新实例.
        /// </summary>
        /// <param name="crawlerEngine">爬虫引擎.</param>
        /// <param name="storageProvider">存储提供程序.</param>
        /// <param name="exportService">数据导出服务.</param>
        public ExportController(CrawlerEngine crawlerEngine, IStorageProvider storageProvider, DataExportService exportService)
        {
            this.crawlerEngine = crawlerEngine ?? throw new ArgumentNullException(nameof(crawlerEngine), "爬虫引擎参数不能为空");
            this.storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider), "存储提供程序参数不能为空");
            this.exportService = exportService ?? throw new ArgumentNullException(nameof(exportService), "数据导出服务参数不能为空");
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

                // 使用数据导出服务实现真正的数据导出
                byte[] exportData;
                string contentType;

                // 根据格式选择导出器并导出数据
                var tempFilePath = Path.GetTempFileName();
                try
                {
                    string finalTempPath;
                    switch (format.ToLower())
                    {
                        case "csv":
                            finalTempPath = Path.ChangeExtension(tempFilePath, ".csv");
                            contentType = "text/csv";
                            break;
                        case "json":
                            finalTempPath = Path.ChangeExtension(tempFilePath, ".json");
                            contentType = "application/json";
                            break;
                        case "excel":
                        case "xlsx":
                            finalTempPath = Path.ChangeExtension(tempFilePath, ".xlsx");
                            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                            fileName = fileName.Replace(".excel", ".xlsx");
                            break;
                        default:
                            return this.BadRequest(new { success = false, error = "不支持的导出格式，请使用json、csv或excel" });
                    }

                    // 导出数据到临时文件
                    await this.exportService.ExportAsync(urls.ToList(), finalTempPath);
                    
                    // 读取导出文件内容
                    exportData = await System.IO.File.ReadAllBytesAsync(finalTempPath);
                }
                finally
                {
                    // 清理临时文件
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }

                // 返回导出文件
                return this.File(exportData, contentType, fileName);
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