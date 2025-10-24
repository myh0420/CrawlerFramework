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
    public CrawlRequest Request { get; set; } = new();
    public DownloadResult DownloadResult { get; set; } = new();
    public ParseResult ParseResult { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ProcessingTime { get; set; }
}
