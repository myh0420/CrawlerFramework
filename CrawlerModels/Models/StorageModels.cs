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
        public string JobId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int TotalUrlsDiscovered { get; set; }
        public int TotalUrlsProcessed { get; set; }
        public int TotalErrors { get; set; }
        //public Dictionary<string, object> Statistics { get; set; } = new();
        public CrawlStatistics? Statistics { get; set; }
        public CrawlerStatus Status { get; set; }
        public object Configuration { get; set; } = new();
    }

    public class UrlState
    {
        public string Url { get; set; } = string.Empty;
        public DateTime DiscoveredAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int StatusCode { get; set; }
        public long ContentLength { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public TimeSpan DownloadTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public object? RetryCount { get; set; }
    }    
}
