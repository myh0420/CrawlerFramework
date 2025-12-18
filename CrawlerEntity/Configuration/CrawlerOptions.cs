// CrawlerEntity/Configuration/CrawlerOptions.cs
namespace  CrawlerFramework.CrawlerEntity.Configuration
{
    /// <summary>
    /// 爬虫配置选项
    /// </summary>
    public class CrawlerOptions
    {
        /// <summary>
        /// 存储选项
        /// </summary>
        public StorageOptions Storage { get; set; } = new StorageOptions();
        /// <summary>
        /// 下载选项
        /// </summary>
        public DownloadOptions Download { get; set; } = new DownloadOptions();
        /// <summary>
        /// 解析器选项
        /// </summary>
        public ParserOptions Parser { get; set; } = new ParserOptions();
    }

    /// <summary>
    /// 存储类型
    /// </summary>
    public class StorageOptions
    {
        /// <summary>
        /// 存储类型，默认值为"FileSystem"
        /// </summary>
        public string Type { get; set; } = "FileSystem";
        /// <summary>
        /// 连接字符串，用于数据库存储
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;
        /// <summary>
        /// 基础目录，用于文件系统存储
        /// </summary>
        public string BaseDirectory { get; set; } = "crawler_data";
    }

    /// <summary>
    /// 下载选项
    /// </summary>
    public class DownloadOptions
    {
        /// <summary>
        /// 最大并发请求数，默认值为10
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 10;
        /// <summary>
        /// 超时时间（秒），默认值为30
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        /// <summary>
        /// 是否使用代理，默认值为false
        /// </summary>
        public bool UseProxies { get; set; } = false;
        /// <summary>
        /// 代理列表，用于下载时轮换代理
        /// </summary>
        public string[] ProxyList { get; set; } = [];
    }

    /// <summary>
    /// 解析器选项
    /// </summary>
    public class ParserOptions
    {
        /// <summary>
        /// 是否提取图片，默认值为true
        /// </summary>
        public bool ExtractImages { get; set; } = true;
        /// <summary>
        /// 是否提取元数据，默认值为true
        /// </summary>
        public bool ExtractMetadata { get; set; } = true;
        /// <summary>
        /// 每个页面最大提取链接数，默认值为100
        /// </summary>
        public int MaxLinksPerPage { get; set; } = 100;
    }
}