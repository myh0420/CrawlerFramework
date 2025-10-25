// CrawlerDownloader/AdvancedDownloader.cs
using CrawlerCore.AntiBot;
using CrawlerCore.Export;
using CrawlerCore.Metrics;
using CrawlerCore.Retry;
using CrawlerCore.Robots;
using CrawlerCore.Services;
using CrawlerCore.Utils;
//using CrawlerCore.Services;
using CrawlerDownloader.Services;
using CrawlerDownloader.Utils;
using CrawlerEntity.Configuration;
using CrawlerEntity.Enums;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrawlerDownloader
{
    public class AdvancedDownloader : IDownloader
    {
        private readonly SimpleHttpClientManager _httpClientManager;
        private readonly ILogger<AdvancedDownloader> _logger;
        private readonly RotatingUserAgentService? _userAgentService;
        private readonly ProxyManager? _proxyManager;
        private readonly AntiBotDetectionService? _antiBotService;
        private readonly AdaptiveRetryStrategy? _retryStrategy;
        private readonly RobotsTxtParser? _robotsTxtParser;
        private readonly CrawlerMetrics? _metrics;

        private readonly DataExportService? _dataExporter; // 添加这个字段

        private readonly bool _useProxies;

        private readonly AdvancedCrawlConfiguration _config;

        public int ConcurrentRequests { get; set; } = 10;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        private readonly SemaphoreSlim _semaphore;

        // 完整的构造函数
        public AdvancedDownloader(
            ILogger<AdvancedDownloader> logger,
            AdvancedCrawlConfiguration config,
            AntiBotDetectionService? antiBotService = null,
            AdaptiveRetryStrategy? retryStrategy = null,
            RobotsTxtParser? robotsTxtParser = null,
            CrawlerMetrics? metrics = null,
            DataExportService? dataExporter = null, // 添加这个参数
            ProxyManager? proxyManager = null,
            int maxHttpClients = 10)
        {
            _logger = logger;
            _config = config;
            _useProxies = config.ProxySettings?.Enabled ?? false;
            _httpClientManager = new SimpleHttpClientManager(maxHttpClients);
            _userAgentService = new RotatingUserAgentService();
            _proxyManager = proxyManager ?? new ProxyManager();
            _antiBotService = antiBotService ?? new(null);
            _retryStrategy = retryStrategy ?? new(null, config.RetryPolicy.MaxRetries);
            _robotsTxtParser = robotsTxtParser ?? new(null,null);
            _metrics = metrics ?? new();
            _dataExporter = dataExporter; // 初始化这个字段
            _semaphore = new SemaphoreSlim(ConcurrentRequests);

            // 配置代理
            if (_useProxies && config.ProxySettings?.ProxyUrls != null)
            {
                foreach (var proxyUrl in config.ProxySettings.ProxyUrls)
                {
                    _proxyManager.AddProxyFromString(proxyUrl);
                }
            }
        }

        public async Task<DownloadResult> DownloadAsync(CrawlRequest request)
        {
            await _semaphore.WaitAsync();
            try
            {
                return await DownloadWithRetryAsync(request);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<DownloadResult> DownloadWithRetryAsync(CrawlRequest request)
        {
            int retryCount = 0;
            Exception? lastException = null;

            while (retryCount <= _config.RetryPolicy.MaxRetries)
            {
                try
                {
                    // 检查 Robots.txt
                    if (_robotsTxtParser != null && _config.RespectRobotsTxt)
                    {
                        var isAllowed = await _robotsTxtParser.IsAllowedAsync(request.Url);
                        if (!isAllowed)
                        {
                            _logger.LogWarning("URL blocked by robots.txt: {Url}", request.Url);
                            return new DownloadResult
                            {
                                Url = request.Url,
                                IsSuccess = false,
                                ErrorMessage = "Blocked by robots.txt",
                                StatusCode = 403
                            };
                        }
                    }

                    var result = await DownloadInternalAsync(request);

                    if (result.IsSuccess)
                    {
                        // 检查反爬虫
                        if (_antiBotService != null && _config.EnableAntiBotDetection)
                        {
                            var antiBotResult = await _antiBotService.DetectAsync(
                                CreateMockResponse(result),
                                result.Content);

                            if (antiBotResult.IsBlocked)
                            {
                                _logger.LogWarning("Anti-bot detected for {Url}: {Reason}",
                                    request.Url, antiBotResult.BlockReason);

                                if (_retryStrategy != null && await _retryStrategy.ShouldRetryAsync(
                                    new Uri(request.Url).Host,
                                    new Exception(antiBotResult.BlockReason),
                                    retryCount))
                                {
                                    retryCount++;
                                    continue;
                                }
                                else
                                {
                                    result.IsSuccess = false;
                                    result.ErrorMessage = antiBotResult.BlockReason;
                                    return result;
                                }
                            }
                        }

                        // 记录成功指标
                        _metrics?.RecordUrlProcessed(
                            new Uri(request.Url).Host,
                            result.StatusCode,
                            result.RawData?.Length ?? 0,
                            result.DownloadTimeMs);

                        return result;
                    }
                    else
                    {
                        // 下载失败，使用重试策略
                        lastException = new Exception(result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError(ex, "Download failed for {Url} (attempt {Attempt})",
                        request.Url, retryCount + 1);
                }

                // 决定是否重试
                string? host = new Uri(request.Url)?.Host;
                if (!string.IsNullOrEmpty( host ) && _retryStrategy != null &&  await _retryStrategy!.ShouldRetryAsync( host!, lastException, retryCount ))
                {
                    retryCount++;
                    _logger.LogInformation("Retrying {Url} (attempt {Attempt})",
                        request.Url, retryCount + 1);
                }
                else
                {
                    break;
                }
            }

            // 记录失败指标
            _metrics?.RecordUrlFailed(new Uri(request.Url).Host, lastException?.GetType().Name ?? "Unknown");

            return new DownloadResult
            {
                Url = request.Url,
                IsSuccess = false,
                ErrorMessage = $"All retry attempts failed. Last error: {lastException?.Message}",
                StatusCode = 0
            };
        }

        private static HttpResponseMessage CreateMockResponse(DownloadResult result)
        {
            var response = new HttpResponseMessage((System.Net.HttpStatusCode)result.StatusCode);
            foreach (var header in result.Headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return response;
        }

        private async Task<DownloadResult> DownloadInternalAsync(CrawlRequest request)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ProxyServer? currentProxy = null;
            HttpRequestMessage? requestMessage = null;

            using var managedClient = _httpClientManager.GetClient();
            try
            {
                // 选择代理（如果启用）
                if (_useProxies)
                {
                    currentProxy = _proxyManager?.GetNextProxy();
                    if (currentProxy != null)
                    {
                        _logger.LogDebug("Using proxy for {Url}: {Proxy}", request.Url, currentProxy.Address);
                    }
                }

                requestMessage = CreateHttpRequestMessage(request);
                HttpResponseMessage response;

                // 使用代理或直接连接
                if (currentProxy != null && _proxyManager != null)
                {
                    using var handler = _proxyManager?.CreateHttpClientHandler(currentProxy);
                    using var proxyClient = new HttpClient(handler!)
                    {
                        Timeout = Timeout
                    };
                    response = await proxyClient.SendAsync(requestMessage);
                }
                else
                {
                    response = await managedClient.Client.SendAsync(requestMessage);
                }

                var content = await ProcessResponseAsync(response);

                var result = new DownloadResult
                {
                    Url = request.Url,
                    Content = content,
                    RawData = await response.Content.ReadAsByteArrayAsync(),
                    ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
                    StatusCode = (int)response.StatusCode,
                    DownloadTimeMs = stopwatch.ElapsedMilliseconds,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                    IsSuccess = response.IsSuccessStatusCode
                };

                // 记录代理成功
                if (currentProxy != null)
                {
                    _proxyManager?.RecordSuccess(currentProxy);
                }

                _logger.LogDebug("Download successful: {Url} ({StatusCode}) in {Time}ms",
                    request.Url, response.StatusCode, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                // 记录代理失败
                if (currentProxy != null)
                {
                    _proxyManager?.RecordFailure(currentProxy, ex.Message);
                }

                _logger.LogError(ex, "Download failed for {Url}", request.Url);

                return new DownloadResult
                {
                    Url = request.Url,
                    Content = string.Empty,
                    RawData = [],
                    StatusCode = 0,
                    DownloadTimeMs = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = ex.Message,
                    IsSuccess = false
                };
            }
            finally
            {
                requestMessage?.Dispose();
                stopwatch.Stop();
            }
        }

        private HttpRequestMessage CreateHttpRequestMessage(CrawlRequest request)
        {
            var message = new HttpRequestMessage(
                request.Method == CrawlMethod.GET ? HttpMethod.Get : HttpMethod.Post,
                request.Url
            );

            // 轮换User-Agent
            message.Headers.Add("User-Agent", _userAgentService?.GetRandomUserAgent());

            // 添加Referrer
            if (!string.IsNullOrEmpty(request.Referrer))
            {
                message.Headers.Referrer = new Uri(request.Referrer);
            }

            // 添加其他常用头信息
            message.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            message.Headers.Add("Accept-Language", "en-US,en;q=0.5");
            message.Headers.Add("Cache-Control", "no-cache");
            message.Headers.Add("Upgrade-Insecure-Requests", "1");

            return message;
        }

        private async Task<string> ProcessResponseAsync(HttpResponseMessage response)
        {
            var contentBytes = await response.Content.ReadAsByteArrayAsync();

            // 处理压缩内容
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                contentBytes = DecompressGzip(contentBytes);
            }
            else if (response.Content.Headers.ContentEncoding.Contains("deflate"))
            {
                contentBytes = DecompressDeflate(contentBytes);
            }

            // 检测编码并转换为字符串
            var encoding = DetectEncoding(contentBytes, response);
            return encoding.GetString(contentBytes);
        }

        private static byte[] DecompressGzip(byte[] compressedData)
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }

        private static byte[] DecompressDeflate(byte[] compressedData)
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            deflateStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }

        private Encoding DetectEncoding(byte[] content, HttpResponseMessage response)
        {
            // 从HTTP头获取编码
            var charset = response.Content.Headers.ContentType?.CharSet;
            if (!string.IsNullOrEmpty(charset))
            {
                try
                {
                    return Encoding.GetEncoding(charset);
                }
                catch
                {
                    _logger.LogDebug("Unsupported charset: {Charset}", charset);
                }
            }

            // 从HTML meta标签检测编码
            try
            {
                var encoding = EncodingDetector.DetectFromContent(content);
                if (encoding != null)
                    return encoding;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to detect encoding from content");
            }

            return Encoding.UTF8;
        }

        // 数据导出方法
        public async Task<bool> ExportDataAsync<T>(IEnumerable<T> data, string filePath)
        {
            if (_dataExporter == null)
            {
                _logger.LogWarning("Data export service is not available");
                return false;
            }

            try
            {
                return await _dataExporter.ExportAsync(data, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export data to {FilePath}", filePath);
                return false;
            }
        }

        public Task InitializeAsync()
        {
            _logger.LogInformation("AdvancedDownloader initialized with {ConcurrentRequests} concurrent requests",
                ConcurrentRequests);
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            _httpClientManager?.Dispose();
            _logger.LogInformation("AdvancedDownloader shutdown");
            return Task.CompletedTask;
        }
    }

    

}