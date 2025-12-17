// <copyright file="ConfigCenterConfig.cs" company="PlaceholderCompany">
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
    /// 配置中心配置节.
    /// </summary>
    public class ConfigCenterConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether 是否启用配置中心.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets 配置中心类型（Consul、Etcd等）.
        /// </summary>
        public string Type { get; set; } = "Consul";

        /// <summary>
        /// Gets or sets consul配置.
        /// </summary>
        public ConsulConfig Consul { get; set; } = new ConsulConfig();
    }
}