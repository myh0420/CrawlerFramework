// <copyright file="CrawlerConfigSection.cs" company="PlaceholderCompany">
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
    /// 爬虫配置主节.
    /// </summary>
    public class CrawlerConfigSection
    {
        /// <summary>
        /// Gets or sets 基础配置节.
        /// </summary>
        [JsonPropertyName("Basic")]
        public BasicConfig Basic { get; set; } = new BasicConfig();

        /// <summary>
        /// Gets or sets 域名配置节.
        /// </summary>
        [JsonPropertyName("Domains")]
        public DomainsConfig Domains { get; set; } = new DomainsConfig();

        /// <summary>
        /// Gets or sets 性能配置节.
        /// </summary>
        [JsonPropertyName("Performance")]
        public PerformanceConfig Performance { get; set; } = new PerformanceConfig();

        /// <summary>
        /// Gets or sets 反爬虫配置节.
        /// </summary>
        [JsonPropertyName("AntiBot")]
        public AntiBotConfig AntiBot { get; set; } = new AntiBotConfig();

        /// <summary>
        /// Gets or sets 代理配置节.
        /// </summary>
        [JsonPropertyName("Proxy")]
        public ProxyConfig Proxy { get; set; } = new ProxyConfig();

        /// <summary>
        /// Gets or sets 存储配置节.
        /// </summary>
        [JsonPropertyName("Storage")]
        public StorageConfig Storage { get; set; } = new StorageConfig();

        /// <summary>
        /// Gets or sets 监控配置节.
        /// </summary>
        [JsonPropertyName("Monitoring")]
        public MonitoringConfig Monitoring { get; set; } = new MonitoringConfig();

        /// <summary>
        /// Gets or sets 数据处理配置节.
        /// </summary>
        [JsonPropertyName("DataProcessing")]
        public DataProcessingConfig DataProcessing { get; set; } = new DataProcessingConfig();

        /// <summary>
        /// Gets or sets 种子配置节.
        /// </summary>
        [JsonPropertyName("Seeds")]
        public SeedsConfig Seeds { get; set; } = new SeedsConfig();

        /// <summary>
        /// 将当前爬虫配置转换为 AdvancedCrawlConfiguration 实例.
        /// </summary>
        /// <returns>转换后的 AdvancedCrawlConfiguration 实例.</returns>
        public AdvancedCrawlConfiguration ToAdvancedCrawlConfiguration()
        {
            return new AdvancedCrawlConfiguration
            {
                // 基础配置
                MaxConcurrentTasks = this.Basic.MaxConcurrentTasks,
                MaxDepth = this.Basic.MaxDepth,
                MaxPages = this.Basic.MaxPages,
                RequestDelay = this.Basic.RequestDelay,
                TimeoutSeconds = this.Basic.TimeoutSeconds,
                RespectRobotsTxt = this.Basic.RespectRobotsTxt,
                FollowRedirects = this.Basic.FollowRedirects,
                AllowedDomains = this.Domains.AllowedDomains,
                BlockedPatterns = this.Domains.BlockedPatterns,

                // 性能配置
                MemoryLimitMB = this.Performance.MemoryLimitMB,
                MaxQueueSize = this.Performance.MaxQueueSize,
                EnableCompression = this.Performance.EnableCompression,

                // 反爬虫配置
                EnableAntiBotDetection = this.AntiBot.EnableDetection,
                RetryPolicy = new RetryPolicy
                {
                    MaxRetries = this.AntiBot.RetryPolicy.MaxRetries,
                    InitialDelay = this.AntiBot.RetryPolicy.InitialDelay,
                    BackoffMultiplier = this.AntiBot.RetryPolicy.BackoffMultiplier,
                    MaxDelay = this.AntiBot.RetryPolicy.MaxDelay,
                },

                // 代理配置
                ProxySettings = new ProxySettings
                {
                    Enabled = this.Proxy.Enabled,
                    ProxyUrls = this.Proxy.ProxyUrls,
                    RotationStrategy = this.Proxy.RotationStrategy,
                    ProxyTestIntervalMinutes = this.Proxy.TestIntervalMinutes,
                },

                // 监控配置
                MonitoringSettings = new MonitoringSettings
                {
                    EnableMetrics = this.Monitoring.EnableMetrics,
                    EnableTracing = this.Monitoring.EnableTracing,
                    MetricsIntervalSeconds = this.Monitoring.MetricsIntervalSeconds,
                },

                // 数据处理配置
                DataCleaningSettings = new DataCleaningSettings
                {
                    RemoveDuplicateContent = this.DataProcessing.RemoveDuplicateContent,
                    RemoveScriptsAndStyles = this.DataProcessing.RemoveScriptsAndStyles,
                    NormalizeText = this.DataProcessing.NormalizeText,
                    MinContentLength = this.DataProcessing.MinContentLength,
                },

                // 是否包含原始数据
                IncludeRawData = true,
            };
        }
    }
}