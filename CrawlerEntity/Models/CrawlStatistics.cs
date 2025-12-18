using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace  CrawlerFramework.CrawlerEntity.Models;

/// <summary>
/// 爬虫统计信息
/// </summary>
public class CrawlStatistics
{
    /// <summary>
    /// 总发现的URL数量
    /// </summary>
    public int TotalUrlsDiscovered { get; set; }

    /// <summary>
    /// 总处理的URL数量
    /// </summary>
    public int TotalUrlsProcessed { get; set; }

    /// <summary>
    /// 成功处理的URL数量
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败的URL数量
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// 跳过的URL数量
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 平均下载时间（毫秒）
    /// </summary>
    public double AverageDownloadTimeMs { get; set; }

    /// <summary>
    /// 总下载数据量（字节）
    /// </summary>
    public long TotalDownloadSize { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 域名统计
    /// </summary>
    public Dictionary<string, DomainStatistics> DomainStats { get; set; } = [];
}
