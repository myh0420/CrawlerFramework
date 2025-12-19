// <copyright file="ICrawlerComponent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CrawlerFramework.CrawlerEntity.Models;

namespace CrawlerFramework.CrawlerInterFaces.Interfaces;
/// <summary>
/// 爬虫组件接口
/// </summary>  
public interface ICrawlerComponent
{
    /// <summary>
    /// 初始化组件
    /// </summary>
    Task InitializeAsync();
    /// <summary>
    /// 关闭组件
    /// </summary>  
    Task ShutdownAsync();
}
/// <summary>
/// 下载器组件接口
/// </summary>
public interface IDownloader : ICrawlerComponent
{
    /// <summary>
    /// 异步下载URL内容
    /// </summary>
    /// <param name="request">爬取请求</param>
    /// <returns>下载结果</returns>
    Task<DownloadResult> DownloadAsync(CrawlRequest request);
    /// <summary>
    /// 并发请求数
    /// </summary>
    int ConcurrentRequests { get; set; }
    /// <summary>
    /// 超时时间
    /// </summary>
    TimeSpan Timeout { get; set; }
}
/// <summary>
/// 解析器组件接口
/// </summary>
public interface IParser : ICrawlerComponent
{
    /// <summary>
    /// 异步解析下载结果
    /// </summary>
    /// <param name="downloadResult">下载结果</param>
    /// <returns>解析结果</returns>
    Task<ParseResult> ParseAsync(DownloadResult downloadResult);
    /// <summary>
    /// 添加内容提取器
    /// </summary>
    /// <param name="name">提取器名称</param>
    /// <param name="extractor">内容提取器</param>
    void AddExtractor(string name, IContentExtractor extractor);
}
/// <summary>
/// 调度器组件接口
/// </summary>
public interface IScheduler : ICrawlerComponent
{
    /// <summary>
    /// 异步添加爬取请求
    /// </summary>
    /// <param name="request">爬取请求</param>
    /// <returns>是否成功添加</returns>
    Task<bool> AddUrlAsync(CrawlRequest request);
    /// <summary>
    /// 异步添加多个爬取请求
    /// </summary>
    /// <param name="requests">爬取请求集合</param>
    /// <returns>成功添加的请求数量</returns>
    Task<int> AddUrlsAsync(IEnumerable<CrawlRequest> requests);
    /// <summary>
    /// 异步获取下一个爬取请求
    /// </summary>
    /// <returns>下一个爬取请求</returns>
    Task<CrawlRequest?> GetNextAsync();
    /// <summary>
    /// 等待队列中的请求数量
    /// </summary>
    int QueuedCount { get; }
    /// <summary>
    /// 已处理的请求数量
    /// </summary>
    int ProcessedCount { get; }
    /// <summary>
        /// 处理过程中的错误数量
        /// </summary>
        int ErrorCount { get; }

        /// <summary>
        /// 获取域名统计信息
        /// </summary>
        /// <returns>域名统计信息字典</returns>
        Task<IDictionary<string, DomainStatistics>> GetDomainStatisticsAsync();
    /// <summary>
    /// 记录域名的下载性能数据
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="downloadTimeMs">下载时间（毫秒）</param>
    /// <param name="isSuccess">是否下载成功</param>
    void RecordDomainPerformance(string domain, long downloadTimeMs, bool isSuccess);
}

//public interface IStorage : ICrawlerComponent
//{
//    Task SaveContentAsync(CrawlResult result);
//    Task SaveMetadataAsync(CrawlMetadata metadata);
//}