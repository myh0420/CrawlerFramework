// CrawlerCore/Metrics/CrawlerMetrics.cs
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CrawlerCore.Metrics
{
    /// <summary>
    /// 指标收集
    /// </summary>
    public class CrawlerMetrics : IDisposable
    {
        private readonly Meter _meter;
        private readonly Counter<long> _urlsProcessed;
        private readonly Counter<long> _urlsFailed;
        private readonly Histogram<double> _downloadDuration;
        private readonly ObservableGauge<int> _queueSize;
        private readonly Counter<long> _bytesDownloaded;

        public CrawlerMetrics(string serviceName = "Crawler")
        {
            _meter = new Meter(serviceName);
            
            _urlsProcessed = _meter.CreateCounter<long>(
                "crawler_urls_processed",
                "count",
                "Number of URLs successfully processed");
            
            _urlsFailed = _meter.CreateCounter<long>(
                "crawler_urls_failed", 
                "count",
                "Number of URLs that failed to process");
            
            _downloadDuration = _meter.CreateHistogram<double>(
                "crawler_download_duration",
                "milliseconds",
                "Duration of download operations");
            
            _bytesDownloaded = _meter.CreateCounter<long>(
                "crawler_bytes_downloaded",
                "bytes",
                "Total bytes downloaded");
            
            _queueSize = _meter.CreateObservableGauge<int>(
                "crawler_queue_size",
                () => GetQueueSize(),
                "items",
                "Current size of the URL queue");
        }

        public void RecordUrlProcessed(string domain, int statusCode, long bytes, double durationMs)
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "status_code", statusCode }
            };

            _urlsProcessed.Add(1, tags);
            _bytesDownloaded.Add(bytes, tags);
            _downloadDuration.Record(durationMs, tags);
        }

        public void RecordUrlFailed(string domain, string errorType)
        {
            var tags = new TagList
            {
                { "domain", domain },
                { "error_type", errorType }
            };

            _urlsFailed.Add(1, tags);
        }

        private static int GetQueueSize()
        {
            // 从调度器获取队列大小
            return 0; // 实际实现中会返回真实值
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _meter?.Dispose();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}