// <copyright file="PerformanceConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// 性能配置节，用于配置爬虫的性能相关参数.
    /// </summary>
    public class PerformanceConfig
    {
        /// <summary>
        /// Gets or sets 爬虫可使用的最大内存限制（MB），超过此限制可能会触发内存回收或任务暂停.
        /// </summary>
        public int MemoryLimitMB { get; set; } = 500;

        /// <summary>
        /// Gets or sets 待爬取URL队列的最大大小，超过此大小可能会触发队列限制或丢弃低优先级URL.
        /// </summary>
        public int MaxQueueSize { get; set; } = 10000;

        /// <summary>
        /// Gets or sets a value indicating whether 是否启用HTTP请求和响应的压缩，可减少网络传输量但会增加CPU负载.
        /// </summary>
        public bool EnableCompression { get; set; } = true;
    }
}