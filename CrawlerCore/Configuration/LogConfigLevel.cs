// <copyright file="LogConfigLevel.cs" company="PlaceholderCompany">
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
    /// 日志级别配置类.
    /// </summary>
    public class LogConfigLevel
    {
        /// <summary>
        /// Gets or sets 默认日志级别.
        /// </summary>
        public string Default { get; set; } = "Information";

        /// <summary>
        /// Gets or sets microsoft 日志级别.
        /// </summary>
        public string Microsoft { get; set; } = "Warning";

        /// <summary>
        /// Gets or sets system 日志级别.
        /// </summary>
        public string System { get; set; } = "Warning";

        /// <summary>
        /// Gets or sets microsoft.AspNetCore 日志级别.
        /// </summary>
        [JsonPropertyName("Microsoft.AspNetCore")]
        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }
}