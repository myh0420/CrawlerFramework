
// CrawlerInterFaces/Interfaces/IDomainDelayManager.cs
using System;
using System.Threading.Tasks;

namespace CrawlerInterFaces.Interfaces
{
    /// <summary>
    /// 域名延迟管理接口
    /// </summary>
    public interface IDomainDelayManager
    {
        /// <summary>
        /// 检查是否可以处理指定域名的请求
        /// </summary>
        Task<bool> CanProcessAsync(string domain);

        /// <summary>
        /// 检查是否可以处理指定域名和请求类型的请求
        /// </summary>
        Task<bool> CanProcessAsync(string domain, string requestType);

        /// <summary>
        /// 记录域名访问时间
        /// </summary>
        Task RecordAccessAsync(string domain);

        /// <summary>
        /// 记录域名和请求类型的访问时间
        /// </summary>
        Task RecordAccessAsync(string domain, string requestType);

        /// <summary>
        /// 设置域名延迟
        /// </summary>
        void SetDelay(string domain, TimeSpan delay);

        /// <summary>
        /// 设置域名特定请求类型的延迟
        /// </summary>
        void SetDelay(string domain, string requestType, TimeSpan delay);

        /// <summary>
        /// 动态增加域名延迟
        /// </summary>
        void IncreaseDelay(string domain);

        /// <summary>
        /// 动态减少域名延迟（恢复）
        /// </summary>
        void DecreaseDelay(string domain);
    }
}