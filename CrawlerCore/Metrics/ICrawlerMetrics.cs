// <copyright file="ICrawlerMetrics.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore.Metrics
{
    using System;

    /// <summary>
    /// 爬虫指标收集服务接口，定义了收集和记录爬虫性能指标的方法.
    /// </summary>
    public interface ICrawlerMetrics : IDisposable
    {
        /// <summary>
        /// 记录成功处理的URL.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="statusCode">HTTP状态码.</param>
        /// <param name="bytes">下载的字节数.</param>
        /// <param name="durationMs">下载持续时间（毫秒）.</param>
        /// <param name="parseDurationMs">解析持续时间（毫秒）.</param>
        /// <param name="storageDurationMs">存储持续时间（毫秒）.</param>
        void RecordUrlProcessed(string domain, int statusCode, long bytes, double durationMs, double parseDurationMs, double storageDurationMs);

        /// <summary>
        /// 记录失败处理的URL.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="errorType">错误类型.</param>
        void RecordUrlFailed(string domain, string errorType = "unknown");

        /// <summary>
        /// 记录失败处理的URL（简化版本）.
        /// </summary>
        /// <param name="url">URL地址.</param>
        void RecordUrlFailed(string url);

        /// <summary>
        /// 记录完整的爬取结果，包括各个阶段的详细指标.
        /// </summary>
        /// <param name="url">爬取的URL地址.</param>
        /// <param name="success">爬取是否成功.</param>
        /// <param name="domain">域名.</param>
        /// <param name="statusCode">HTTP状态码.</param>
        /// <param name="contentLength">内容长度（字节数）.</param>
        /// <param name="downloadDurationMs">下载持续时间（毫秒）.</param>
        /// <param name="parseDurationMs">解析持续时间（毫秒）.</param>
        /// <param name="storageDurationMs">存储持续时间（毫秒）.</param>
        /// <param name="errorType">错误类型（仅在失败时使用）.</param>
        void RecordCrawlResult(
            string url,
            bool success,
            string domain,
            int statusCode,
            long contentLength,
            double downloadDurationMs,
            double parseDurationMs,
            double storageDurationMs,
            string errorType = "unknown");

        /// <summary>
        /// 记录下载的字节数.
        /// </summary>
        /// <param name="bytes">下载的字节数.</param>
        /// <param name="domain">域名.</param>
        void RecordBytesDownloaded(long bytes, string domain = "unknown");

        /// <summary>
        /// 记录HTTP状态码.
        /// </summary>
        /// <param name="statusCode">HTTP状态码.</param>
        /// <param name="domain">域名.</param>
        void RecordHttpStatusCode(int statusCode, string domain = "unknown");

        /// <summary>
        /// 记录错误.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="errorType">错误类型.</param>
        /// <param name="errorCode">错误代码.</param>
        void RecordError(string domain, string errorType, string errorCode = "");

        /// <summary>
        /// 记录重试尝试.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="errorType">错误类型.</param>
        void RecordRetryAttempt(string domain, string errorType);

        /// <summary>
        /// 记录代理切换.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="reason">切换原因.</param>
        void RecordProxySwitch(string domain, string reason);

        /// <summary>
        /// 获取平均下载时间（毫秒）.
        /// </summary>
        /// <returns>平均下载时间（毫秒）.</returns>
        double GetAverageDownloadTime();

        /// <summary>
        /// 获取总下载字节数.
        /// </summary>
        /// <returns>总下载字节数.</returns>
        long GetTotalDownloadedBytes();

        /// <summary>
        /// 记录下载持续时间.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="durationMs">下载持续时间（毫秒）.</param>
        void RecordDownloadDuration(string domain, double durationMs);

        /// <summary>
        /// 记录解析持续时间.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="durationMs">解析持续时间（毫秒）.</param>
        /// <param name="contentType">内容类型.</param>
        void RecordParseDuration(string domain, double durationMs, string contentType = "html");

        /// <summary>
        /// 记录存储持续时间.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="durationMs">存储持续时间（毫秒）.</param>
        /// <param name="storageType">存储类型.</param>
        void RecordStorageDuration(string domain, double durationMs, string storageType = "database");
    }
}