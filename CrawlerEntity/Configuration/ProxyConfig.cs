// <copyright file="ProxyConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerEntity.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// 代理配置节，用于配置爬虫的代理服务器设置.
    /// </summary>
    public class ProxyConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether 是否启用代理服务器，启用后爬虫请求会通过代理发送.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets 可用代理服务器的URL列表，格式为"http://ip:port"或"https://ip:port".
        /// </summary>
        public string[] ProxyUrls { get; set; } = [];

        /// <summary>
        /// Gets or sets 加密后的代理服务器URL列表，仅用于配置文件存储.
        /// </summary>
        public string[] EncryptedProxyUrls { get; set; } = [];

        /// <summary>
        /// Gets or sets 代理服务器轮换策略，用于决定如何从代理列表中选择下一个代理服务器.
        /// </summary>
        public ProxyRotationStrategy RotationStrategy { get; set; } = ProxyRotationStrategy.RoundRobin;

        /// <summary>
        /// Gets or sets 代理服务器可用性测试的间隔时间（分钟），定期测试代理是否可用并更新可用代理列表.
        /// </summary>
        public int TestIntervalMinutes { get; set; } = 30;
    }
}