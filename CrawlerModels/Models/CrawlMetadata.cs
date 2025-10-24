// CrawlerEntity/Models/CrawlMetadata.cs
using CrawlerEntity.Configuration;
using System;
using System.Collections.Generic;

namespace CrawlerEntity.Models;

/// <summary>
/// 爬虫元数据
/// </summary>
public class CrawlMetadata
{
    /// <summary>
    /// 爬虫作业ID
    /// </summary>
    public string JobId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 配置信息
    /// </summary>
    public CrawlConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// 统计信息
    /// </summary>
    public CrawlStatistics Statistics { get; set; } = new CrawlStatistics();

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
}