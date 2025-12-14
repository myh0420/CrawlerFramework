// CrawlerCore/CrawlerEngine.cs

using CrawlerCore.AntiBot;
using CrawlerCore.Retry;
using CrawlerCore.Robots;
using CrawlerEntity.Configuration;
using CrawlerEntity.Enums;
using CrawlerEntity.Events;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CrawlerCore;
/// <summary>
/// 爬虫引擎，负责协调爬取任务的执行
/// </summary>
/// <param name="scheduler">任务调度器</param>
/// <param name="downloader">下载器</param>
/// <param name="parser">解析器</param>
/// <param name="storage">存储提供器</param>
/// <param name="logger">日志记录器</param>
/// <param name="metadataStore">元数据存储</param>
/// <param name="antiBotService">反机器人服务</param>
/// <param name="retryStrategy">重试策略</param>
/// <param name="robotsTxtParser">Robots.txt解析器</param>
public class CrawlerEngine(IScheduler scheduler, IDownloader downloader,
    IParser parser, IStorageProvider storage, ILogger<CrawlerEngine> logger, IMetadataStore? metadataStore = null,
    AntiBotDetectionService? antiBotService = null,
    AdaptiveRetryStrategy? retryStrategy = null,
    RobotsTxtParser? robotsTxtParser = null) : IDisposable
{
    /// <summary>
    /// 任务调度器
    /// </summary>
    private readonly IScheduler _scheduler = scheduler;
    /// <summary>
    /// 下载器
    /// </summary>
    private readonly IDownloader _downloader = downloader;
    /// <summary>
    /// 解析器
    /// </summary>
    private readonly IParser _parser = parser;
    /// <summary>
    /// 存储提供器
    /// </summary>
    private readonly IStorageProvider _storage = storage;
    /// <summary>
    /// 元数据存储
    /// </summary>
    private readonly IMetadataStore? _metadataStore = metadataStore;
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<CrawlerEngine> _logger = logger;
    /// <summary>
    /// 工作任务列表
    /// </summary>
    private readonly List<Task> _workerTasks = [];
    /// <summary>
    /// 取消令牌源
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSource;
    /// <summary>
    /// 是否正在运行
    /// </summary>
    private bool _isRunning;
    /// <summary>
    /// 当前状态
    /// </summary>
    private CrawlerStatus _currentStatus = CrawlerStatus.Idle;
    /// <summary>
    /// 启动时间
    /// </summary>
    private DateTime _startTime;
    /// <summary>
    /// 当前作业ID
    /// </summary>
    private string? _currentJobId = string.Empty;
    /// <summary>
    /// 暂停信号量
    /// </summary>
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    // 通过构造函数注入的服务
    /// <summary>
    /// 反机器人服务
    /// </summary>
    private readonly AntiBotDetectionService? _antiBotService = antiBotService;
    /// <summary>
    /// 重试策略
    /// </summary>
    private readonly AdaptiveRetryStrategy? _retryStrategy = retryStrategy;
    /// <summary>
    /// Robots.txt解析器
    /// </summary>
    private readonly RobotsTxtParser? _robotsTxtParser = robotsTxtParser;

    // 事件定义
    /// <summary>
    /// 爬取完成事件
    /// </summary>
    public event EventHandler<CrawlCompletedEventArgs>? OnCrawlCompleted;
    /// <summary>
    /// 爬取错误事件
    /// </summary>
    public event EventHandler<CrawlErrorEventArgs>? OnCrawlError;
    /// <summary>
    /// URL发现事件
    /// </summary>
    public event EventHandler<UrlDiscoveredEventArgs>? OnUrlDiscovered;
    /// <summary>
    /// 爬虫状态改变事件
    /// </summary>  
    public event EventHandler<CrawlerStatusChangedEventArgs>? OnStatusChanged;

    /// <summary>
    /// 当前状态
    /// </summary>
    public CrawlerStatus CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            if (_currentStatus != value)
            {
                var previousStatus = _currentStatus;
                _currentStatus = value;
                OnStatusChanged?.Invoke(this, new CrawlerStatusChangedEventArgs
                {
                    PreviousStatus = previousStatus,
                    CurrentStatus = value,
                    Message = $"Status changed from {previousStatus} to {value}"
                });
            }
        }
    }
    
    /// <summary>
    /// 爬虫运行时间
    /// </summary>
    public TimeSpan Uptime
    {
        get
        {
            if (_currentStatus == CrawlerStatus.Running)
            {
                return DateTime.UtcNow - _startTime;
            }
            return TimeSpan.Zero;
        }
    }
    
    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _isRunning;
    
    /// <summary>
    /// 当前任务ID
    /// </summary>
    public string? CurrentJobId => _currentJobId;
    
    /// <summary>
    /// 获取统计信息
    /// </summary>
    /// <returns>包含爬虫状态、运行时间、任务ID、工作线程数、队列长度、已处理URL数量的字典</returns>
    /// <remarks>
    /// 包含当前状态、运行时间、任务ID、工作线程数、队列长度、已处理URL数量。
    /// </remarks>
    public Task<Dictionary<string, object>> GetStatisticsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            { "Status", _currentStatus },
            { "Uptime", Uptime },
            { "JobId", _currentJobId ?? string.Empty },
            { "WorkerCount", _workerTasks.Count },
            { "QueueLength", _scheduler.QueuedCount },
            { "ProcessedCount", _scheduler.ProcessedCount }
        };
        return Task.FromResult(stats);
    }
    
    /// <summary>
    /// 获取当前爬取状态
    /// </summary>
    /// <returns>当前爬取状态</returns>
    /// <remarks>
    /// 包含当前状态、作业ID、启动时间、已发现URL数量、已处理URL数量、错误数量和配置信息。
    /// </remarks>
    public Task<CrawlState> GetCurrentCrawlStateAsync()
    {
        var state = new CrawlState
        {
            Status = _currentStatus,
            JobId = _currentJobId ?? string.Empty,
            StartTime = _startTime,
            TotalUrlsDiscovered = 0, // 需要从实际数据中获取
            TotalUrlsProcessed = _scheduler.ProcessedCount,
            TotalErrors = 0, // 需要从实际数据中获取
            Configuration = new {}
        };
        return Task.FromResult(state);
    }
    
    /// <summary>
    /// 恢复爬虫
    /// </summary>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 仅在爬虫当前状态为暂停时有效。
    /// </remarks>
    public Task ResumeAsync()
    {
        if (!_isRunning || CurrentStatus != CrawlerStatus.Paused)
        {
            return Task.CompletedTask;
        }

        try
        {
            _pauseSemaphore.Release();
            _currentStatus = CrawlerStatus.Running;
            _logger.LogInformation("Crawler resumed");
        }
        catch (SemaphoreFullException)
        {
            // 如果信号量已经释放，忽略
            _logger.LogWarning("Attempted to resume crawler when not paused");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 开始爬虫（带配置）
    /// </summary>
    /// <param name="config">高级爬取配置</param>
    /// <param name="jobId">任务ID</param>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 开始新的爬取任务，使用提供的高级配置。
    /// </remarks>
    public async Task StartAsync(AdvancedCrawlConfiguration config, string? jobId = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        // 使用配置中的并发任务数，或默认值
        int workerCount = config.MaxConcurrentTasks > 0 ? config.MaxConcurrentTasks : 5;
        await StartAsync(workerCount, jobId);
    }
    
    /// <summary>
    /// 开始爬虫
    /// </summary>
    /// <param name="workerCount">并发任务数</param>
    /// <param name="jobId">任务ID</param>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 开始新的爬取任务，使用提供的并发任务数。
    /// </remarks>
    public async Task StartAsync(int workerCount = 5, string? jobId = null)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Crawler is already running.");
            return;
        }

        try
        {
            _isRunning = true;
            _currentJobId = jobId ?? Guid.NewGuid().ToString();
            _startTime = DateTime.UtcNow;
            CurrentStatus = CrawlerStatus.Running;

            // 保存初始爬取状态
            await SaveInitialCrawlStateAsync();

            // 初始化组件
            await InitializeComponentsAsync();

            // 启动工作线程
            _cancellationTokenSource = new CancellationTokenSource();
            for (int i = 0; i < workerCount; i++)
            {
                _workerTasks.Add(WorkerLoopAsync(i,_cancellationTokenSource.Token));
            }

            _logger.LogInformation("Crawler started with {WorkerCount} workers. Job ID: {JobId}", workerCount, _currentJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start crawler");
            CurrentStatus = CrawlerStatus.Error;
            _isRunning = false;
            throw;
        }
    }

    /// <summary>
    /// 停止爬虫
    /// </summary>
    /// <param name="saveState">是否保存最终状态</param>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 停止当前运行的爬取任务，可选是否保存最终状态。
    /// </remarks>
    public async Task StopAsync(bool saveState = true)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Crawler is not running.");
            return;
        }

        try
        {
            CurrentStatus = CrawlerStatus.Stopping;
            _logger.LogInformation("Stopping crawler...");

            // 取消所有工作线程
            _cancellationTokenSource?.Cancel();

            // 等待所有工作线程完成
            if (_workerTasks.Count > 0)
            {
                await Task.WhenAll(_workerTasks.Select(t => Task.WhenAny(t, Task.Delay(30000))));
            }

            // 保存最终状态
            if (saveState)
            {
                await SaveFinalCrawlStateAsync();
            }

            // 关闭组件
            await ShutdownComponentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping crawler");
            CurrentStatus = CrawlerStatus.Error;
        }
        finally
        {
            _isRunning = false;
            CurrentStatus = CrawlerStatus.Idle;
            _workerTasks.Clear();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _logger.LogInformation("Crawler stopped");
        }
    }

    /// <summary>
    /// 暂停爬虫
    /// </summary>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 暂停当前运行的爬取任务，允许稍后恢复。
    /// </remarks>
    public async Task PauseAsync()
    {
        if (!_isRunning || CurrentStatus == CrawlerStatus.Paused)
        {
            return;
        }

        await _pauseSemaphore.WaitAsync();
        try
        {
            CurrentStatus = CrawlerStatus.Paused;
            _logger.LogInformation("Crawler paused");
        }
        finally
        {
            // 这里不释放信号量，保持暂停状态
        }
    }

    /// <summary>
    /// 添加种子URL
    /// </summary>
    /// <param name="urls">要添加的种子URL列表</param>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 将种子URL添加到爬取队列中，用于初始化爬取。
    /// </remarks>
    public async Task AddSeedUrlsAsync(IEnumerable<string> urls)
    {
        var seedUrls = urls ?? [];
        if (!seedUrls.Any()) return;

        var seedRequests = seedUrls.Select(url => new CrawlRequest
        {
            Url = url,
            Depth = 0,
            Priority = 10 // 种子URL高优先级
        }).ToList();

        var addedCount = await _scheduler.AddUrlsAsync(seedRequests);
        _logger.LogInformation("Added {AddedCount} seed URLs out of {TotalCount}", addedCount, seedRequests.Count);
    }

    /// <summary>
    /// 工作线程循环
    /// </summary>
    /// <param name="workerId">工作线程ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 工作线程循环，用于处理爬取请求。
    /// </remarks>
    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (CurrentStatus == CrawlerStatus.Paused)
                {
                    try
                    {
                        // 等待恢复信号或取消
                        if (await _pauseSemaphore.WaitAsync(100, cancellationToken))
                        {
                            _pauseSemaphore.Release(); // 立即释放，保持运行状态
                            continue;
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                // 获取下一个请求
                var request = await _scheduler.GetNextAsync();
                if (request == null)
                {
                    // 没有更多请求，短暂休息后继续检查
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                // 设置取消令牌
                request.CancellationToken = cancellationToken;

                // 处理请求
                await ProcessRequestAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker {WorkerId} was canceled", workerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in worker {WorkerId}", workerId);
        }
        finally
        {
            _logger.LogInformation("Worker {WorkerId} stopped", workerId);
        }
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    /// <param name="request">要处理的爬取请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 处理单个爬取请求，包括下载、解析和存储。
    /// </remarks>
    private async Task ProcessRequestAsync(CrawlRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return;

        _logger.LogDebug("Processing URL: {Url} (Depth: {Depth})", request.Url, request.Depth);

        try
        {
            // 检查是否超过最大深度
            var crawlConfig = request.Configuration as CrawlConfiguration;
            if (crawlConfig != null && request.Depth > crawlConfig.MaxDepth)
            {
                _logger.LogDebug("URL {Url} exceeds max depth ({Depth})", request.Url, request.Depth);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            // 下载阶段
            var downloadResult = await DownloadContentAsync(request);
            if (downloadResult == null)
            {
                _logger.LogWarning("Failed to download {Url}", request.Url);
                return;
            }

            // 解析阶段
            var parseResult = await ParseContentAsync(downloadResult, request);
            if (parseResult == null)
            {
                _logger.LogWarning("Failed to parse {Url}", request.Url);
                return;
            }

            // 存储阶段
            await StoreResultAsync(parseResult, request);

            // 批量添加发现的链接
            if (parseResult.Links?.Count > 0 && crawlConfig != null && request.Depth < crawlConfig.MaxDepth)
            {
                var discoveredRequests = parseResult.Links.Select(url => new CrawlRequest
                {
                    Url = url,
                    Depth = request.Depth + 1,
                    Priority = request.Priority - 1, // 优先级递减
                    Configuration = request.Configuration
                }).ToList();

                var addedCount = await _scheduler.AddUrlsAsync(discoveredRequests);
                _logger.LogDebug("Discovered {TotalUrls} URLs from {SourceUrl}, added {AddedUrls} to scheduler",
                    parseResult.Links.Count, request.Url, addedCount);

                // 触发URL发现事件
                OnUrlDiscovered?.Invoke(this, new UrlDiscoveredEventArgs
                {
                    SourceUrl = request.Url,
                    DiscoveredUrls = [.. parseResult.Links],
                    AddedUrls = addedCount
                });
            }

            // 触发爬取完成事件
            OnCrawlCompleted?.Invoke(this, new CrawlCompletedEventArgs
            {
                Url = request.Url,
                Depth = request.Depth,
                ContentType = parseResult.ContentType,
                ContentLength = parseResult.Content?.Length ?? 0,
                DiscoveredUrls = parseResult.DiscoveredUrls?.Count ?? 0
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Request processing canceled for {Url}", request.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {Url}", request.Url);
            
            // 触发爬取错误事件
            OnCrawlError?.Invoke(this, new CrawlErrorEventArgs
            {
                Url = request.Url,
                Depth = request.Depth,
                ErrorMessage = ex.Message,
                Exception = ex
            });
        }
    }

    /// <summary>
    /// 下载内容
    /// </summary>
    /// <param name="request">要下载的爬取请求</param>
    /// <returns>下载结果</returns>
    /// <remarks>
    /// 下载指定URL的内容，包括检查robots.txt规则、防机器人检查和重试逻辑。
    /// </remarks>
    private async Task<DownloadResult?> DownloadContentAsync(CrawlRequest request)
    {
        try
        {
            // 检查robots.txt规则
            if (_robotsTxtParser != null && !await _robotsTxtParser.IsAllowedAsync(request.Url))
            {
                _logger.LogDebug("URL {Url} is disallowed by robots.txt", request.Url);
                return null;
            }

            // 防机器人检查
            if (_antiBotService != null && !_antiBotService.ShouldProcess(request.Url))
            {
                _logger.LogDebug("URL {Url} is skipped due to anti-bot rules", request.Url);
                return null;
            }

            // 下载内容
            return await _downloader.DownloadAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download error for {Url}", request.Url);
            
            // 重试逻辑
            try
            {
                string? host = new Uri(request.Url).Host;
                if (!string.IsNullOrEmpty(host) && _retryStrategy != null && await _retryStrategy.ShouldRetryAsync(host, ex, request.RetryCount))
                {
                    request.RetryCount++;
                    await _scheduler.AddUrlAsync(request);
                    _logger.LogInformation("Retrying {Url} ({RetryCount})", request.Url, request.RetryCount);
                }
            }
            catch (UriFormatException) { /* URL格式错误，不重试 */ }
            
            return null;
        }
    }

    /// <summary>
    /// 解析内容
    /// </summary>
    /// <param name="downloadResult">要解析的下载结果</param>
    /// <param name="request">对应的爬取请求</param>
    /// <returns>解析结果</returns>
    /// <remarks>
    /// 解析下载的内容，提取出有用的信息。
    /// </remarks>
    private async Task<ParseResult?> ParseContentAsync(DownloadResult downloadResult, CrawlRequest request)
    {
        try
        {
            return await _parser.ParseAsync(downloadResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse error for {Url}", request.Url);
            return null;
        }
    }

    /// <summary>
    /// 存储结果
    /// </summary>
    /// <param name="parseResult">要存储的解析结果</param>
    /// <param name="request">对应的爬取请求</param>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 将解析结果和爬取请求存储到数据库中。
    /// </remarks>
    private async Task StoreResultAsync(ParseResult parseResult, CrawlRequest request)
    {
        try
        {
            var result = new CrawlResult
            {
                Request = request,
                ParseResult = parseResult,
                ProcessedAt = DateTime.UtcNow
            };
            await _storage.SaveAsync(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage error for {Url}", request.Url);
            throw; // 存储错误应该被上层捕获并处理
        }
    }

    /// <summary>
    /// 初始化组件
    /// </summary>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 初始化所有必要的组件，包括调度器、下载器、解析器、存储和其他服务。
    /// </remarks>
    private async Task InitializeComponentsAsync()
        {
            try
            {
                // 初始化各个组件
                await _scheduler.InitializeAsync();
                await _downloader.InitializeAsync();
                await _parser.InitializeAsync();
                await _storage.InitializeAsync();
                
                // 初始化其他组件
                if (_robotsTxtParser != null) await _robotsTxtParser.InitializeAsync();
                if (_antiBotService != null) await _antiBotService.InitializeAsync();
                if (_retryStrategy != null) await ((_retryStrategy as ICrawlerComponent)?.InitializeAsync() ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing components");
                throw;
            }
        }

    /// <summary>
    /// 关闭组件
    /// </summary>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 关闭所有初始化的组件，释放资源。
    /// </remarks>
    private async Task ShutdownComponentsAsync()
        {
            try
            {
                // 关闭各个组件 - 按相反顺序关闭
                if (_retryStrategy != null) await ((_retryStrategy as ICrawlerComponent)?.ShutdownAsync() ?? Task.CompletedTask);
                if (_antiBotService != null) await _antiBotService.ShutdownAsync();
                if (_robotsTxtParser != null) await _robotsTxtParser.ShutdownAsync();
                
                await _storage.ShutdownAsync();
                await _parser.ShutdownAsync();
                await _downloader.ShutdownAsync();
                await _scheduler.ShutdownAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down components");
            }
        }

    /// <summary>
    /// 保存初始爬取状态
    /// </summary>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 保存当前爬取的初始状态，包括作业ID、开始时间和当前状态。
    /// </remarks>
    private async Task SaveInitialCrawlStateAsync()
    {
        try
        {
            if (_metadataStore != null)
            {
                var state = new CrawlState
                {
                    JobId = _currentJobId ?? string.Empty,
                    StartTime = _startTime,
                    Status = CurrentStatus
                };
                await _metadataStore.SaveCrawlStateAsync(state);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving initial crawl state");
        }
    }

    /// <summary>
    /// 保存最终爬取状态
    /// </summary>
    /// <returns>任务完成时的任务</returns>
    /// <remarks>
    /// 保存当前爬取的最终状态，包括作业ID、开始时间、结束时间和当前状态。
    /// </remarks>
    private async Task SaveFinalCrawlStateAsync()
    {
        try
        {
            if (_metadataStore != null)
            {
                var state = new CrawlState
                {
                    JobId = _currentJobId ?? string.Empty,
                    StartTime = _startTime,
                    EndTime = DateTime.UtcNow,
                    Status = CurrentStatus
                };
                await _metadataStore.SaveCrawlStateAsync(state);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving final crawl state");
        }
    }

    #region IDisposable Implementation
    /// <summary>
    /// 释放所有资源
    /// </summary>
    private bool _disposed = false;
    /// <summary>
    /// 释放所有资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    /// <summary>
    /// 释放所有资源
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _pauseSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
            
            // 释放组件资源
            (_scheduler as IDisposable)?.Dispose();
            (_downloader as IDisposable)?.Dispose();
            (_parser as IDisposable)?.Dispose();
            (_storage as IDisposable)?.Dispose();
            (_metadataStore as IDisposable)?.Dispose();
            (_antiBotService as IDisposable)?.Dispose();
            (_retryStrategy as IDisposable)?.Dispose();
            (_robotsTxtParser as IDisposable)?.Dispose();
        }

        _disposed = true;
    }
    #endregion
}