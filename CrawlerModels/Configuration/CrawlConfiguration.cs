// CrawlerEntity/Configuration/CrawlConfiguration.cs
using CrawlerEntity.Enums;

namespace CrawlerEntity.Configuration;
public class CrawlConfiguration
{
    public int MaxConcurrentTasks { get; set; } = 10;
    public int MaxDepth { get; set; } = 3;
    public TimeSpan RequestDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public int MaxPages { get; set; } = 1000;
    public string[] AllowedDomains { get; set; } = [];
    public string[] BlockedPatterns { get; set; } = [];
    public bool RespectRobotsTxt { get; set; } = true;
    public bool FollowRedirects { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    
    // 存储配置
    public StorageType StorageType { get; set; } = StorageType.FileSystem;
    public string ConnectionString { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = "crawled_data";
}