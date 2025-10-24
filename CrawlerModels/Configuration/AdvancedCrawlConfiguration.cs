// CrawlerEntity/Configuration/AdvancedCrawlConfiguration.cs
using System;
using System.Collections.Generic;

namespace CrawlerEntity.Configuration;

/// <summary>
/// 高级配置管理
/// </summary>
public class AdvancedCrawlConfiguration : CrawlConfiguration
{
    // 性能设置
    public int MemoryLimitMB { get; set; } = 500;
    public int MaxQueueSize { get; set; } = 10000;
    public bool EnableCompression { get; set; } = true;
    
    // 重试设置
    public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();
    
    // 代理设置
    public ProxySettings ProxySettings { get; set; } = new ProxySettings();
    
    // 缓存设置
    public CacheSettings CacheSettings { get; set; } = new CacheSettings();
    
    // 监控设置
    public MonitoringSettings MonitoringSettings { get; set; } = new MonitoringSettings();
    
    // 高级爬取设置
    public bool EnableJavascript { get; set; } = false;
    public string[] JavascriptWaitForSelectors { get; set; } = [];
    public int JavascriptTimeoutSeconds { get; set; } = 30;
    
    // 数据清洗设置
    public DataCleaningSettings DataCleaningSettings { get; set; } = new DataCleaningSettings();
    public bool EnableAntiBotDetection { get; set; }
}

public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    public string[] RetryableStatusCodes { get; set; } = [ "5xx", "429", "408" ];
}

public class ProxySettings
{
    public bool Enabled { get; set; } = false;
    public string[] ProxyUrls { get; set; } = [];
    public ProxyRotationStrategy RotationStrategy { get; set; } = ProxyRotationStrategy.RoundRobin;
    public int ProxyTestIntervalMinutes { get; set; } = 30;
}

public class CacheSettings
{
    public bool Enabled { get; set; } = true;
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);
    public int MaxCacheSizeMB { get; set; } = 100;
}

public class MonitoringSettings
{
    public bool EnableMetrics { get; set; } = true;
    public bool EnableTracing { get; set; } = false;
    public string MetricsEndpoint { get; set; } = "/metrics";
    public int MetricsIntervalSeconds { get; set; } = 30;
}

public class DataCleaningSettings
{
    public bool RemoveDuplicateContent { get; set; } = true;
    public bool RemoveScriptsAndStyles { get; set; } = true;
    public bool NormalizeText { get; set; } = true;
    public int MinContentLength { get; set; } = 100;
}

public enum ProxyRotationStrategy
{
    RoundRobin,
    Random,
    ByLatency,
    Sticky
}