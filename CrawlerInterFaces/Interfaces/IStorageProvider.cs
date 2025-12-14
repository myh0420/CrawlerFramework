// CrawlerInterFaces/Interfaces/IStorageProvider.cs

using CrawlerEntity.Models;

namespace CrawlerInterFaces.Interfaces;
/// <summary>
/// 存储提供程序接口
/// </summary>
public interface IStorageProvider : ICrawlerComponent
{
    /// <summary>
    /// 异步保存爬取结果
    /// </summary>
    /// <param name="result">爬取结果</param>
    /// <returns>已完成任务</returns>
    Task SaveAsync(CrawlResult result);
    /// <summary>
    /// 异步获取指定域名的爬取结果
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="limit">最大返回数量，默认值为100</param>
    /// <returns>爬取结果列表</returns>
    Task<IEnumerable<CrawlResult>> GetByDomainAsync(string domain, int limit = 100);
    /// <summary>
    /// 异步获取指定URL的爬取结果
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>爬取结果</returns>
    Task<CrawlResult?> GetByUrlAsync(string url);
    /// <summary>
    /// 异步获取总记录数
    /// </summary>
    /// <returns>总记录数</returns>
    Task<long> GetTotalCountAsync();
    /// <summary>
    /// 异步删除指定URL的爬取结果
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>已完成任务</returns>
    Task<bool> DeleteAsync(string url);

    /// <summary>
    /// 异步获取爬取统计信息
    /// </summary>
    /// <returns>爬取统计信息</returns>
    Task<CrawlStatistics> GetStatisticsAsync();
    /// <summary>
    /// 异步清除所有数据
    /// </summary>
    /// <returns>已完成任务</returns>
    Task ClearAllAsync();
    /// <summary>
    /// 异步备份数据
    /// </summary>
    /// <param name="backupPath">备份路径</param>
    /// <returns>已完成任务</returns>
    Task BackupAsync(string backupPath);
}
/// <summary>
/// 元数据存储接口
/// </summary>
public interface IMetadataStore
{
    /// <summary>
    /// 异步保存爬取状态
    /// </summary>
    /// <param name="state">爬取状态</param>
    /// <returns>已完成任务</returns>
    Task SaveCrawlStateAsync(CrawlState state);
    /// <summary>
    /// 异步获取指定作业ID的爬取状态
    /// </summary>
    /// <param name="jobId">作业ID</param>
    /// <returns>爬取状态</returns>
    Task<CrawlState?> GetCrawlStateAsync(string? jobId);
    /// <summary>
    /// 异步保存URL状态
    /// </summary>
    /// <param name="state">URL状态</param>
    /// <returns>已完成任务</returns>
    Task SaveUrlStateAsync(UrlState state);
    /// <summary>
    /// 异步获取指定URL的URL状态
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>URL状态</returns>
    Task<UrlState?> GetUrlStateAsync(string url);
}
