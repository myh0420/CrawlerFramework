using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrawlerEntity.Models;

/// <summary>
/// 爬取结果
/// </summary>
public class CrawlResult
{
    /// <summary>
    /// 爬取请求
    /// </summary>
    public CrawlRequest Request { get; set; } = new();
    /// <summary>
    /// 下载结果
    /// </summary>
    public DownloadResult DownloadResult { get; set; } = new();
    /// <summary>
    /// 解析结果
    /// </summary>
    public ParseResult ParseResult { get; set; } = new();
    /// <summary>
    /// 处理时间
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// 处理时间
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }
}
