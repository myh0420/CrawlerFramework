// <copyright file="StorageModels.cs" company="PlaceholderCompany">
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

    // CrawlerEntity/Models/StorageModels.cs

    /// <summary>
    /// 爬取状态.
    /// </summary>
    public class CrawlState
    {
        /// <summary>
        /// Gets or sets 任务ID.
        /// </summary>
        public string JobId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets 开始时间.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets 结束时间.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Gets or sets 发现的URL总数.
        /// </summary>
        public int TotalUrlsDiscovered { get; set; }

        /// <summary>
        /// Gets or sets 处理的URL总数.
        /// </summary>
        public int TotalUrlsProcessed { get; set; }

        /// <summary>
        /// Gets or sets 错误总数.
        /// </summary>
        public int TotalErrors { get; set; }

        // public Dictionary<string, object> Statistics { get; set; } = new();

        /// <summary>
        /// Gets or sets 爬取统计信息.
        /// </summary>
        public CrawlStatistics? Statistics { get; set; }

        /// <summary>
        /// Gets or sets 任务状态.
        /// </summary>
        public CrawlerStatus Status { get; set; }

        /// <summary>
        /// Gets or sets 任务配置.
        /// </summary>
        public object Configuration { get; set; } = new();
    }
}
