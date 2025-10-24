// CrawlerEntity/Configuration/CrawlerOptions.cs
namespace CrawlerEntity.Configuration
{
    /// <summary>
    /// 爬虫配置选项
    /// </summary>
    public class CrawlerOptions
    {
        public StorageOptions Storage { get; set; } = new StorageOptions();
        public DownloadOptions Download { get; set; } = new DownloadOptions();
        public ParserOptions Parser { get; set; } = new ParserOptions();
    }

    /// <summary>
    /// 存储选项
    /// </summary>
    public class StorageOptions
    {
        public string Type { get; set; } = "FileSystem";
        public string ConnectionString { get; set; } = string.Empty;
        public string BaseDirectory { get; set; } = "crawler_data";
    }

    /// <summary>
    /// 下载选项
    /// </summary>
    public class DownloadOptions
    {
        public int MaxConcurrentRequests { get; set; } = 10;
        public int TimeoutSeconds { get; set; } = 30;
        public bool UseProxies { get; set; } = false;
        public string[] ProxyList { get; set; } = [];
    }

    /// <summary>
    /// 解析器选项
    /// </summary>
    public class ParserOptions
    {
        public bool ExtractImages { get; set; } = true;
        public bool ExtractMetadata { get; set; } = true;
        public int MaxLinksPerPage { get; set; } = 100;
    }
}