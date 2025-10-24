using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrawlerEntity.Models;


/// <summary>
/// 域名统计信息
/// </summary>
public class DomainStatistics
{
    public int UrlsDiscovered { get; set; }
    public int UrlsProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public long TotalDownloadSize { get; set; }
    public double AverageDownloadTimeMs { get; set; }
}
