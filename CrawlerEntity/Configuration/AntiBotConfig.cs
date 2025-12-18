// <copyright file="AntiBotConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// 反机器人配置节，用于配置爬虫的反机器人检测和应对策略.
    /// </summary>
    public class AntiBotConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether 是否启用反机器人检测机制，启用后会尝试识别并应对网站的反爬虫措施.
        /// </summary>
        public bool EnableDetection { get; set; } = true;

        /// <summary>
        /// Gets or sets 重试策略配置，定义了当请求失败时的重试次数、延迟和退避算法.
        /// </summary>
        public RetryPolicyConfig RetryPolicy { get; set; } = new RetryPolicyConfig();
    }
}