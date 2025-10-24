// CrawlerCore/CrawlerEngine.cs

using CrawlerCore.AntiBot;
using CrawlerCore.Export;
using CrawlerCore.Health;
using CrawlerCore.Metrics;
using CrawlerCore.Retry;
using CrawlerCore.Robots;
using CrawlerEntity.Configuration;
using CrawlerEntity.Enums;
using CrawlerEntity.Events;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

namespace CrawlerCore;
public class CrawlerEngine(IScheduler scheduler, IDownloader downloader,
    IParser parser, IStorageProvider storage, ILogger<CrawlerEngine> logger)
{
    private readonly IScheduler _scheduler = scheduler;
    private readonly IDownloader _downloader = downloader;
    private readonly IParser _parser = parser;
    private readonly IStorageProvider _storage = storage;
    private readonly ILogger<CrawlerEngine> _logger = logger;

    private readonly List<Task> _workerTasks = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;
    private CrawlerStatus _currentStatus = CrawlerStatus.Idle;
    private DateTime _startTime;
    private string? _currentJobId;
    // 添加新的私有字段
    private AntiBotDetectionService? _antiBotService;
    private AdaptiveRetryStrategy? _retryStrategy;
    private DataExportService? _dataExporter;
    private CrawlerHealthCheck? _healthCheckService;
    private CrawlerMetrics? _metrics;
    private RobotsTxtParser? _robotsTxtParser;

    // 事件定义
    public event EventHandler<CrawlCompletedEventArgs>? OnCrawlCompleted;
    public event EventHandler<CrawlErrorEventArgs>? OnCrawlError;
    public event EventHandler<UrlDiscoveredEventArgs>? OnUrlDiscovered;
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
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 运行时间
    /// </summary>
    public TimeSpan Uptime => _isRunning ? DateTime.UtcNow - _startTime : TimeSpan.Zero;

    /// <summary>
    /// 当前作业ID
    /// </summary>
    public string CurrentJobId => _currentJobId ?? string.Empty;
    public AdvancedCrawlConfiguration AdvancedConfig { get; private set; } = new();

    public async Task StartAsync(AdvancedCrawlConfiguration config)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Crawler is already running");
            return;
        }
        AdvancedConfig = config;
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;
            _currentJobId = Guid.NewGuid().ToString();
            _startTime = DateTime.UtcNow;

            await ChangeStatusAsync(CrawlerStatus.Running, "Starting crawler engine");
            // 初始化新增的服务
            await InitializeAdvancedServicesAsync(config);

            await InitializeComponentsAsync();

            // 启动工作线程
            for (int i = 0; i < config.MaxConcurrentTasks; i++)
            {
                _workerTasks.Add(Task.Run(() => WorkerLoopAsync(_cancellationTokenSource.Token)));
            }

            _logger.LogInformation("Crawler started with {WorkerCount} workers, JobId: {JobId}",
                config.MaxConcurrentTasks, _currentJobId);

            // 保存初始爬虫状态
            await SaveInitialCrawlStateAsync(config);
        }
        catch (Exception ex)
        {
            await ChangeStatusAsync(CrawlerStatus.Error, $"Failed to start crawler: {ex.Message}");
            _logger.LogError(ex, "Failed to start crawler");
            throw;
        }
    }

    // 新增：初始化高级服务
    private async Task InitializeAdvancedServicesAsync(AdvancedCrawlConfiguration config)
    {
        // 初始化反爬虫检测服务
        if (config.EnableAntiBotDetection)
        {
            _antiBotService = new AntiBotDetectionService(null);
            _logger.LogInformation("Anti-bot detection service initialized");
        }

        // 初始化重试策略
        _retryStrategy = new AdaptiveRetryStrategy(null, config.RetryPolicy.MaxRetries);
        _logger.LogInformation("Adaptive retry strategy initialized");

        // 初始化数据导出服务
        _dataExporter = new DataExportService();
        _logger.LogInformation("Data export service initialized");

        // 初始化健康检查服务
        _healthCheckService = new CrawlerHealthCheck(this, _storage as IStorageProvider);
        _logger.LogInformation("Health check service initialized");

        // 初始化指标收集
        _metrics = new CrawlerMetrics();
        _logger.LogInformation("Metrics collection initialized");

        // 初始化 Robots.txt 解析器
        if (config.RespectRobotsTxt)
        {
            _robotsTxtParser = new RobotsTxtParser(null, HttpClientFactory.CreateClient());
            _logger.LogInformation("Robots.txt parser initialized");
        }

        await Task.CompletedTask;
    }

    
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Crawler is not running");
            return;
        }

        try
        {
            await ChangeStatusAsync(CrawlerStatus.Stopped, "Stopping crawler engine");

            _cancellationTokenSource?.Cancel();

            // 等待所有工作线程完成
            await Task.WhenAll(_workerTasks);

            await ShutdownComponentsAsync();

            _isRunning = false;

            // 保存最终爬虫状态
            await SaveFinalCrawlStateAsync();

            _logger.LogInformation("Crawler stopped successfully. Total uptime: {Uptime}", Uptime);
        }
        catch (Exception ex)
        {
            await ChangeStatusAsync(CrawlerStatus.Error, $"Error while stopping crawler: {ex.Message}");
            _logger.LogError(ex, "Error while stopping crawler");
            throw;
        }
    }

    /// <summary>
    /// 暂停爬虫
    /// </summary>
    public async Task PauseAsync()
    {
        if (!_isRunning || CurrentStatus == CrawlerStatus.Paused)
            return;

        await ChangeStatusAsync(CrawlerStatus.Paused, "Crawler paused");
        _logger.LogInformation("Crawler paused");
    }

    /// <summary>
    /// 恢复爬虫
    /// </summary>
    public async Task ResumeAsync()
    {
        if (!_isRunning || CurrentStatus != CrawlerStatus.Paused)
            return;

        await ChangeStatusAsync(CrawlerStatus.Running, "Crawler resumed");
        _logger.LogInformation("Crawler resumed");
    }

    private async Task ChangeStatusAsync(CrawlerStatus newStatus, string message)
    {
        var previousStatus = CurrentStatus;
        CurrentStatus = newStatus;

        _logger.LogInformation("Crawler status changed: {PreviousStatus} -> {NewStatus}: {Message}",
            previousStatus, newStatus, message);

        // 触发状态改变事件
        OnStatusChanged?.Invoke(this, new CrawlerStatusChangedEventArgs
        {
            PreviousStatus = previousStatus,
            CurrentStatus = newStatus,
            Message = message
        });

        // 更新爬虫状态存储
        await UpdateCrawlStateStatusAsync(newStatus);
    }

    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Worker thread started");

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // 如果爬虫处于暂停状态，等待一段时间再检查
                if (CurrentStatus == CrawlerStatus.Paused)
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                var request = await _scheduler.GetNextAsync();
                if (request == null)
                {
                    // 没有更多任务，检查是否应该停止
                    if (_scheduler.QueuedCount == 0 && _scheduler.ProcessedCount > 0)
                    {
                        _logger.LogInformation("No more URLs to process. Stopping crawler.");
                        await StopAsync();
                        break;
                    }

                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                await ProcessRequestAsync(request);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
            }
        }

        _logger.LogDebug("Worker thread stopped");
    }

    private async Task ProcessRequestAsync(CrawlRequest request)
    {
        try
        {
            _logger.LogDebug("Processing request: {Url} (Depth: {Depth})", request.Url, request.Depth);

            // 下载
            var downloadResult = await _downloader.DownloadAsync(request);

            // 解析
            var parseResult = await _parser.ParseAsync(downloadResult);

            // 存储
            var crawlResult = new CrawlResult
            {
                Request = request,
                DownloadResult = downloadResult,
                ParseResult = parseResult,
                ProcessedAt = DateTime.UtcNow,
                ProcessingTime = TimeSpan.FromMilliseconds(downloadResult.DownloadTimeMs)
            };

            await _storage.SaveAsync(crawlResult);

            // 触发爬取完成事件
            OnCrawlCompleted?.Invoke(this, new CrawlCompletedEventArgs { Result = crawlResult });

            // 添加新链接到调度器并触发URL发现事件
            if (parseResult?.Links != null)
            {
                foreach (var link in parseResult.Links)
                {
                    var newRequest = new CrawlRequest
                    {
                        Url = link,
                        Depth = request.Depth + 1,
                        Referrer = request.Url
                    };

                    var added = await _scheduler.AddUrlAsync(newRequest);
                    if (added)
                    {
                        // 触发URL发现事件
                        OnUrlDiscovered?.Invoke(this, new UrlDiscoveredEventArgs
                        {
                            SourceUrl = request.Url,
                            DiscoveredUrl = link,
                            Depth = newRequest.Depth
                        });

                        _logger.LogDebug("Discovered new URL: {Url} from {SourceUrl}", link, request.Url);
                    }
                }
            }

            // 更新统计信息
            await UpdateCrawlStatisticsAsync(crawlResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed for {Url}", request.Url);

            // 触发错误事件
            OnCrawlError?.Invoke(this, new CrawlErrorEventArgs
            {
                Request = request,
                Exception = ex
            });

            // 更新错误统计
            await UpdateErrorStatisticsAsync(request.Url, ex.Message);
        }
    }

    private async Task InitializeComponentsAsync()
    {
        _logger.LogDebug("Initializing crawler components");

        await _scheduler.InitializeAsync();
        await _downloader.InitializeAsync();
        await _parser.InitializeAsync();
        await _storage.InitializeAsync();

        _logger.LogDebug("All crawler components initialized");
    }

    private async Task ShutdownComponentsAsync()
    {
        _logger.LogDebug("Shutting down crawler components");

        await _storage.ShutdownAsync();
        await _parser.ShutdownAsync();
        await _downloader.ShutdownAsync();
        await _scheduler.ShutdownAsync();

        _logger.LogDebug("All crawler components shut down");
    }

    public async Task AddSeedUrlsAsync(IEnumerable<string> urls)
    {
        if (urls == null)
            return;

        foreach (var url in urls)
        {
            var request = new CrawlRequest
            {
                Url = url,
                Depth = 0,
                Priority = 10 // 种子URL高优先级
            };

            var added = await _scheduler.AddUrlAsync(request);
            if (added)
            {
                _logger.LogInformation("Added seed URL: {Url}", url);

                // 触发URL发现事件（种子URL）
                OnUrlDiscovered?.Invoke(this, new UrlDiscoveredEventArgs
                {
                    SourceUrl = string.Empty, // 种子URL没有来源
                    DiscoveredUrl = url,
                    Depth = 0
                });
            }
        }
    }

    /// <summary>
    /// 获取当前统计信息
    /// </summary>
    public async Task<CrawlStatistics> GetStatisticsAsync()
    {
        if (_storage is IStorageProvider storageProvider)
        {
            return await storageProvider.GetStatisticsAsync();
        }

        // 如果没有存储提供者，返回基本统计
        return new CrawlStatistics
        {
            TotalUrlsProcessed = _scheduler.ProcessedCount,
            StartTime = _startTime,
            LastUpdateTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 获取当前爬虫状态
    /// </summary>
    public async Task<CrawlState?> GetCurrentCrawlStateAsync()
    {
        if (_storage is IMetadataStore metadataStore)
        {
            return await metadataStore.GetCrawlStateAsync(_currentJobId);
        }

        return new CrawlState
        {
            JobId = _currentJobId!,
            StartTime = _startTime,
            Status = CurrentStatus
        };
    }

    private async Task SaveInitialCrawlStateAsync(CrawlConfiguration config)
    {
        if (_storage is IMetadataStore metadataStore)
        {
            var initialState = new CrawlState
            {
                JobId = _currentJobId!,
                StartTime = _startTime,
                Status = CrawlerStatus.Running,
                Configuration = config,
                Statistics = new CrawlStatistics
                {
                    StartTime = _startTime,
                    LastUpdateTime = DateTime.UtcNow
                }
            };

            await metadataStore.SaveCrawlStateAsync(initialState);
            _logger.LogDebug("Initial crawl state saved for job: {JobId}", _currentJobId);
        }
    }

    private async Task SaveFinalCrawlStateAsync()
    {
        if (_storage is IMetadataStore metadataStore)
        {
            var currentState = await metadataStore.GetCrawlStateAsync(_currentJobId);
            if (currentState != null)
            {
                currentState.EndTime = DateTime.UtcNow;
                currentState.Status = CrawlerStatus.Stopped;
                await metadataStore.SaveCrawlStateAsync(currentState);
                _logger.LogDebug("Final crawl state saved for job: {JobId}", _currentJobId);
            }
        }
    }

    private async Task UpdateCrawlStateStatusAsync(CrawlerStatus status)
    {
        if (_storage is IMetadataStore metadataStore)
        {
            var currentState = await metadataStore.GetCrawlStateAsync(_currentJobId);
            if (currentState != null)
            {
                currentState.Status = status;
                await metadataStore.SaveCrawlStateAsync(currentState);
            }
        }
    }

    private async Task UpdateCrawlStatisticsAsync(CrawlResult result)
    {
        if (_storage is IMetadataStore metadataStore)
        {
            var currentState = await metadataStore.GetCrawlStateAsync(_currentJobId);
            if (currentState != null)
            {
                currentState.TotalUrlsProcessed++;
                if (result.DownloadResult.IsSuccess)
                {
                    currentState.Statistics!.SuccessCount++;
                }
                else
                {
                    currentState.Statistics!.ErrorCount++;
                }

                currentState.Statistics.LastUpdateTime = DateTime.UtcNow;
                await metadataStore.SaveCrawlStateAsync(currentState);
            }
        }
    }

    private async Task UpdateErrorStatisticsAsync(string url, string errorMessage)
    {
        if (_storage is IMetadataStore metadataStore)
        {
            var currentState = await metadataStore.GetCrawlStateAsync(_currentJobId);
            if (currentState != null)
            {
                currentState.TotalErrors++;
                currentState.Statistics!.ErrorCount++;
                currentState.Statistics.LastUpdateTime = DateTime.UtcNow;
                await metadataStore.SaveCrawlStateAsync(currentState);
            }

            // 保存URL错误状态
            var urlState = new UrlState
            {
                Url = url,
                DiscoveredAt = DateTime.UtcNow.AddMinutes(-1),
                ProcessedAt = DateTime.UtcNow,
                StatusCode = 0,
                ErrorMessage = errorMessage
            };
            await metadataStore.SaveUrlStateAsync(urlState);
        }
    }
}
// 简单的 HttpClient 工厂
public class HttpClientFactory
{
    public static HttpClient CreateClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}