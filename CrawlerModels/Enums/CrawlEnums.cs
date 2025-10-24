// CrawlerEntity/Enums/CrawlEnums.cs
namespace CrawlerEntity.Enums
{
    /// <summary>
    /// HTTP爬取方法
    /// </summary>
    public enum CrawlMethod
    {
        GET,
        POST,
        HEAD,
        PUT,
        DELETE
    }

    /// <summary>
    /// 存储类型
    /// </summary>
    public enum StorageType
    {
        FileSystem,
        Database,
        CloudStorage,
        Hybrid
    }

    /// <summary>
    /// 内容类型
    /// </summary>
    public enum ContentType
    {
        Html,
        Text,
        Json,
        Xml,
        Binary,
        Unknown
    }

    /// <summary>
    /// 爬虫状态
    /// </summary>
    public enum CrawlerStatus
    {
        Idle,
        Running,
        Paused,
        Stopped,
        Completed,
        Error
    }

    /// <summary>
    /// URL优先级
    /// </summary>
    public enum UrlPriority
    {
        Lowest = 1,
        Low = 2,
        Normal = 5,
        High = 8,
        Highest = 10
    }
}