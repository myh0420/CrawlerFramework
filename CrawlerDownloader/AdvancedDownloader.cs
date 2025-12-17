// <copyright file="AdvancedDownloader.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerDownloader
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
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
    using OpenTelemetry.Trace;

    /// <summary>
    /// 高级下载器，用于异步下载URL内容.
    /// </summary>
    public class AdvancedDownloader : IDownloader, IPlugin
    {
        /// <summary>
        /// 初始化AdvancedDownloader类的新实例.
        /// </summary>
        private readonly SimpleHttpClientManager httpClientManager;

        /// <summary>
        /// 日志记录器.
        /// </summary>
        private readonly ILogger<AdvancedDownloader> logger;

        /// <summary>
        /// 用户代理服务.
        /// </summary>
        private readonly RotatingUserAgentService? userAgentService;

        /// <summary>
        /// 代理管理器.
        /// </summary>
        private readonly ProxyManager? proxyManager;

        /// <summary>
        /// 反爬虫检测服务.
        /// </summary>
        private readonly AntiBotDetectionService? antiBotService;

        /// <summary>
        /// 重试策略.
        /// </summary>
        private readonly AdaptiveRetryStrategy? retryStrategy;

        /// <summary>
        /// Robots.txt解析器.
        /// </summary>
        private readonly RobotsTxtParser? robotsTxtParser;

        /// <summary>
        /// 指标服务.
        /// </summary>
        private readonly CrawlerMetrics? metrics;

        /// <summary>
        /// 错误处理服务.
        /// </summary>
        private readonly IErrorHandlingService? errorHandlingService;

        /// <summary>
        /// 代理处理程序缓存.
        /// </summary>
        private readonly ConcurrentDictionary<string, HttpClientHandler> proxyHandlerCache = new();

        /// <summary>
        /// HttpClient缓存，按代理地址组织.
        /// </summary>
        private readonly ConcurrentDictionary<string, HttpClient> httpClientCache = new();

        /// <summary>
        /// 数据导出服务.
        /// </summary>
        private readonly DataExportService? dataExporter; // 添加这个字段

        /// <summary>
        /// 信号量，用于控制并发请求数.
        /// </summary>
        private readonly SemaphoreSlim semaphore;

        /// <summary>
        /// 是否使用代理.
        /// </summary>
        private readonly bool useProxies;

        /// <summary>
        /// 配置.
        /// </summary>
        private readonly AdvancedCrawlConfiguration config;

        // 完整的构造函数

        /// <summary>
        /// Initializes a new instance of the <see cref="AdvancedDownloader"/> class.
        /// 初始化AdvancedDownloader类的新实例.
        /// </summary>
        /// <param name="logger">日志记录器.</param>
        /// <param name="config">高级爬取配置.</param>
        /// <param name="httpClientManager">HTTP客户端管理器.</param>
        /// <param name="userAgentService">用户代理服务.</param>
        /// <param name="proxyManager">代理管理器.</param>
        /// <param name="errorHandlingService">错误处理服务.</param>
        /// <param name="antiBotService">反爬虫检测服务.</param>
        /// <param name="retryStrategy">重试策略.</param>
        /// <param name="robotsTxtParser">Robots.txt解析器.</param>
        /// <param name="metrics">指标服务.</param>
        /// <param name="dataExporter">数据导出服务.</param>
        /// <remarks>
        /// 此构造函数用于初始化AdvancedDownloader类的新实例。
        /// 它接受多个参数，包括日志记录器、高级爬取配置、HTTP客户端管理器、用户代理服务、代理管理器、错误处理服务、反爬虫检测服务、重试策略、Robots.txt解析器、指标服务和数据导出服务。
        /// 构造函数会根据配置初始化并发请求数、超时时间、信号量、是否使用代理等属性。
        /// 它还会根据配置添加代理到代理管理器中。.
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
            this.logger = logger;
            this.config = config;
            this.httpClientManager = httpClientManager;
            this.userAgentService = userAgentService;
            this.proxyManager = proxyManager;
            this.errorHandlingService = errorHandlingService;
            this.antiBotService = antiBotService;
            this.retryStrategy = retryStrategy;
            this.robotsTxtParser = robotsTxtParser;
            this.metrics = metrics;
            this.dataExporter = dataExporter;
            this.useProxies = config.ProxySettings?.Enabled ?? false;
            this.semaphore = new SemaphoreSlim(config.MaxConcurrentTasks);

            // 配置代理
            if (this.useProxies && config.ProxySettings?.ProxyUrls != null)
            {
                foreach (var proxyUrl in config.ProxySettings.ProxyUrls)
                {
                    this.proxyManager.AddProxyFromString(proxyUrl);
                }
            }
        }

        /// <summary>
        /// Gets or sets 构造函数.
        /// </summary>
        public int ConcurrentRequests { get; set; } = 10;

        /// <summary>
        /// Gets or sets 超时时间.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        // IPlugin接口实现

        /// <summary>
        /// Gets 插件名称.
        /// </summary>
        public string PluginName => "AdvancedDownloader";

        /// <summary>
        /// Gets 插件版本.
        /// </summary>
        public string Version => "1.0.0";

        /// <summary>
        /// Gets 插件描述.
        /// </summary>
        public string Description => "高级下载器插件，支持代理、重试、反爬虫检测等功能";

        /// <summary>
        /// Gets 插件类型.
        /// </summary>
        public PluginType PluginType => PluginType.Downloader;

        /// <summary>
        /// Gets 插件作者.
        /// </summary>
        public string Author => "CrawlerFramework Team";

        /// <summary>
        /// Gets 插件入口点类型.
        /// </summary>
        public Type EntryPointType => typeof(AdvancedDownloader);

        /// <summary>
        /// 异步下载URL内容.
        /// </summary>
        /// <param name="request">下载请求.</param>
        /// <returns>下载结果.</returns>
        /// <remarks>
        /// 此方法用于异步下载URL内容，包括处理重试、Robots.txt检查、反爬虫检测等。
        /// 如果下载成功，则返回包含内容的DownloadResult；否则返回包含错误信息的DownloadResult。.
        /// </remarks>
        public async Task<DownloadResult> DownloadAsync(CrawlRequest request)
        {
            await this.semaphore.WaitAsync();
            try
            {
                return await this.DownloadWithRetryAsync(request);
            }
            finally
            {
                this.semaphore.Release();
            }
        }

        /// <summary>
        /// 异步导出数据到指定文件.
        /// </summary>
        /// <typeparam name="T">数据类型.
        /// </typeparam>
        /// <param name="data">要导出的数据集合.
        /// </param>
        /// <param name="filePath">导出文件的路径.
        /// </param>
        /// <returns>如果导出成功则返回true，否则返回false.
        /// </returns>
        /// <remarks>
        /// 此方法用于异步导出数据到指定文件，支持多种数据类型。
        /// 如果数据导出服务未配置或初始化失败，则返回false。.
        /// </remarks>
        public async Task<bool> ExportDataAsync<T>(IEnumerable<T> data, string filePath)
        {
            if (this.dataExporter == null)
            {
                this.logger.LogWarning("Data export service is not available");
                return false;
            }

            try
            {
                return await this.dataExporter.ExportAsync(data, filePath);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to export data to {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// 异步初始化下载器.
        /// </summary>
        /// <returns>初始化完成的任务.
        /// </returns>
        /// <remarks>
        /// 此方法用于异步初始化下载器，包括初始化HTTP客户端管理器和数据导出服务。
        /// 如果初始化失败，则记录警告日志。.
        /// </remarks>
        public Task InitializeAsync()
        {
            this.logger.LogInformation("AdvancedDownloader initialized with {ConcurrentRequests} concurrent requests", this.ConcurrentRequests);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步关闭下载器.
        /// </summary>
        /// <returns>关闭完成的任务.
        /// </returns>
        /// <remarks>
        /// 此方法用于异步关闭下载器，包括释放HTTP客户端管理器和缓存的代理处理器。.
        /// </remarks>
        public Task ShutdownAsync()
        {
            this.httpClientManager?.Dispose();

            // 释放缓存的HttpClient实例
            foreach (var client in this.httpClientCache.Values)
            {
                client.Dispose();
            }

            this.httpClientCache.Clear();

            // 释放缓存的代理处理器
            foreach (var handler in this.proxyHandlerCache.Values)
            {
                handler.Dispose();
            }

            this.proxyHandlerCache.Clear();

            this.logger.LogInformation("AdvancedDownloader shutdown");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 创建模拟的HttpResponseMessage.
        /// </summary>
        /// <param name="result">下载结果.</param>
        /// <returns>模拟的HttpResponseMessage.</returns>
        /// <remarks>
        /// 此方法用于创建模拟的HttpResponseMessage，用于反爬虫检测。.
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
        /// 获取内容流（处理压缩）.
        /// </summary>
        /// <param name="response">HTTP响应消息.
        /// </param>
        /// <param name="responseStream">HTTP响应内容的流.
        /// </param>
        /// <returns>处理后的内容流.</returns>
        /// <remarks>
        /// 此方法用于根据Content-Encoding头信息处理HTTP响应内容的流。
        /// 如果包含"gzip"，则返回解压缩后的GZipStream；
        /// 如果包含"deflate"，则返回解压缩后的DeflateStream；
        /// 否则返回原始流。.
        /// </remarks>
        private static Stream GetContentStream(HttpResponseMessage response, Stream responseStream)
        {
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                return new GZipStream(responseStream, CompressionMode.Decompress);
            }
            else if (response.Content.Headers.ContentEncoding.Contains("deflate"))
            {
                return new DeflateStream(responseStream, CompressionMode.Decompress);
            }

            return responseStream;
        }

        /// <summary>
        /// 异步下载URL内容.
        /// </summary>
        /// <param name="request">下载请求.</param>
        /// <returns>下载结果.</returns>
        /// <remarks>
        /// 此方法用于异步下载URL内容，包括处理重试、Robots.txt检查、反爬虫检测等。
        /// 如果下载成功，则返回包含内容的DownloadResult；否则返回包含错误信息的DownloadResult。.
        /// </remarks>
        private async Task<DownloadResult> DownloadWithRetryAsync(CrawlRequest request)
        {
            int retryCount = 0;
            Exception? lastException = null;
            ProxyServer? lastProxy = null;

            while (retryCount <= this.config.RetryPolicy.MaxRetries)
            {
                try
                {
                    // 检查 Robots.txt
                    if (this.robotsTxtParser != null && this.config.RespectRobotsTxt)
                    {
                        var isAllowed = await this.robotsTxtParser.IsAllowedAsync(request.Url);
                        if (!isAllowed)
                        {
                            this.logger.LogWarning("URL blocked by robots.txt: {Url}", request.Url);
                            return new DownloadResult
                            {
                                Url = request.Url,
                                IsSuccess = false,
                                ErrorMessage = "Blocked by robots.txt",
                                StatusCode = 403,
                            };
                        }
                    }

                    var result = await this.DownloadInternalAsync(request);

                    if (result.IsSuccess)
                    {
                        // 检查反爬虫
                        if (this.antiBotService != null && this.config.EnableAntiBotDetection)
                        {
                            var antiBotResult = await this.antiBotService.DetectAsync(
                                CreateMockResponse(result),
                                result.Content);

                            if (antiBotResult.IsBlocked)
                            {
                                this.logger.LogWarning("Anti-bot detected for {Url}: {Reason}", request.Url, antiBotResult.BlockReason);

                                var antiBotException = new AntiBotException(request.Url, true, antiBotResult.BlockReason);
                                bool canRecover = this.errorHandlingService?.CanAutoRecover(antiBotException) ?? false;

                                if ((this.retryStrategy != null && await this.retryStrategy.ShouldRetryAsync(
                                    new Uri(request.Url).Host,
                                    antiBotException,
                                    retryCount)) || canRecover)
                                {
                                    // 反爬错误，自动切换代理
                                    if (this.useProxies && this.proxyManager != null)
                                    {
                                        this.logger.LogInformation("Switching proxy due to anti-bot detection for {Url}", request.Url);

                                        // 可以清除当前代理缓存或直接获取新代理
                                    }

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
                        this.metrics?.RecordUrlProcessed(
                            new Uri(request.Url).Host,
                            result.StatusCode,
                            result.RawData?.Length ?? 0,
                            result.DownloadTimeMs,
                            0, // parseDurationMs
                            0); // storageDurationMs

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
                    this.logger.LogError(ex, "Download failed for {Url} (attempt {Attempt})", request.Url, retryCount + 1);

                    // 检查是否可以自动恢复
                    if (this.errorHandlingService != null && this.errorHandlingService.CanAutoRecover(ex))
                    {
                        this.logger.LogInformation("Error can be auto-recovered for {Url}: {ErrorType}", request.Url, this.errorHandlingService.GetErrorType(ex));

                        // 针对反爬错误或网络错误，自动切换代理
                        if (this.useProxies && this.proxyManager != null &&
                            (this.errorHandlingService.GetErrorType(ex) == ErrorType.AntiBot ||
                             this.errorHandlingService.GetErrorType(ex) == ErrorType.Network))
                        {
                            this.logger.LogInformation("Switching proxy due to {ErrorType} error for {Url}", this.errorHandlingService.GetErrorType(ex), request.Url);

                            // 标记当前代理为失败（如果有）
                            if (lastProxy != null)
                            {
                                this.proxyManager.RecordFailure(lastProxy, ex.Message);
                            }

                            // 记录代理切换指标
                            var domain = new Uri(request.Url).Host;
                            this.metrics?.RecordProxySwitch(domain, this.errorHandlingService.GetErrorType(ex).ToString());

                            // 清除当前代理的缓存，强制下次使用新代理
                            if (lastProxy != null && this.httpClientCache.ContainsKey(lastProxy.Address))
                            {
                                this.httpClientCache.TryRemove(lastProxy.Address, out _);
                                this.logger.LogDebug("Removed cached HttpClient for proxy: {Proxy}", lastProxy.Address);
                            }
                        }
                    }
                }

                // 决定是否重试
                string? host = new Uri(request.Url).Host;
                bool shouldRetry = false;

                if (!string.IsNullOrEmpty(host) && this.retryStrategy != null && await this.retryStrategy.ShouldRetryAsync(host, lastException, retryCount))
                {
                    shouldRetry = true;
                }

                // 如果重试策略不建议重试，但错误处理服务认为可以自动恢复，则仍然重试
                else if (this.errorHandlingService != null && this.errorHandlingService.CanAutoRecover(lastException))
                {
                    shouldRetry = true;
                }

                if (shouldRetry)
                {
                    retryCount++;

                    // 获取推荐的重试延迟时间
                    TimeSpan delay = TimeSpan.FromSeconds(1);
                    if (this.errorHandlingService != null)
                    {
                        delay = this.errorHandlingService.GetRecommendedDelay(lastException, retryCount);
                    }

                    this.logger.LogInformation("Retrying {Url} (attempt {Attempt}) after {Delay}ms delay", request.Url, retryCount + 1, delay.TotalMilliseconds);

                    // 等待推荐的延迟时间
                    await Task.Delay(delay);
                }
                else
                {
                    break;
                }
            }

            // 记录失败指标
            this.metrics?.RecordUrlFailed(new Uri(request.Url).Host, lastException?.GetType().Name ?? "Unknown");

            // 使用错误处理服务来处理最终异常
            if (this.errorHandlingService != null)
            {
                return this.errorHandlingService.HandleDownloadException(request.Url, lastException ?? new Exception("Download failed after all retries"));
            }

            return new DownloadResult
            {
                Url = request.Url,
                IsSuccess = false,
                ErrorMessage = $"All retry attempts failed. Last error: {lastException?.Message}",
                StatusCode = 0,
            };
        }

        /// <summary>
        /// 内部下载方法.
        /// </summary>
        /// <param name="request">要下载的请求.</param>
        /// <returns>下载结果.</returns>
        /// <exception cref="DownloadException">下载过程中发生的异常.</exception>
        /// <remarks>
        /// 此方法是下载过程的核心，负责实际的HTTP请求和响应处理。
        /// 它会根据配置使用代理服务器，处理重试逻辑，记录指标和异常。
        /// 使用HttpClient连接池复用和流处理以减少内存占用。.
        /// </remarks>
        private async Task<DownloadResult> DownloadInternalAsync(CrawlRequest request)
        {
            // 获取取消令牌，默认使用None
            var cancellationToken = request.CancellationToken ?? CancellationToken.None;

            // 检查取消令牌
            cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ProxyServer? currentProxy = null;
            HttpRequestMessage? requestMessage = null;
            HttpResponseMessage? response = null;

            // 创建OpenTelemetry追踪
            var tracer = TracerProvider.Default.GetTracer("CrawlerDownloader");
            using var span = tracer.StartActiveSpan("DownloadInternal", SpanKind.Client);
            span.SetAttribute("url", request.Url);
            span.SetAttribute("user_agent", this.userAgentService?.GetRandomUserAgent() ?? "default");

            // 添加请求上下文信息
            span.SetAttribute("request.id", request.RequestId ?? Guid.NewGuid().ToString());
            if (request.AdditionalHeaders != null)
            {
                foreach (var header in request.AdditionalHeaders)
                {
                    span.SetAttribute($"request.header.{header.Key}", header.Value);
                }
            }

            // 选择代理（如果启用）
            if (this.useProxies)
            {
                currentProxy = this.proxyManager?.GetNextProxy();
                if (currentProxy != null)
                {
                    this.logger.LogDebug("Using proxy for {Url}: {Proxy}", request.Url, currentProxy.Address);
                    span.SetAttribute("proxy.address", currentProxy.Address);
                    span.SetAttribute("proxy.type", currentProxy.Protocol);
                }
            }

            requestMessage = this.CreateHttpRequestMessage(request);
            HttpClient? httpClient = null;

            try
            {
                // 使用代理或直接连接
                if (currentProxy != null && this.proxyManager != null)
                {
                    // 从缓存获取或创建HttpClient
                    httpClient = this.httpClientCache.GetOrAdd(currentProxy.Address, (proxyAddress) =>
                    {
                        // 获取或创建代理处理器
                        if (!this.proxyHandlerCache.TryGetValue(proxyAddress, out var handler))
                        {
                            handler = this.proxyManager.CreateHttpClientHandler(currentProxy!);
                            this.proxyHandlerCache.TryAdd(proxyAddress, handler);
                        }

                        return new HttpClient(handler)
                        {
                            Timeout = this.Timeout,
                        };
                    });

                    try
                    {
                        response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        // 记录代理失败并抛出下载异常
                        this.proxyManager.RecordFailure(currentProxy, ex.Message);
                        throw new DownloadException(request.Url, ex.Message, null, ex);
                    }
                }
                else
                {
                    // 使用管理的客户端（无代理情况）
                    using var managedClient = await this.httpClientManager.GetClientAsync();
                    try
                    {
                        response = await managedClient.Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
                        this.proxyManager?.RecordFailure(currentProxy, $"HTTP error: {response.StatusCode}");
                    }

                    throw new DownloadException(request.Url, $"HTTP error: {response.StatusCode}", (int)response.StatusCode);
                }

                // 记录响应信息到追踪
                span.SetAttribute("response.status_code", (int)response.StatusCode);
                span.SetAttribute("response.is_success", response.IsSuccessStatusCode);

                if (response.Content.Headers.ContentType != null)
                {
                    span.SetAttribute("response.content_type", response.Content.Headers.ContentType.ToString());
                }

                if (response.Content.Headers.ContentLength.HasValue)
                {
                    span.SetAttribute("response.content_length", response.Content.Headers.ContentLength.Value);
                }

                var content = await this.ProcessResponseAsync(response);

                // 使用流处理获取RawData，减少内存占用
                byte[]? rawData = null;

                // 暂时移除对IncludeRawData的依赖，后续会修复配置问题
                using var memoryStream = new MemoryStream();
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await responseStream.CopyToAsync(memoryStream, 81920, cancellationToken);
                rawData = memoryStream.ToArray();

                var result = new DownloadResult
                {
                    Url = request.Url,
                    Content = content,
                    RawData = rawData,
                    ContentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty,
                    StatusCode = (int)response.StatusCode,
                    DownloadTimeMs = stopwatch.ElapsedMilliseconds,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                    IsSuccess = true,
                };

                // 记录代理成功
                if (currentProxy != null)
                {
                    this.proxyManager?.RecordSuccess(currentProxy);
                }

                this.logger.LogDebug("Download successful: {Url} ({StatusCode}) in {Time}ms", request.Url, response.StatusCode, stopwatch.ElapsedMilliseconds);

                return result;
            }
            finally
            {
                requestMessage?.Dispose();
                response?.Dispose();
                stopwatch.Stop();
                span.SetAttribute("duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>
        /// 创建HttpRequestMessage.
        /// </summary>
        /// <param name="request">要下载的请求.
        /// </param>
        /// <returns>创建的HttpRequestMessage.</returns>
        /// <remarks>
        /// 此方法用于创建HttpRequestMessage，设置方法、URL、User-Agent和Referrer等头信息。.
        /// </remarks>
        private HttpRequestMessage CreateHttpRequestMessage(CrawlRequest request)
        {
            var message = new HttpRequestMessage(
                request.Method == CrawlMethod.GET ? HttpMethod.Get : HttpMethod.Post,
                request.Url);

            // 轮换User-Agent
            message.Headers.Add("User-Agent", this.userAgentService?.GetRandomUserAgent());

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
        /// 处理HTTP响应内容.
        /// </summary>
        /// <param name="response">HTTP响应消息.
        /// </param>
        /// <returns>处理后的内容字符串.</returns>
        /// <remarks>
        /// 此方法用于处理HTTP响应内容，包括解压缩和编码检测。
        /// 它会根据Content-Encoding头信息解压缩内容，然后根据字符集检测编码。
        /// 使用流直接处理以减少内存占用。.
        /// </remarks>
        private async Task<string> ProcessResponseAsync(HttpResponseMessage response)
        {
            // 直接处理响应流，避免一次性加载到内存
            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var contentStream = GetContentStream(response, responseStream);

            // 将内容复制到MemoryStream以支持Seek操作
            using var memoryStream = new MemoryStream();
            await contentStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // 检测编码
            var encoding = await this.DetectEncodingAsync(memoryStream, response);

            // 重置流位置以便重新读取
            memoryStream.Position = 0;

            // 使用StreamReader读取内容
            using var reader = new StreamReader(memoryStream, encoding, detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// 检测HTTP响应内容的编码（异步流版本）.
        /// </summary>
        /// <param name="stream">HTTP响应内容的流.
        /// </param>
        /// <param name="response">HTTP响应消息.
        /// </param>
        /// <returns>检测到的编码.</returns>
        /// <remarks>
        /// 此方法用于检测HTTP响应内容的编码，包括从HTTP头和HTML meta标签中提取编码。
        /// 从流中读取少量数据进行检测，然后重置流位置。
        /// 如果无法检测到编码，则返回UTF-8编码。.
        /// </remarks>
        private async Task<Encoding> DetectEncodingAsync(Stream stream, HttpResponseMessage response)
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
                    this.logger.LogDebug("Unsupported charset: {Charset}", charset);
                }
            }

            // 从HTML meta标签检测编码
            try
            {
                // 保存当前流位置
                var originalPosition = stream.Position;

                // 读取前8KB用于编码检测
                var buffer = new byte[Math.Min(8192, (int)stream.Length)];
                var bytesRead = await stream.ReadAsync(buffer);

                // 重置流位置
                stream.Seek(originalPosition, SeekOrigin.Begin);

                if (bytesRead > 0)
                {
                    // 创建实际读取的字节的副本
                    var contentBytes = buffer;
                    if (bytesRead < buffer.Length)
                    {
                        Array.Resize(ref contentBytes, bytesRead);
                    }

                    var encoding = EncodingDetector.DetectFromContent(contentBytes);
                    if (encoding != null)
                    {
                        return encoding;
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to detect encoding from content");
            }

            return Encoding.UTF8;
        }

        /// <summary>
        /// 检测HTTP响应内容的编码（字节数组版本，用于向后兼容）.
        /// </summary>
        /// <param name="content">HTTP响应内容的字节数组.
        /// </param>
        /// <param name="response">HTTP响应消息.
        /// </param>
        /// <returns>检测到的编码.</returns>
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
                    this.logger.LogDebug("Unsupported charset: {Charset}", charset);
                }
            }

            // 从HTML meta标签检测编码
            try
            {
                var encoding = EncodingDetector.DetectFromContent(content);
                if (encoding != null)
                {
                    return encoding;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Failed to detect encoding from content");
            }

            return Encoding.UTF8;
        }
    }
}