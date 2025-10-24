// CrawlerCore/Health/HealthCheckService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrawlerEntity.Enums;
using CrawlerInterFaces.Interfaces;
using CrawlerStorage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrawlerCore.Health
{
    /// <summary>
    /// 健康检查
    /// </summary>
    /// <param name="crawlerEngine"></param>
    /// <param name="storageProvider">可不传，默认为FileSystemStorage</param>
    public class CrawlerHealthCheck(CrawlerEngine crawlerEngine, IStorageProvider? storageProvider) : IHealthCheck
    {
        private readonly CrawlerEngine _crawlerEngine = crawlerEngine;
        private readonly IStorageProvider _storageProvider = storageProvider ?? new FileSystemStorage(null,null);

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();
            var checks = new List<string>();

            try
            {
                // 检查爬虫状态
                data["crawler_status"] = _crawlerEngine.CurrentStatus.ToString();
                data["crawler_uptime"] = _crawlerEngine.Uptime.TotalMinutes;

                // 检查存储
                var totalCount = await _storageProvider.GetTotalCountAsync();
                data["storage_total_urls"] = totalCount;

                // 检查内存使用
                var memoryUsage = GC.GetTotalMemory(false) / 1024 / 1024;
                data["memory_usage_mb"] = memoryUsage;

                // 评估健康状态
                var status = _crawlerEngine.CurrentStatus == CrawlerStatus.Running 
                    ? HealthStatus.Healthy 
                    : HealthStatus.Degraded;

                if (memoryUsage > 500) // 500MB 阈值
                {
                    status = HealthStatus.Degraded;
                    checks.Add("High memory usage");
                }

                return new HealthCheckResult(status, 
                    $"Crawler is {_crawlerEngine.CurrentStatus}", 
                    data: data);
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(HealthStatus.Unhealthy, 
                    "Crawler health check failed", ex, data);
            }
        }
    }
}