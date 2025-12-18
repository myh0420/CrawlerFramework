// <copyright file="MonitoringConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// 监控配置节.
    /// </summary>
    public class MonitoringConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether 是否启用指标监控.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether 是否启用跟踪监控.
        /// </summary>
        public bool EnableTracing { get; set; } = false;

        /// <summary>
        /// Gets or sets 指标监控间隔时间（秒）.
        /// </summary>
        public int MetricsIntervalSeconds { get; set; } = 30;
    }
}