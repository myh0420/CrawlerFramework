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
    /// <summary>
    /// 发现的URL数量
    /// </summary>
    public int UrlsDiscovered { get; set; }
    /// <summary>
    /// 处理的URL数量
    /// </summary>
    public int UrlsProcessed { get; set; }
    /// <summary>
    /// 成功处理的URL数量
    /// </summary>
    public int SuccessCount { get; set; }
    /// <summary>
    /// 处理错误的URL数量
    /// </summary>
    public int ErrorCount { get; set; }
    /// <summary>
    /// 总下载大小（字节）
    /// </summary>
    public long TotalDownloadSize { get; set; }
    /// <summary>
    /// 平均下载时间（毫秒）
    /// </summary>
    public double AverageDownloadTimeMs { get; set; }
}
