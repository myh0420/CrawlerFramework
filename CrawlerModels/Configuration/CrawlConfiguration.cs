// CrawlerEntity/Configuration/CrawlConfiguration.cs
using CrawlerEntity.Enums;

namespace CrawlerEntity.Configuration;
/// <summary>
/// 爬取配置
/// </summary>
public class CrawlConfiguration
{
    /// <summary>
    /// 最大并发任务数，默认值为10
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 10;
    /// <summary>
    /// 最大深度，默认值为3
    /// </summary>
    public int MaxDepth { get; set; } = 3;
    /// <summary>
    /// 请求延迟，默认值为500毫秒
    /// </summary>
    public TimeSpan RequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    /// <summary>
    /// 最大页面数，默认值为1000
    /// </summary>
    public int MaxPages { get; set; } = 1000;
    /// <summary>
    /// 允许的域名列表，默认值为空数组
    /// </summary>
    public string[] AllowedDomains { get; set; } = [];
    /// <summary>
    /// 阻塞的URL模式列表，默认值为空数组
    /// </summary>
    public string[] BlockedPatterns { get; set; } = [];
    /// <summary>
    /// 是否尊重robots.txt文件，默认值为true
    /// </summary>
    public bool RespectRobotsTxt { get; set; } = true;
    /// <summary>
    /// 是否遵循重定向，默认值为true
    /// </summary>
    public bool FollowRedirects { get; set; } = true;
    /// <summary>
    /// 请求超时时间，默认值为30秒
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    // 存储配置
    /// <summary>
    /// 存储类型，默认值为"FileSystem"
    /// </summary>
    public StorageType StorageType { get; set; } = StorageType.FileSystem;
    /// <summary>
    /// 连接字符串，用于数据库存储
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>
    /// 输出目录，用于文件系统存储
    /// </summary>
    public string OutputDirectory { get; set; } = "crawled_data";
    /// <summary>
    /// 种子URL列表，用于开始爬取
    /// </summary>
    public List<string> SeedUrls { get; set; } = [];
}