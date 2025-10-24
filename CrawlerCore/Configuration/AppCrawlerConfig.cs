// CrawlerCore/Configuration/AppCrawlerConfig.cs
using CrawlerEntity.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CrawlerCore.Configuration
{
    /// <summary>
    /// 应用程序爬虫配置根对象
    /// </summary>
    public class AppCrawlerConfig
    {
        [JsonPropertyName("CrawlerConfig")]
        public CrawlerConfigSection CrawlerConfig { get; set; } = new CrawlerConfigSection();

        [JsonPropertyName("Logging")]
        public LoggingConfig Logging { get; set; } = new LoggingConfig();

        [JsonPropertyName("AllowedHosts")]
        public string AllowedHosts { get; set; } = "*";
    }

    /// <summary>
    /// 爬虫配置主节
    /// </summary>
    public class CrawlerConfigSection
    {
        [JsonPropertyName("Basic")]
        public BasicConfig Basic { get; set; } = new BasicConfig();

        [JsonPropertyName("Domains")]
        public DomainsConfig Domains { get; set; } = new DomainsConfig();

        [JsonPropertyName("Performance")]
        public PerformanceConfig Performance { get; set; } = new PerformanceConfig();

        [JsonPropertyName("AntiBot")]
        public AntiBotConfig AntiBot { get; set; } = new AntiBotConfig();

        [JsonPropertyName("Proxy")]
        public ProxyConfig Proxy { get; set; } = new ProxyConfig();

        [JsonPropertyName("Storage")]
        public StorageConfig Storage { get; set; } = new StorageConfig();

        [JsonPropertyName("Monitoring")]
        public MonitoringConfig Monitoring { get; set; } = new MonitoringConfig();

        [JsonPropertyName("DataProcessing")]
        public DataProcessingConfig DataProcessing { get; set; } = new DataProcessingConfig();

        [JsonPropertyName("Seeds")]
        public SeedsConfig Seeds { get; set; } = new SeedsConfig(); 

        /// <summary>
        /// 转换为 AdvancedCrawlConfiguration
        /// </summary>
        public AdvancedCrawlConfiguration ToAdvancedCrawlConfiguration()
        {
            return new AdvancedCrawlConfiguration
            {
                // 基础配置
                MaxConcurrentTasks = Basic.MaxConcurrentTasks,
                MaxDepth = Basic.MaxDepth,
                MaxPages = Basic.MaxPages,
                RequestDelay = Basic.RequestDelay,
                TimeoutSeconds = Basic.TimeoutSeconds,
                RespectRobotsTxt = Basic.RespectRobotsTxt,
                FollowRedirects = Basic.FollowRedirects,
                AllowedDomains = Domains.AllowedDomains,
                BlockedPatterns = Domains.BlockedPatterns,

                // 性能配置
                MemoryLimitMB = Performance.MemoryLimitMB,
                MaxQueueSize = Performance.MaxQueueSize,
                EnableCompression = Performance.EnableCompression,

                // 反爬虫配置
                EnableAntiBotDetection = AntiBot.EnableDetection,
                RetryPolicy = new RetryPolicy
                {
                    MaxRetries = AntiBot.RetryPolicy.MaxRetries,
                    InitialDelay = AntiBot.RetryPolicy.InitialDelay,
                    BackoffMultiplier = AntiBot.RetryPolicy.BackoffMultiplier,
                    MaxDelay = AntiBot.RetryPolicy.MaxDelay
                },

                // 代理配置
                ProxySettings = new ProxySettings
                {
                    Enabled = Proxy.Enabled,
                    ProxyUrls = Proxy.ProxyUrls,
                    RotationStrategy = Proxy.RotationStrategy,
                    ProxyTestIntervalMinutes = Proxy.TestIntervalMinutes
                },

                // 监控配置
                MonitoringSettings = new MonitoringSettings
                {
                    EnableMetrics = Monitoring.EnableMetrics,
                    EnableTracing = Monitoring.EnableTracing,
                    MetricsIntervalSeconds = Monitoring.MetricsIntervalSeconds
                },

                // 数据处理配置
                DataCleaningSettings = new DataCleaningSettings
                {
                    RemoveDuplicateContent = DataProcessing.RemoveDuplicateContent,
                    RemoveScriptsAndStyles = DataProcessing.RemoveScriptsAndStyles,
                    NormalizeText = DataProcessing.NormalizeText,
                    MinContentLength = DataProcessing.MinContentLength
                }
            };
        }
    }

    // 配置子节类
    public class BasicConfig
    {
        public int MaxConcurrentTasks { get; set; } = 10;
        public int MaxDepth { get; set; } = 3;
        public int MaxPages { get; set; } = 1000;
        public TimeSpan RequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        public int TimeoutSeconds { get; set; } = 30;
        public bool RespectRobotsTxt { get; set; } = true;
        public bool FollowRedirects { get; set; } = true;
    }

    public class LoggingConfig {
        [JsonPropertyName("LogLevel")]
        public LogConfigLevel LogLevel { get; set; } = new();
    }
    public class LogConfigLevel
    {
        public string Default { get; set; } = "Information";
        public string Microsoft { get; set; } = "Warning";
        public string System { get; set; } = "Warning";
        [JsonPropertyName("Microsoft.AspNetCore")]
        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }
    public class DomainsConfig
    {
        public string[] AllowedDomains { get; set; } = [];
        public string[] BlockedPatterns { get; set; } = [];
    }

    public class PerformanceConfig
    {
        public int MemoryLimitMB { get; set; } = 500;
        public int MaxQueueSize { get; set; } = 10000;
        public bool EnableCompression { get; set; } = true;
    }

    public class AntiBotConfig
    {
        public bool EnableDetection { get; set; } = true;
        public RetryPolicyConfig RetryPolicy { get; set; } = new RetryPolicyConfig();
    }

    public class RetryPolicyConfig
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        public double BackoffMultiplier { get; set; } = 2.0;
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class ProxyConfig
    {
        public bool Enabled { get; set; } = false;
        public string[] ProxyUrls { get; set; } = [];
        public ProxyRotationStrategy RotationStrategy { get; set; } = ProxyRotationStrategy.RoundRobin;
        public int TestIntervalMinutes { get; set; } = 30;
    }

    public class StorageConfig
    {
        public string Type { get; set; } = "FileSystem";
        public string FileSystemPath { get; set; } = "crawler_data";
        public string DatabaseConnection { get; set; } = "Data Source=crawler.db";
    }

    public class MonitoringConfig
    {
        public bool EnableMetrics { get; set; } = true;
        public bool EnableTracing { get; set; } = false;
        public int MetricsIntervalSeconds { get; set; } = 30;
    }

    public class DataProcessingConfig
    {
        public bool RemoveDuplicateContent { get; set; } = true;
        public bool RemoveScriptsAndStyles { get; set; } = true;
        public bool NormalizeText { get; set; } = true;
        public int MinContentLength { get; set; } = 100;
    }

    public class SeedsConfig
    {
        public string[] SeedUrls { get; set; } = [];
    }
}