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
    
    // 重试设置
    /// <summary>
    /// 重试策略
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();
    
    // 代理设置
    /// <summary>
    /// 代理设置
    /// </summary>
    public ProxySettings ProxySettings { get; set; } = new ProxySettings();
    
    // 缓存设置
    /// <summary>
    /// 缓存设置
    /// </summary>
    public CacheSettings CacheSettings { get; set; } = new CacheSettings();
    
    // 监控设置
    /// <summary>
    /// 监控设置
    /// </summary>
    public MonitoringSettings MonitoringSettings { get; set; } = new MonitoringSettings();
    
    // 高级爬取设置
    /// <summary>
    /// 是否启用JavaScript渲染
    /// </summary>
    public bool EnableJavascript { get; set; } = false;
    /// <summary>
    /// JavaScript等待选择器数组
    /// </summary>
    public string[] JavascriptWaitForSelectors { get; set; } = [];
    /// <summary>
    /// JavaScript超时时间（秒）
    /// </summary>
    public int JavascriptTimeoutSeconds { get; set; } = 30;
    
    // 数据清洗设置
    /// <summary>
    /// 数据清洗设置
    /// </summary>
    public DataCleaningSettings DataCleaningSettings { get; set; } = new DataCleaningSettings();
    /// <summary>
    /// 是否启用反机器人检测
    /// </summary>
    public bool EnableAntiBotDetection { get; set; }
}
/// <summary>
/// 重试策略
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    /// <summary>
    /// 初始延迟时间（秒）
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>
    /// 退避乘数
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
    /// <summary>
    /// 最大延迟时间（秒）
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    /// <summary>
    /// 可重试的HTTP状态码数组
    /// </summary>
    public string[] RetryableStatusCodes { get; set; } = [ "5xx", "429", "408" ];
}
/// <summary>
/// 代理设置
/// </summary>
public class ProxySettings
{
    /// <summary>
    /// 是否启用代理
    /// </summary>
    public bool Enabled { get; set; } = false;
    /// <summary>
    /// 代理URL数组
    /// </summary>
    public string[] ProxyUrls { get; set; } = [];
    /// <summary>
    /// 代理轮换策略
    /// </summary>
    public ProxyRotationStrategy RotationStrategy { get; set; } = ProxyRotationStrategy.RoundRobin;
    /// <summary>
    /// 代理测试间隔时间（分钟）
    /// </summary>
    public int ProxyTestIntervalMinutes { get; set; } = 30;
}
/// <summary>
/// 缓存设置
/// </summary>
public class CacheSettings
{
    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// 缓存持续时间（小时）
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);
    /// <summary>
    /// 最大缓存大小（MB）
    /// </summary>
    public int MaxCacheSizeMB { get; set; } = 100;
}
/// <summary>
/// 监控设置
/// </summary>
public class MonitoringSettings
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
    /// 指标监控端点
    /// </summary>
    public string MetricsEndpoint { get; set; } = "/metrics";
    /// <summary>
    /// 指标监控间隔时间（秒）
    /// </summary>
    public int MetricsIntervalSeconds { get; set; } = 30;
}
/// <summary>
/// 数据清洗设置
/// </summary>
public class DataCleaningSettings
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
/// 代理轮换策略
/// </summary>
public enum ProxyRotationStrategy
{
    /// <summary>
    /// 循环轮换代理
    /// </summary>
    RoundRobin,
    /// <summary>
    /// 随机轮换代理
    /// </summary>
    Random,
    /// <summary>
    /// 根据延迟时间轮换代理
    /// </summary>
    ByLatency,
    /// <summary>
    /// 粘性代理，保持使用相同代理
    /// </summary>
    Sticky
}