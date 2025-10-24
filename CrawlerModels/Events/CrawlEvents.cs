// CrawlerCore/Events/CrawlEvents.cs
using CrawlerEntity.Enums;
using CrawlerEntity.Models;
using System;

namespace CrawlerEntity.Events
{
    /// <summary>
    /// 爬取完成事件参数
    /// </summary>
    public class CrawlCompletedEventArgs : EventArgs
    {
        public CrawlResult Result { get; set; } = new();
    }

    /// <summary>
    /// 爬取错误事件参数
    /// </summary>
    public class CrawlErrorEventArgs : EventArgs
    {
        public CrawlRequest Request { get; set; } = new();
        public Exception Exception { get; set; } = new();
    }

    /// <summary>
    /// URL发现事件参数
    /// </summary>
    public class UrlDiscoveredEventArgs : EventArgs
    {
        public string SourceUrl { get; set; } = string.Empty;
        public string DiscoveredUrl { get; set; } = string.Empty;
        public int Depth { get; set; }
    }

    /// <summary>
    /// 爬虫状态改变事件参数
    /// </summary>
    public class CrawlerStatusChangedEventArgs : EventArgs
    {
        public CrawlerStatus PreviousStatus { get; set; }
        public CrawlerStatus CurrentStatus { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}