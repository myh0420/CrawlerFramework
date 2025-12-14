using CrawlerEntity.Enums;
using CrawlerEntity.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrawlerEntity.Models
{

    // CrawlerEntity/Models/StorageModels.cs
    public class CrawlState
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public string JobId { get; set; } = string.Empty;
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }
        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }
        /// <summary>
        /// 发现的URL总数
        /// </summary>
        public int TotalUrlsDiscovered { get; set; }
        /// <summary>
        /// 处理的URL总数
        /// </summary>
        public int TotalUrlsProcessed { get; set; }
        /// <summary>
        /// 错误总数
        /// </summary>
        public int TotalErrors { get; set; }
        //public Dictionary<string, object> Statistics { get; set; } = new();
        /// <summary>
        /// 爬取统计信息
        /// </summary>
        public CrawlStatistics? Statistics { get; set; }
        /// <summary>
        /// 任务状态
        /// </summary>
        public CrawlerStatus Status { get; set; }
        /// <summary>
        /// 任务配置
        /// </summary>
        public object Configuration { get; set; } = new();
    }
    /// <summary>
    /// URL状态
    /// </summary>
    public class UrlState
    {
        /// <summary>
        /// URL
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 发现时间
        /// </summary>
        public DateTime DiscoveredAt { get; set; }
        /// <summary>
        /// 处理时间
        /// </summary>
        public DateTime? ProcessedAt { get; set; }
        /// <summary>
        /// HTTP状态码
        /// </summary>
        public int StatusCode { get; set; }
        /// <summary>
        /// 内容长度
        /// </summary>
        public long ContentLength { get; set; }
        /// <summary>
        /// 内容类型
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
        /// <summary>
        /// 下载时间
        /// </summary>
        public TimeSpan DownloadTime { get; set; }
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        /// <summary>
        /// 重试次数
        /// </summary>
        public object? RetryCount { get; set; }
    }    
}
