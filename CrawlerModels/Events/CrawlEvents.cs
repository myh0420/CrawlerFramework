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
        /// <summary>
        /// URL
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 深度
        /// </summary>
        public int Depth { get; set; }
        /// <summary>
        /// 内容类型
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
        /// <summary>
        /// 内容长度
        /// </summary>
        public int ContentLength { get; set; }
        /// <summary>
        /// 发现的URL数量
        /// </summary>
        public int DiscoveredUrls { get; set; }
    }

    /// <summary>
    /// 爬取错误事件参数
    /// </summary>
    public class CrawlErrorEventArgs : EventArgs
    {
        /// <summary>
        /// URL
        /// </summary>
        public string Url { get; set; } = string.Empty;
        /// <summary>
        /// 深度
        /// </summary>
        public int Depth { get; set; }
        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        /// <summary>
        /// 异常
        /// </summary>
        public Exception Exception { get; set; } = new();
    }

    /// <summary>
    /// URL发现事件参数
    /// </summary>
    public class UrlDiscoveredEventArgs : EventArgs
    {
        /// <summary>
        /// 发现URL的源URL
        /// </summary>
        public string SourceUrl { get; set; } = string.Empty;
        /// <summary>
        /// 发现的URL列表
        /// </summary>
        public List<string> DiscoveredUrls { get; set; } = [];
        /// <summary>
        /// 添加到队列的URL数量
        /// </summary>
        public int AddedUrls { get; set; }
    }

    /// <summary>
    /// 爬虫状态改变事件参数
    /// </summary>
    public class CrawlerStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 前一个状态
        /// </summary>
        public CrawlerStatus PreviousStatus { get; set; }
        /// <summary>
        /// 当前状态
        /// </summary>
        public CrawlerStatus CurrentStatus { get; set; }
        /// <summary>
        /// 状态改变消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}