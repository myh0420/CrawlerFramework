// <copyright file="AppCrawlerConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using CrawlerEntity.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 应用程序爬虫配置根对象.
    /// </summary>
    public class AppCrawlerConfig
    {
        /// <summary>
        /// Gets or sets 爬虫配置主节.
        /// </summary>
        [JsonPropertyName("CrawlerConfig")]
        public CrawlerConfigSection CrawlerConfig { get; set; } = new CrawlerConfigSection();

        /// <summary>
        /// Gets or sets 日志配置节.
        /// </summary>
        [JsonPropertyName("Logging")]
        public LoggingConfig Logging { get; set; } = new LoggingConfig();

        /// <summary>
        /// Gets or sets 允许的主机配置.
        /// </summary>
        [JsonPropertyName("AllowedHosts")]
        public string AllowedHosts { get; set; } = "*";

        /// <summary>
        /// Gets or sets 配置中心配置节.
        /// </summary>
        [JsonPropertyName("ConfigCenter")]
        public ConfigCenterConfig ConfigCenter { get; set; } = new ConfigCenterConfig();
    }
}