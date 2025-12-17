// <copyright file="UrlState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerEntity.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using CrawlerEntity.Enums;
    using CrawlerEntity.Models;

    /// <summary>
    /// Gets or sets URL状态.
    /// </summary>
    public class UrlState
    {
        /// <summary>
        /// Gets or sets URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets 发现时间.
        /// </summary>
        public DateTime DiscoveredAt { get; set; }

        /// <summary>
        /// Gets or sets 处理时间.
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Gets or sets HTTP状态码.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets 内容长度.
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Gets or sets 内容类型.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets 下载时间.
        /// </summary>
        public TimeSpan DownloadTime { get; set; }

        /// <summary>
        /// Gets or sets 错误消息.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets 重试次数.
        /// </summary>
        public object? RetryCount { get; set; }
    }
}
