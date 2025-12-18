// CrawlerInterFaces/Interfaces/IUrlFilter.cs
namespace CrawlerFramework.CrawlerInterFaces.Interfaces
{
    /// <summary>
    /// URL过滤器接口
    /// </summary>
    public interface IUrlFilter
    {
        /// <summary>
        /// 检查URL是否允许爬取
        /// </summary>
        bool IsAllowed(string url);

        /// <summary>
        /// 添加允许的域名
        /// </summary>
        void AddAllowedDomain(string domain);

        /// <summary>
        /// 添加阻止的模式
        /// </summary>
        void AddBlockedPattern(string pattern);
    }
}

