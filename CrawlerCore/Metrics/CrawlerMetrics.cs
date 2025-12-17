// <copyright file="CrawlerMetrics.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Metrics
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// 指标收集.
    /// </summary>
    public class CrawlerMetrics : IDisposable
    {
        /// <summary>
        /// 指标收集器.
        /// </summary>
        private readonly Meter meter;

        /// <summary>
        /// 成功处理的URL数量.
        /// </summary>
        private readonly Counter<long> urlsProcessed;

        /// <summary>
        /// 失败处理的URL数量.
        /// </summary>
        private readonly Counter<long> urlsFailed;

        /// <summary>
        /// 下载持续时间直方图.
        /// </summary>
        private readonly Histogram<double> downloadDuration;

        /// <summary>
        /// 解析持续时间直方图.
        /// </summary>
        private readonly Histogram<double> parseDuration;

        /// <summary>
        /// 存储持续时间直方图.
        /// </summary>
        private readonly Histogram<double> storageDuration;

        /// <summary>
        /// URL队列大小可观测量.
        /// </summary>
        private readonly ObservableGauge<int> queueSize;

        /// <summary>
        /// 内存使用量可观测量.
        /// </summary>
        private readonly ObservableGauge<double> memoryUsage;

        /// <summary>
        /// 下载的字节数.
        /// </summary>
        private readonly Counter<long> bytesDownloaded;

        /// <summary>
        /// 错误数.
        /// </summary>
        private readonly Counter<long> errors;

        /// <summary>
        /// 重试次数.
        /// </summary>
        private readonly Counter<long> retryAttempts;

        /// <summary>
        /// 代理切换次数.
        /// </summary>
        private readonly Counter<long> proxySwitches;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrawlerMetrics"/> class.
        /// 构造函数.
        /// </summary>
        /// <param name="serviceName">服务名称.</param>
        public CrawlerMetrics(string serviceName = "Crawler")
        {
            this.meter = new Meter(serviceName);

            this.urlsProcessed = this.meter.CreateCounter<long>(
                "crawler_urls_processed",
                "count",
                "Number of URLs successfully processed");

            this.urlsFailed = this.meter.CreateCounter<long>(
                "crawler_urls_failed",
                "count",
                "Number of URLs that failed to process");

            this.downloadDuration = this.meter.CreateHistogram<double>(
                "crawler_download_duration",
                "milliseconds",
                "Duration of download operations");

            this.bytesDownloaded = this.meter.CreateCounter<long>(
                "crawler_bytes_downloaded",
                "bytes",
                "Total bytes downloaded");

            this.queueSize = this.meter.CreateObservableGauge<int>(
                "crawler_queue_size",
                () => GetQueueSize(),
                "items",
                "Current size of the URL queue");

            this.errors = this.meter.CreateCounter<long>(
                "crawler_errors",
                "count",
                "Number of errors encountered");

            this.retryAttempts = this.meter.CreateCounter<long>(
                "crawler_retry_attempts",
                "count",
                "Number of retry attempts made");

            this.proxySwitches = this.meter.CreateCounter<long>(
                "crawler_proxy_switches",
                "count",
                "Number of times proxy was switched due to errors");

            this.parseDuration = this.meter.CreateHistogram<double>(
                "crawler_parse_duration",
                "milliseconds",
                "Duration of parsing operations");

            this.storageDuration = this.meter.CreateHistogram<double>(
                "crawler_storage_duration",
                "milliseconds",
                "Duration of storage operations");

            this.memoryUsage = this.meter.CreateObservableGauge<double>(
                "crawler_memory_usage",
                () => GetMemoryUsage(),
                "megabytes",
                "Current memory usage of the crawler process");
        }

        /// <summary>
        /// 记录成功处理的URL.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="statusCode">HTTP状态码.</param>
        /// <param name="bytes">下载的字节数.</param>
        /// <param name="durationMs">下载持续时间（毫秒）.</param>
        /// <param name="parseDurationMs">解析持续时间（毫秒）.</param>
        /// <param name="storageDurationMs">存储持续时间（毫秒）.</param>
        public void RecordUrlProcessed(string domain, int statusCode, long bytes, double durationMs, double parseDurationMs, double storageDurationMs)
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "status_code", statusCode },
            };

            this.urlsProcessed.Add(1, tags);
            this.bytesDownloaded.Add(bytes, tags);
            this.downloadDuration.Record(durationMs, tags);
            this.parseDuration.Record(parseDurationMs, tags);
            this.storageDuration.Record(storageDurationMs, tags);
        }

        /// <summary>
        /// 记录失败处理的URL.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="errorType">错误类型.</param>
        public void RecordUrlFailed(string domain, string errorType)
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "error_type", errorType },
            };

            this.urlsFailed.Add(1, tags);
            this.errors.Add(1, tags);
        }

        /// <summary>
        /// 记录错误.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="errorType">错误类型.</param>
        /// <param name="errorCode">错误代码.</param>
        public void RecordError(string domain, string errorType, string errorCode = "")
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "error_type", errorType },
                { "error_code", errorCode },
            };

            this.errors.Add(1, tags);
        }

        /// <summary>
        /// 记录重试尝试.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="errorType">错误类型.</param>
        public void RecordRetryAttempt(string domain, string errorType)
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "error_type", errorType },
            };

            this.retryAttempts.Add(1, tags);
        }

        /// <summary>
        /// 记录代理切换.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="reason">切换原因.</param>
        public void RecordProxySwitch(string domain, string reason)
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "reason", reason },
            };

            this.proxySwitches.Add(1, tags);
        }

        /// <summary>
        /// 记录解析持续时间.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="durationMs">解析持续时间（毫秒）.</param>
        /// <param name="contentType">内容类型.</param>
        public void RecordParseDuration(string domain, double durationMs, string contentType = "html")
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "content_type", contentType },
            };

            this.parseDuration.Record(durationMs, tags);
        }

        /// <summary>
        /// 记录存储持续时间.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="durationMs">存储持续时间（毫秒）.</param>
        /// <param name="storageType">存储类型.</param>
        public void RecordStorageDuration(string domain, double durationMs, string storageType = "database")
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "storage_type", storageType },
            };

            this.storageDuration.Record(durationMs, tags);
        }

        // /// <summary>
        // /// 记录队列大小.
        // /// </summary>
        // /// <param name="domain">域名.</param>
        // /// <param name="queueSize">队列大小.</param>
        // public void RecordQueueSize(string domain, int queueSize)
        // {
        //     var tags = new TagList
        //     {
        //         { "domain", domain },
        //     };

        // this.queueSize.Add(queueSize, tags);
        // }

        /// <summary>
        /// 释放资源.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源.
        /// </summary>
        /// <param name="disposing">是否释放托管资源.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.meter?.Dispose();
            }
        }

        /// <summary>
        /// 获取当前队列大小.
        /// </summary>
        /// <returns>队列大小.</returns>
        private static int GetQueueSize()
        {
            // 从调度器获取队列大小
            return 0; // 实际实现中会返回真实值
        }

        /// <summary>
        /// 获取当前内存使用情况.
        /// </summary>
        /// <returns>内存使用情况（MB）.</returns>
        private static double GetMemoryUsage()
        {
            // 获取当前进程的内存使用情况
            using var process = Process.GetCurrentProcess();

            // 转换为兆字节
            return process.WorkingSet64 / (1024.0 * 1024.0);
        }
    }
}