// <copyright file="DomainsConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// 域名配置节，用于配置爬虫允许访问和阻塞的域名规则.
    /// </summary>
    public class DomainsConfig
    {
        /// <summary>
        /// Gets or sets 允许爬虫访问的域名列表，只有在该列表中的域名才会被爬取.
        /// </summary>
        public string[] AllowedDomains { get; set; } = [];

        /// <summary>
        /// Gets or sets 用于阻塞特定URL的正则表达式或字符串模式列表.
        /// </summary>
        public string[] BlockedPatterns { get; set; } = [];
    }
}