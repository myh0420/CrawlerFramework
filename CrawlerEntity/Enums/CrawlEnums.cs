// CrawlerEntity/Enums/CrawlEnums.cs
namespace  CrawlerFramework.CrawlerEntity.Enums
{
    /// <summary>
    /// HTTP爬取方法
    /// </summary>
    public enum CrawlMethod
    {
        /// <summary>
        /// GET方法
        /// </summary>
        GET,
        /// <summary>
        /// POST方法
        /// </summary>
        POST,
        /// <summary>
        /// HEAD方法
        /// </summary>
        HEAD,
        /// <summary>
        /// PUT方法
        /// </summary>
        PUT,
        /// <summary>
        /// DELETE方法
        /// </summary>
        DELETE
    }

    /// <summary>
    /// 存储类型
    /// </summary>
    public enum StorageType
    {
        /// <summary>
        /// 文件系统
        /// </summary>
        FileSystem,
        /// <summary>
        /// 数据库
        /// </summary>
        Database,
        /// <summary>
        /// 云存储
        /// </summary>
        CloudStorage,
        /// <summary>
        /// 混合存储
        /// </summary>
        Hybrid
    }

    /// <summary>
    /// 内容类型
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// HTML内容
        /// </summary>
        Html,
        /// <summary>
        /// 文本内容
        /// </summary>
        Text,
        /// <summary>
        /// JSON内容
        /// </summary>
        Json,
        /// <summary>
        /// XML内容
        /// </summary>
        Xml,
        /// <summary>
        /// 二进制内容
        /// </summary>
        Binary,
        /// <summary>
        /// 未知内容类型
        /// </summary>
        Unknown
    }

    /// <summary>
    /// 爬虫状态
    /// </summary>
    public enum CrawlerStatus
    {
        /// <summary>
        /// 空闲状态
        /// </summary>
        Idle,
        /// <summary>
        /// 运行状态
        /// </summary>
        Running,
        /// <summary>
        /// 暂停状态
        /// </summary>
        Paused,
        /// <summary>
        /// 停止状态
        /// </summary>
        Stopping,
        /// <summary>
        /// 已停止状态
        /// </summary>
        Stopped,
        /// <summary>
        /// 完成状态
        /// </summary>
        Completed,
        /// <summary>
        /// 错误状态
        /// </summary>
        Error
    }

    /// <summary>
    /// URL优先级
    /// </summary>
    public enum UrlPriority
    {
        /// <summary>
        /// 最低优先级
        /// </summary>
        Lowest = 1,
        /// <summary>
        /// 低优先级
        /// </summary>
        Low = 2,
        /// <summary>
        /// 正常优先级
        /// </summary>
        Normal = 5,
        /// <summary>
        /// 高优先级
        /// </summary>
        High = 8,
        /// <summary>
        /// 最高优先级
        /// </summary>
        Highest = 10
    }
}