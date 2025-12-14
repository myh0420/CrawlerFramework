// CrawlerDownloader/AdvancedDownloader.cs
using CrawlerCore.AntiBot;
using CrawlerCore.ErrorHandling;
using CrawlerCore.Exceptions;
using CrawlerCore.Export;
using CrawlerCore.Metrics;
using CrawlerCore.Retry;
using CrawlerCore.Robots;
using CrawlerCore.Services;
using CrawlerCore.Utils;

using CrawlerDownloader.Services;
using CrawlerDownloader.Utils;
using CrawlerEntity.Configuration;
using CrawlerEntity.Enums;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
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
    /// <summary>
    /// 高级下载器，用于异步下载URL内容
    /// </summary>
    public class AdvancedDownloader : IDownloader
    {
        /// <summary>
        /// 初始化AdvancedDownloader类的新实例
        /// </summary>
        private readonly SimpleHttpClientManager _httpClientManager;
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<AdvancedDownloader> _logger;
        /// <summary>
        /// 用户代理服务
        /// </summary>
        private readonly RotatingUserAgentService? _userAgentService;
        /// <summary>
        /// 代理管理器
        /// </summary>
        private readonly ProxyManager? _proxyManager;
        /// <summary>
        /// 反爬虫检测服务
        /// </summary>
        private readonly AntiBotDetectionService? _antiBotService;
        /// <summary>
        /// 重试策略
        /// </summary>
        private readonly AdaptiveRetryStrategy? _retryStrategy;
        /// <summary>
        /// Robots.txt解析器
        /// </summary>
        private readonly RobotsTxtParser? _robotsTxtParser;
        /// <summary>
        /// 指标服务
        /// </summary>
        private readonly CrawlerMetrics? _metrics;
        /// <summary>
        /// 错误处理服务
        /// </summary>
        private readonly IErrorHandlingService? _errorHandlingService;
        /// <summary>
        /// 代理处理程序缓存
        /// </summary>
        private readonly ConcurrentDictionary<string, HttpClientHandler> _proxyHandlerCache = new();
        /// <summary>
        /// 代理处理程序信号量
        /// </summary>
        private readonly SemaphoreSlim _proxyHandlerSemaphore = new(1);

        /// <summary>
        /// 数据导出服务
        /// </summary>
        private readonly DataExportService? _dataExporter; // 添加这个字段
        /// <summary>
        /// 是否使用代理
        /// </summary>
        private readonly bool _useProxies;
        /// <summary>
        /// 配置
        /// </summary>
        private readonly AdvancedCrawlConfiguration _config;
        /// <summary>
        /// 构造函数
        /// </summary>
        public int ConcurrentRequests { get; set; } = 10;
        /// <summary>
        /// 超时时间
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        /// <summary>
        /// 信号量，用于控制并发请求数
        /// </summary>
        private readonly SemaphoreSlim _semaphore;

        // 完整的构造函数
        /// <summary>
        /// 初始化AdvancedDownloader类的新实例
        /// </summary>
        /// <param name="logger">日志记录器</param>
        /// <param name="config">高级爬取配置</param>
        /// <param name="httpClientManager">HTTP客户端管理器</param>
        /// <param name="userAgentService">用户代理服务</param>
        /// <param name="proxyManager">代理管理器</param>
        /// <param name="errorHandlingService">错误处理服务</param>
        /// <param name="antiBotService">反爬虫检测服务</param>
        /// <param name="retryStrategy">重试策略</param>
        /// <param name="robotsTxtParser">Robots.txt解析器</param>
        /// <param name="metrics">指标服务</param>
        /// <param name="dataExporter">数据导出服务</param>
        /// s<remarks>
        /// 此构造函数用于初始化AdvancedDownloader类的新实例。
        /// 它接受多个参数，包括日志记录器、高级爬取配置、HTTP客户端管理器、用户代理服务、代理管理器、错误处理服务、反爬虫检测服务、重试策略、Robots.txt解析器、指标服务和数据导出服务。
        /// 构造函数会根据配置初始化并发请求数、超时时间、信号量、是否使用代理等属性。
        /// 它还会根据配置添加代理到代理管理器中。
        /// </remarks>
        public AdvancedDownloader(
            ILogger<AdvancedDownloader> logger,
            AdvancedCrawlConfiguration config,
            SimpleHttpClientManager httpClientManager,
            RotatingUserAgentService userAgentService,
            ProxyManager proxyManager,
            IErrorHandlingService? errorHandlingService = null,
            AntiBotDetectionService? antiBotService = null,
            AdaptiveRetryStrategy? retryStrategy = null,
            RobotsTxtParser? robotsTxtParser = null,
            CrawlerMetrics? metrics = null,
            DataExportService? dataExporter = null)
        {
            _logger = logger;
            _config = config;
            _httpClientManager = httpClientManager;
            _userAgentService = userAgentService;
            _proxyManager = proxyManager;
            _errorHandlingService = errorHandlingService;
            _antiBotService = antiBotService;
            _retryStrategy = retryStrategy;
            _robotsTxtParser = robotsTxtParser;
            _metrics = metrics;
            _dataExporter = dataExporter;
            _useProxies = config.ProxySettings?.Enabled ?? false;
            _semaphore = new SemaphoreSlim(config.MaxConcurrentTasks);

            // 配置代理
            if (_useProxies && config.ProxySettings?.ProxyUrls != null)
            {
                foreach (var proxyUrl in config.ProxySettings.ProxyUrls)
                {
                    _proxyManager.AddProxyFromString(proxyUrl);
                }
            }
        }
        /// <summary>
        /// 异步下载URL内容
        /// </summary>
        /// <param name="request">下载请求</param>
        /// <returns>下载结果</returns>
        /// <remarks>
        /// 此方法用于异步下载URL内容，包括处理重试、Robots.txt检查、反爬虫检测等。
        /// 如果下载成功，则返回包含内容的DownloadResult；否则返回包含错误信息的DownloadResult。
        /// </remarks>
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
        /// <summary>
        /// 异步下载URL内容
        /// </summary>
        /// <param name="request">下载请求</param>
        /// <returns>下载结果</returns>
        /// <remarks>
        /// 此方法用于异步下载URL内容，包括处理重试、Robots.txt检查、反爬虫检测等。
        /// 如果下载成功，则返回包含内容的DownloadResult；否则返回包含错误信息的DownloadResult。
        /// </remarks>
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
                                    new AntiBotException(request.Url, true, antiBotResult.BlockReason),
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
                        lastException = new DownloadException(request.Url, result.ErrorMessage, result.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError(ex, "Download failed for {Url} (attempt {Attempt})",
                        request.Url, retryCount + 1);
                }

                // 决定是否重试
                string? host = new Uri(request.Url).Host;
                if (!string.IsNullOrEmpty(host) && _retryStrategy != null && await _retryStrategy.ShouldRetryAsync(host, lastException, retryCount))
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

            // 使用错误处理服务来处理最终异常
            if (_errorHandlingService != null)
            {
                return _errorHandlingService.HandleDownloadException(request.Url, lastException ?? new Exception("Download failed after all retries"));
            }

            return new DownloadResult
            {
                Url = request.Url,
                IsSuccess = false,
                ErrorMessage = $"All retry attempts failed. Last error: {lastException?.Message}",
                StatusCode = 0
            };
        }

        /// <summary>
        /// 创建模拟的HttpResponseMessage
        /// </summary>
        /// <param name="result">下载结果</param>
        /// <returns>模拟的HttpResponseMessage</returns>
        /// <remarks>
        /// 此方法用于创建模拟的HttpResponseMessage，用于反爬虫检测。
        /// </remarks>
        private static HttpResponseMessage CreateMockResponse(DownloadResult result)
        {
            var response = new HttpResponseMessage((System.Net.HttpStatusCode)result.StatusCode);
            foreach (var header in result.Headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return response;
        }
        /// <summary>
        /// 内部下载方法
        /// </summary>
        /// <param name="request">要下载的请求</param>
        /// <returns>下载结果</returns>
        /// <exception cref="DownloadException">下载过程中发生的异常</exception>
        /// <remarks>
        /// 此方法是下载过程的核心，负责实际的HTTP请求和响应处理。
        /// 它会根据配置使用代理服务器，处理重试逻辑，记录指标和异常。
        /// </remarks>
        private async Task<DownloadResult> DownloadInternalAsync(CrawlRequest request)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ProxyServer? currentProxy = null;
            HttpRequestMessage? requestMessage = null;

            using var managedClient = await _httpClientManager.GetClientAsync();
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
                    // 尝试从缓存获取代理处理器，避免每次创建新的处理器
                    if (!_proxyHandlerCache.TryGetValue(currentProxy.Address, out var handler))
                    {
                        // 线程安全地创建和缓存处理器
                        await _proxyHandlerSemaphore.WaitAsync();
                        try
                        {
                            // 双重检查锁定模式
                            if (!_proxyHandlerCache.TryGetValue(currentProxy.Address, out handler))
                            {
                                handler = _proxyManager.CreateHttpClientHandler(currentProxy);
                                _proxyHandlerCache.TryAdd(currentProxy.Address, handler);
                            }
                        }
                        finally
                        {
                            _proxyHandlerSemaphore.Release();
                        }
                    }
                    
                    using var proxyClient = new HttpClient(handler)
                    {
                        Timeout = Timeout
                    };
                    try
                    {
                        response = await proxyClient.SendAsync(requestMessage);
                    }
                    catch (Exception ex)
                    {
                        // 记录代理失败并抛出下载异常
                        _proxyManager.RecordFailure(currentProxy, ex.Message);
                        throw new DownloadException(request.Url, ex.Message, null, ex);
                    }
                }
                else
                {
                    try
                    {
                        response = await managedClient.Client.SendAsync(requestMessage);
                    }
                    catch (Exception ex)
                    {
                        throw new DownloadException(request.Url, ex.Message, null, ex);
                    }
                }

                if (!response.IsSuccessStatusCode)
                {
                    // 记录代理失败并抛出下载异常
                    if (currentProxy != null)
                    {
                        _proxyManager?.RecordFailure(currentProxy, $"HTTP error: {response.StatusCode}");
                    }
                    throw new DownloadException(request.Url, $"HTTP error: {response.StatusCode}", (int)response.StatusCode);
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
                    IsSuccess = true
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
            finally
            {
                requestMessage?.Dispose();
                stopwatch.Stop();
            }
        }
        /// <summary>
        /// 创建HttpRequestMessage
        /// </summary>
        /// <param name="request">要下载的请求</param>
        /// <returns>创建的HttpRequestMessage</returns>
        /// <remarks>
        /// 此方法用于创建HttpRequestMessage，设置方法、URL、User-Agent和Referrer等头信息。
        /// </remarks>
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
        /// <summary>
        /// 处理HTTP响应内容
        /// </summary>
        /// <param name="response">HTTP响应消息</param>
        /// <returns>处理后的内容字符串</returns>
        /// <remarks>
        /// 此方法用于处理HTTP响应内容，包括解压缩和编码检测。
        /// 它会根据Content-Encoding头信息解压缩内容，然后根据字符集检测编码。
        /// </remarks>
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
        /// <summary>
        /// 解压缩Gzip压缩数据
        /// </summary>
        /// <param name="compressedData">压缩后的字节数组</param>
        /// <returns>解压缩后的字节数组</returns>
        /// <remarks>
        /// 此方法用于解压缩Gzip压缩数据，返回解压缩后的字节数组。
        /// </remarks>
        private static byte[] DecompressGzip(byte[] compressedData)
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            gzipStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
        /// <summary>
        /// 解压缩Deflate压缩数据
        /// </summary>
        /// <param name="compressedData">压缩后的字节数组</param>
        /// <returns>解压缩后的字节数组</returns>
        /// <remarks>
        /// 此方法用于解压缩Deflate压缩数据，返回解压缩后的字节数组。
        /// </remarks>
        private static byte[] DecompressDeflate(byte[] compressedData)
        {
            using var compressedStream = new MemoryStream(compressedData);
            using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            deflateStream.CopyTo(resultStream);
            return resultStream.ToArray();
        }
        /// <summary>
        /// 检测HTTP响应内容的编码
        /// </summary>
        /// <param name="content">HTTP响应内容的字节数组</param>
        /// <param name="response">HTTP响应消息</param>
        /// <returns>检测到的编码</returns>
        /// <remarks>
        /// 此方法用于检测HTTP响应内容的编码，包括从HTTP头和HTML meta标签中提取编码。
        /// 如果无法检测到编码，则返回UTF-8编码。
        /// </remarks>
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

        /// <summary>
        /// 异步导出数据到指定文件
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">要导出的数据集合</param>
        /// <param name="filePath">导出文件的路径</param>
        /// <returns>如果导出成功则返回true，否则返回false</returns>
        /// <remarks>
        /// 此方法用于异步导出数据到指定文件，支持多种数据类型。
        /// 如果数据导出服务未配置或初始化失败，则返回false。
        /// </remarks>
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
        /// <summary>
        /// 异步初始化下载器
        /// </summary>
        /// <returns>初始化完成的任务</returns>
        /// <remarks>
        /// 此方法用于异步初始化下载器，包括初始化HTTP客户端管理器和数据导出服务。
        /// 如果初始化失败，则记录警告日志。
        /// </remarks>
        public Task InitializeAsync()
        {
            _logger.LogInformation("AdvancedDownloader initialized with {ConcurrentRequests} concurrent requests",
                ConcurrentRequests);
            return Task.CompletedTask;
        }
        /// <summary>
        /// 异步关闭下载器
        /// </summary>
        /// <returns>关闭完成的任务</returns>
        /// <remarks>
        /// 此方法用于异步关闭下载器，包括释放HTTP客户端管理器和缓存的代理处理器。
        /// </remarks>
        public Task ShutdownAsync()
        {
            _httpClientManager?.Dispose();
            
            // 释放缓存的代理处理器
            foreach (var handler in _proxyHandlerCache.Values)
            {
                handler.Dispose();
            }
            _proxyHandlerCache.Clear();
            
            _logger.LogInformation("AdvancedDownloader shutdown");
            return Task.CompletedTask;
        }
    }

    

}