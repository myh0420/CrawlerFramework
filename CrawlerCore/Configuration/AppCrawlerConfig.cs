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
        /// <summary>
        /// 爬虫配置主节
        /// </summary>
        [JsonPropertyName("CrawlerConfig")]
        public CrawlerConfigSection CrawlerConfig { get; set; } = new CrawlerConfigSection();
        
        /// <summary>
        /// 日志配置节
        /// </summary>
        [JsonPropertyName("Logging")]
        public LoggingConfig Logging { get; set; } = new LoggingConfig();

        /// <summary>
        /// 允许的主机配置
        /// </summary>
        [JsonPropertyName("AllowedHosts")]
        public string AllowedHosts { get; set; } = "*";
    }

    /// <summary>
    /// 爬虫配置主节
    /// </summary>
    public class CrawlerConfigSection
    {
        /// <summary>
        /// 基础配置节
        /// </summary>
        [JsonPropertyName("Basic")]
        public BasicConfig Basic { get; set; } = new BasicConfig();
        
        /// <summary>
        /// 域名配置节
        /// </summary>
        [JsonPropertyName("Domains")]
        public DomainsConfig Domains { get; set; } = new DomainsConfig();

        /// <summary>
        /// 性能配置节
        /// </summary>
        [JsonPropertyName("Performance")]
        public PerformanceConfig Performance { get; set; } = new PerformanceConfig();
        
        /// <summary>
        /// 反爬虫配置节
        /// </summary>
        [JsonPropertyName("AntiBot")]
        public AntiBotConfig AntiBot { get; set; } = new AntiBotConfig();
        
        /// <summary>
        /// 代理配置节
        /// </summary>
        [JsonPropertyName("Proxy")]
        public ProxyConfig Proxy { get; set; } = new ProxyConfig();
        
        /// <summary>
        /// 存储配置节
        /// </summary>
        [JsonPropertyName("Storage")]
        public StorageConfig Storage { get; set; } = new StorageConfig();
        
        /// <summary>
        /// 监控配置节
        /// </summary>
        [JsonPropertyName("Monitoring")]
        public MonitoringConfig Monitoring { get; set; } = new MonitoringConfig();
        
        /// <summary>
        /// 数据处理配置节
        /// </summary>
        [JsonPropertyName("DataProcessing")]
        public DataProcessingConfig DataProcessing { get; set; } = new DataProcessingConfig();

        /// <summary>
        /// 种子配置节
        /// </summary>  
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
        /// <summary>
        /// 最大并发任务数
        /// </summary>
        public int MaxConcurrentTasks { get; set; } = 10;
        
        /// <summary>
        /// 最大深度
        /// </summary>
        public int MaxDepth { get; set; } = 3;
        
        /// <summary>
        /// 最大页面数
        /// </summary>
        public int MaxPages { get; set; } = 1000;
        
        /// <summary>
        /// 请求延迟
        /// </summary>
        public TimeSpan RequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);
        
        /// <summary>
        /// 请求超时时间
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// 是否遵守 robots.txt
        /// </summary>
        public bool RespectRobotsTxt { get; set; } = true;
        
        /// <summary>
        /// 是否遵循重定向
        /// </summary>
        public bool FollowRedirects { get; set; } = true;
    }
    /// <summary>
    /// 日志配置节
    /// </summary>
    public class LoggingConfig {
        /// <summary>
        /// 日志级别配置
        /// </summary>
        [JsonPropertyName("LogLevel")]
        public LogConfigLevel LogLevel { get; set; } = new();
    }
    /// <summary>
    /// 日志级别配置类
    /// </summary>
    public class LogConfigLevel
    {
        /// <summary>
        /// 默认日志级别
        /// </summary>
        public string Default { get; set; } = "Information";
        /// <summary>
        /// Microsoft 日志级别
        /// </summary>
        public string Microsoft { get; set; } = "Warning";
        /// <summary>
        /// System 日志级别
        /// </summary>
        public string System { get; set; } = "Warning";
        /// <summary>
        /// Microsoft.AspNetCore 日志级别
        /// </summary>
        [JsonPropertyName("Microsoft.AspNetCore")]
        public string MicrosoftAspNetCore { get; set; } = "Warning";
    }
    /// <summary>
    /// 域名配置节
    /// </summary>
    public class DomainsConfig
    {
        /// <summary>
        /// 允许的域名列表
        /// </summary>
        public string[] AllowedDomains { get; set; } = [];
        /// <summary>
        /// 阻塞的域名模式列表
        /// </summary>
        public string[] BlockedPatterns { get; set; } = [];
    }
    /// <summary>
    /// 性能配置节
    /// </summary>
    public class PerformanceConfig
    {
        /// <summary>
        /// 内存限制（MB）
        /// </summary>
        public int MemoryLimitMB { get; set; } = 500;
        /// <summary>
        /// 最大队列大小
        /// </summary>
        public int MaxQueueSize { get; set; } = 10000;
        /// <summary>
        /// 是否启用压缩
        /// </summary>
        public bool EnableCompression { get; set; } = true;
    }
    /// <summary>
    /// 反机器人配置节
    /// </summary>
    public class AntiBotConfig
    {
        /// <summary>
        /// 是否启用反机器人检测
        /// </summary>
        public bool EnableDetection { get; set; } = true;
        /// <summary>
        /// 重试策略配置
        /// </summary>
        public RetryPolicyConfig RetryPolicy { get; set; } = new RetryPolicyConfig();
    }
    /// <summary>
    /// 重试策略配置节
    /// </summary>
    public class RetryPolicyConfig
    {
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;
        /// <summary>
        /// 初始延迟时间
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        /// 延迟 multiplier
        /// </summary>
        public double BackoffMultiplier { get; set; } = 2.0;
        /// <summary>
        /// 最大延迟时间
        /// </summary>
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    }
    /// <summary>
    /// 代理配置节
    /// </summary>
    public class ProxyConfig
    {
        /// <summary>
        /// 是否启用代理
        /// </summary>
        public bool Enabled { get; set; } = false;
        /// <summary>
        /// 代理 URL 列表
        /// </summary>
        public string[] ProxyUrls { get; set; } = [];
        /// <summary>
        /// 代理轮换策略
        /// </summary>
        public ProxyRotationStrategy RotationStrategy { get; set; } = ProxyRotationStrategy.RoundRobin;
        /// <summary>
        /// 测试间隔时间（分钟）
        /// </summary>
        public int TestIntervalMinutes { get; set; } = 30;
    }
    /// <summary>
    /// 存储配置节
    /// </summary>
    public class StorageConfig
    {
        /// <summary>
        /// 存储类型（FileSystem 或 Database）
        /// </summary>
        public string Type { get; set; } = "FileSystem";
        /// <summary>
        /// 文件系统存储路径
        /// </summary>
        public string FileSystemPath { get; set; } = "crawler_data";
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string DatabaseConnection { get; set; } = "Data Source=crawler.db";
    }
    /// <summary>
    /// 监控配置节
    /// </summary>
    public class MonitoringConfig
    {
        /// <summary>
        /// 是否启用指标监控
        /// </summary>
        public bool EnableMetrics { get; set; } = true;
        /// <summary>
        /// 是否启用跟踪监控
        /// </summary>
        public bool EnableTracing { get; set; } = false;
        /// <summary>
        /// 指标监控间隔时间（秒）
        /// </summary>
        public int MetricsIntervalSeconds { get; set; } = 30;
    }
    /// <summary>
    /// 数据处理配置节
    /// </summary>
    public class DataProcessingConfig
    {
        /// <summary>
        /// 是否移除重复内容
        /// </summary>
        public bool RemoveDuplicateContent { get; set; } = true;
        /// <summary>
        /// 是否移除脚本和样式
        /// </summary>
        public bool RemoveScriptsAndStyles { get; set; } = true;
        /// <summary>
        /// 是否归一化文本
        /// </summary>
        public bool NormalizeText { get; set; } = true;
        /// <summary>
        /// 最小内容长度
        /// </summary>
        public int MinContentLength { get; set; } = 100;
    }
    /// <summary>
    /// 种子配置节
    /// </summary>
    public class SeedsConfig
    {
        /// <summary>
        /// 种子 URL 列表
        /// </summary>
        public string[] SeedUrls { get; set; } = [];
    }
}