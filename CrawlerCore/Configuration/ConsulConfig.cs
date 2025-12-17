// <copyright file="ConsulConfig.cs" company="PlaceholderCompany">
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
    /// Consul配置.
    /// </summary>
    public class ConsulConfig
    {
        /// <summary>
        /// Gets or sets consul地址.
        /// </summary>
        public string Address { get; set; } = "http://localhost:8500";

        /// <summary>
        /// Gets or sets 配置键前缀.
        /// </summary>
        public string ConfigPrefix { get; set; } = "crawler/config";

        /// <summary>
        /// Gets or sets 配置刷新间隔（秒）.
        /// </summary>
        public int RefreshIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets Consul数据中心名称.
        /// </summary>
        public string Datacenter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets Consul访问令牌，用于身份验证和授权.
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }
}