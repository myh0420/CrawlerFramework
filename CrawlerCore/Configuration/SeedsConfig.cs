// <copyright file="SeedsConfig.cs" company="PlaceholderCompany">
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
    /// 种子配置节.
    /// </summary>
    public class SeedsConfig
    {
        /// <summary>
        /// Gets or sets 种子 URL 列表.
        /// </summary>
        public string[] SeedUrls { get; set; } = [];
    }
}