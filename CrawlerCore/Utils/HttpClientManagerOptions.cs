// <copyright file="HttpClientManagerOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Utils
{
    using System;

    /// <summary>
    /// HttpClient管理器的配置选项类。
    /// </summary>
    public class HttpClientManagerOptions
    {
        /// <summary>
        /// Gets or sets 获取或设置最大客户端数量限制，默认值为10。
        /// </summary>
        public int MaxClients { get; set; } = 10;

        /// <summary>
        /// Gets or sets 获取或设置每个域名的最大客户端数量限制，默认值为5。
        /// </summary>
        public int MaxClientsPerDomain { get; set; } = 5;

        /// <summary>
        /// Gets or sets 获取或设置客户端最大生命周期，默认值为1小时。
        /// </summary>
        public TimeSpan MaxClientLifetime { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets 获取或设置客户端最大空闲时间，默认值为30分钟。
        /// </summary>
        public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets a value indicating whether 获取或设置是否启用基于域名的HttpClient隔离，默认值为true。
        /// </summary>
        public bool EnableDomainIsolation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether 获取或设置是否启用自动清理过期客户端，默认值为true。
        /// </summary>
        public bool EnableAutoCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets 获取或设置自动清理的时间间隔，默认值为30秒。
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets a value indicating whether 获取或设置是否验证域名格式，默认值为true。
        /// </summary>
        public bool ValidateDomainFormat { get; set; } = true;
    }
}