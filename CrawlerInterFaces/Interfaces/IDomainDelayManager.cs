
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
        /// 记录域名访问时间
        /// </summary>
        Task RecordAccessAsync(string domain);

        /// <summary>
        /// 设置域名延迟
        /// </summary>
        void SetDelay(string domain, TimeSpan delay);
    }
}