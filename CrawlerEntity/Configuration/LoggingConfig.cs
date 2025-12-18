// <copyright file="LoggingConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using CrawlerEntity.Configuration;

    /// <summary>
    /// 日志配置节.
    /// </summary>
    public class LoggingConfig
    {
        /// <summary>
        /// Gets or sets 日志级别配置.
        /// </summary>
        [JsonPropertyName("LogLevel")]
        public LogConfigLevel LogLevel { get; set; } = new();
    }
}
