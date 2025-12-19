// <copyright file="CrawlerEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerCore;

using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using CrawlerCore.AntiBot;
using CrawlerCore.ErrorHandling;
using CrawlerCore.Metrics;
using CrawlerCore.Plugins;
using CrawlerCore.Retry;
using CrawlerCore.Robots;
using CrawlerFramework.CrawlerEntity.Configuration;
using CrawlerFramework.CrawlerEntity.Enums;
using CrawlerFramework.CrawlerEntity.Events;
using CrawlerFramework.CrawlerEntity.Models;
using CrawlerFramework.CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using CrawlerFramework.CrawlerCore.Exceptions;

/// <summary>
/// 爬虫引擎，负责协调和管理整个爬取任务的执行流程。.
/// </summary>
/// <remarks>
/// <para>CrawlerEngine是整个爬虫框架的核心组件，负责：.</para>
/// <list type="bullet">
/// <item>管理工作线程池，根据队列负载动态调整线程数量</item>
/// <item>协调下载器、解析器和存储提供器的工作流程</item>
/// <item>处理爬虫的启动、暂停、恢复和停止操作</item>
/// <item>管理爬取任务的优先级和执行顺序</item>
/// <item>提供统计信息和状态监控</item>
/// <item>支持反机器人检测和自适应重试策略</item>
/// <item>处理种子URL的添加和初始化</item>
/// <item>监控爬取状态并触发相应事件</item>
/// </list>
/// </remarks>

/// <example>
/// <code>
/// var engine = new CrawlerEngine(scheduler, downloader, parser, storage, logger);
/// await engine.AddSeedUrlsAsync(new[] { "https://example.com" });
/// await engine.StartAsync(5);
/// </code>
/// </example>
public class CrawlerEngine : IDisposable
{
    /// <summary>
    /// 任务调度器，负责管理爬取请求队列和优先级.
    /// </summary>
    private readonly IScheduler scheduler;

    /// <summary>
    /// 下载器，负责从网络获取网页内容.
    /// </summary>
    private readonly IDownloader downloader;

    /// <summary>
    /// 解析器，负责解析下载的内容并提取信息.
    /// </summary>
    private readonly IParser parser;

    /// <summary>
    /// 存储提供器，负责存储爬取结果和元数据.
    /// </summary>
    private readonly IStorageProvider storage;

    /// <summary>
    /// 元数据存储，用于存储爬取任务的元数据（可选）.
    /// </summary>
    private readonly IMetadataStore? metadataStore;

    /// <summary>
    /// 日志记录器，用于记录运行时信息和错误.
    /// </summary>
    private readonly ILogger<CrawlerEngine> logger;

    /// <summary>
    /// 工作任务列表，用于存储正在运行的工作线程任务.
    /// </summary>
    private readonly List<Task> workerTasks = [];

    /// <summary>
    /// 暂停信号量，用于控制爬虫暂停和恢复.
    /// </summary>
    private readonly SemaphoreSlim pauseSemaphore = new (1, 1);

    /// <summary>
    /// 自适应线程池配置，用于动态调整工作线程数量.
    /// </summary>
    private readonly int minWorkerCount = 1;

    /// <summary>
    /// 最大工作线程数，用于限制工作线程的最大数量.
    /// </summary>
    private readonly int maxWorkerCount = Environment.ProcessorCount * 4;

    /// <summary>
    /// 高队列阈值，用于触发增加工作线程.
    /// </summary>
    private readonly int queueThresholdHigh = 50;

    /// <summary>
    /// 低队列阈值，用于触发减少工作线程.
    /// </summary>
    private readonly int queueThresholdLow = 10;

    /// <summary>
    /// 线程调整间隔，用于定时检查和调整工作线程数量.
    /// </summary>
    private readonly int threadAdjustIntervalMs = 5000;

    /// <summary>
    /// 自动停止配置.
    /// </summary>
    private readonly bool enableAutoStop; // 是否启用自动停止

    /// <summary>
    /// 任务队列为空超时时间，用于判断是否触发自动停止.
    /// </summary>
    private readonly TimeSpan autoStopTimeout; // 任务队列为空超时时间

    /// <summary>
    /// 用于保护_workerTasks的锁.
    /// </summary>
    private readonly Lock workerTasksLock = new ();

    // 通过构造函数注入的服务

    /// <summary>
    /// 反机器人服务.
    /// </summary>
    private readonly AntiBotDetectionService? antiBotService;

    /// <summary>
    /// 重试策略.
    /// </summary>
    private readonly AdaptiveRetryStrategy? retryStrategy;

    /// <summary>
    /// 错误处理服务.
    /// </summary>
    private readonly IErrorHandlingService? errorHandlingService;

    /// <summary>
    /// Robots.txt解析器.
    /// </summary>
    private readonly RobotsTxtParser? robotsTxtParser;

    /// <summary>
    /// 指标服务.
    /// </summary>
    private readonly ICrawlerMetrics? metrics;

    /// <summary>
    /// 插件加载器.
    /// </summary>
    private readonly IPluginLoader? pluginLoader;

    /// <summary>
    /// 按类型分类的已加载插件字典.
    /// </summary>
    private readonly Dictionary<PluginType, List<IPlugin>> pluginsByType = [];

    /// <summary>
    /// 插件实例缓存，用于复用插件实例而非每次重新创建.
    /// </summary>
    private readonly Dictionary<Type, ICrawlerComponent> pluginInstanceCache = [];

    /// <summary>
    /// 统计信息更新间隔（毫秒）.
    /// </summary>
    private readonly int statisticsUpdateIntervalMs = 30000; // 默认30秒

    /// <summary>
    /// 保护统计信息的锁.
    /// </summary>
    private readonly Lock statisticsLock = new ();

    /// <summary>
    /// 当前使用的爬取配置.
    /// </summary>
    private AdvancedCrawlConfiguration? currentConfig;

    /// <summary>
    /// 需要移除的工作线程数.
    /// </summary>
    private int workersToRemove = 0;

    /// <summary>
    /// 取消令牌源，用于取消所有工作线程任务.
    /// </summary>
    private CancellationTokenSource? cancellationTokenSource;

    /// <summary>
    /// 是否正在运行，用于判断爬虫是否正在工作.
    /// </summary>
    private bool isRunning;

    /// <summary>
    /// 当前状态，用于记录爬虫当前的运行状态.
    /// </summary>
    private CrawlerStatus currentStatus = CrawlerStatus.Idle;

    /// <summary>
    /// 启动时间，记录爬虫启动的时间点.
    /// </summary>
    private DateTime startTime;

    /// <summary>
    /// 当前作业ID，用于标识当前运行的作业.
    /// </summary>
    private string? currentJobId = string.Empty;

    /// <summary>
    /// 任务队列为空超时时间，用于判断是否触发自动停止.
    /// </summary>
    private DateTime? queueEmptyStartTime; // 任务队列为空的起始时间

    /// <summary>
    /// 线程池监控任务.
    /// </summary>
    private Task? threadMonitoringTask;

    /// <summary>
    /// 统计信息更新任务.
    /// </summary>
    private Task? statisticsUpdateTask;

    /// <summary>
    /// 当前爬取统计信息.
    /// </summary>
    private CrawlStatistics? currentStatistics;

    /// <summary>
    /// 释放所有资源.
    /// </summary>
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrawlerEngine"/> class.
    /// 初始化 <see cref="CrawlerEngine"/> 类的新实例.
    /// </summary>
    /// <param name="scheduler">任务调度器，负责管理爬取请求队列和优先级.</param>
    /// <param name="downloader">下载器，负责从网络获取网页内容.</param>
    /// <param name="parser">解析器，负责解析下载的内容并提取信息.</param>
    /// <param name="storage">存储提供器，负责存储爬取结果和元数据.</param>
    /// <param name="logger">日志记录器，用于记录运行时信息和错误.</param>
    /// <param name="metadataStore">元数据存储，用于存储爬取任务的元数据（可选）.</param>
    /// <param name="antiBotService">反机器人服务，用于检测和应对反爬机制（可选）.</param>
    /// <param name="retryStrategy">重试策略，用于处理下载失败的情况（可选）.</param>
    /// <param name="errorHandlingService">错误处理服务，用于统一处理各种错误情况（可选）.</param>
    /// <param name="robotsTxtParser">Robots.txt解析器，用于遵守网站的爬取规则（可选）.</param>
    /// <param name="metrics">指标服务，用于收集和报告爬取性能数据（可选）.</param>
    /// <param name="pluginLoader">插件加载器，用于加载和管理爬虫插件（可选）.</param>
    /// <param name="enableAutoStop">是否启用自动停止功能，当任务队列为空时自动停止（默认：true）.</param>
    /// <param name="autoStopTimeout">自动停止超时时间，当任务队列为空超过此时间时自动停止（默认：30秒）.</param>
    public CrawlerEngine(
        IScheduler scheduler,
        IDownloader downloader,
        IParser parser,
        IStorageProvider storage,
        ILogger<CrawlerEngine> logger,
        IMetadataStore? metadataStore = null,
        AntiBotDetectionService? antiBotService = null,
        AdaptiveRetryStrategy? retryStrategy = null,
        IErrorHandlingService? errorHandlingService = null,
        RobotsTxtParser? robotsTxtParser = null,
        ICrawlerMetrics? metrics = null,
        IPluginLoader? pluginLoader = null,
        bool enableAutoStop = true,
        TimeSpan? autoStopTimeout = null)
    {
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler), "任务调度器参数不能为空");
        this.downloader = downloader ?? throw new ArgumentNullException(nameof(downloader), "下载器参数不能为空");
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser), "解析器参数不能为空");
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage), "存储提供器参数不能为空");
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger), "日志记录器参数不能为空");
        this.metadataStore = metadataStore;
        this.antiBotService = antiBotService;
        this.retryStrategy = retryStrategy;
        this.errorHandlingService = errorHandlingService;
        this.robotsTxtParser = robotsTxtParser;
        this.metrics = metrics;
        this.pluginLoader = pluginLoader;
        this.enableAutoStop = enableAutoStop;
        this.autoStopTimeout = autoStopTimeout ?? TimeSpan.FromSeconds(30);
    }

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
    /// Gets 当前状态.
    /// </summary>
    public CrawlerStatus CurrentStatus
    {
        get => this.currentStatus;
        private set
        {
            if (this.currentStatus != value)
            {
                var previousStatus = this.currentStatus;
                this.currentStatus = value;
                this.OnStatusChanged?.Invoke(this, new CrawlerStatusChangedEventArgs
                {
                    PreviousStatus = previousStatus,
                    CurrentStatus = value,
                    Message = $"Status changed from {previousStatus} to {value}",
                });
            }
        }
    }

    /// <summary>
    /// Gets 爬虫运行时间.
    /// </summary>
    public TimeSpan Uptime
    {
        get
        {
            if (this.currentStatus == CrawlerStatus.Running)
            {
                return DateTime.UtcNow - this.startTime;
            }

            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Gets a value indicating whether 是否正在运行.
    /// </summary>
    public bool IsRunning => this.isRunning;

    /// <summary>
    /// Gets 当前任务ID.
    /// </summary>
    public string? CurrentJobId => this.currentJobId;

    /// <summary>
    /// 获取指标服务.
    /// </summary>
    /// <returns>指标服务实例.</returns>
    public CrawlerMetrics? GetMetrics()
    {
        return (CrawlerMetrics?)this.metrics;
    }

    /// <summary>
    /// 根据插件类型获取已加载的插件列表.
    /// </summary>
    /// <param name="pluginType">插件类型.</param>
    /// <returns>指定类型的插件列表.</returns>
    private List<IPlugin> GetPluginsByType(PluginType pluginType)
    {
        if (this.pluginsByType.TryGetValue(pluginType, out var plugins) && plugins != null)
        {
            return plugins;
        }

        return [];
    }

    /// <summary>
    /// 获取或创建插件实例，实现插件实例复用.
    /// </summary>
    /// <typeparam name="T">插件接口类型.</typeparam>
    /// <param name="plugin">插件元数据.</param>
    /// <returns>插件实例.</returns>
    private async Task<T> GetOrCreatePluginInstanceAsync<T>(IPlugin plugin)
        where T : class, ICrawlerComponent
    {
        if (!this.pluginInstanceCache.TryGetValue(plugin.EntryPointType, out var instance))
        {
            // 如果缓存中没有实例，创建并初始化一个新实例
            instance = (ICrawlerComponent)Activator.CreateInstance(plugin.EntryPointType) !;
            await instance.InitializeAsync();
            this.pluginInstanceCache[plugin.EntryPointType] = instance;
            this.logger.LogInformation("Created and initialized plugin instance for '{Name}'", plugin.PluginName);
        }

        return (T)instance;
    }

    /// <summary>
    /// 获取统计信息.
    /// </summary>
    /// <returns>包含爬虫状态、运行时间、任务ID、工作线程数、队列长度、已处理URL数量的字典.</returns>
    /// <remarks>
    /// 包含当前状态、运行时间、任务ID、工作线程数、队列长度、已处理URL数量。.
    /// </remarks>
    public Task<Dictionary<string, object>> GetStatisticsAsync()
    {
        var stats = new Dictionary<string, object>
        {
            { "Status", this.currentStatus },
            { "Uptime", this.Uptime },
            { "JobId", this.currentJobId ?? string.Empty },
            { "WorkerCount", this.workerTasks.Count },
            { "QueueLength", this.scheduler.QueuedCount },
            { "ProcessedCount", this.scheduler.ProcessedCount },
        };
        return Task.FromResult(stats);
    }

    /// <summary>
    /// 获取当前爬取状态.
    /// </summary>
    /// <returns>当前爬取状态.</returns>
    /// <remarks>
    /// 包含当前状态、作业ID、启动时间、已发现URL数量、已处理URL数量、错误数量和配置信息。.
    /// </remarks>
    public Task<CrawlState> GetCurrentCrawlStateAsync()
    {
        var state = new CrawlState
        {
            Status = this.currentStatus,
            JobId = this.currentJobId ?? string.Empty,
            StartTime = this.startTime,
            TotalUrlsDiscovered = this.scheduler.QueuedCount + this.scheduler.ProcessedCount, // 从调度器获取实际值
            TotalUrlsProcessed = this.scheduler.ProcessedCount,
            TotalErrors = this.scheduler.ErrorCount, // 从调度器获取错误计数
            Configuration = this.currentConfig ?? new AdvancedCrawlConfiguration(),
        };
        return Task.FromResult(state);
    }

    /// <summary>
    /// 恢复爬虫.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 仅在爬虫当前状态为暂停时有效。.
    /// </remarks>
    public Task ResumeAsync()
    {
        if (!this.isRunning || this.CurrentStatus != CrawlerStatus.Paused)
        {
            return Task.CompletedTask;
        }

        try
        {
            this.pauseSemaphore.Release();
            this.currentStatus = CrawlerStatus.Running;
            this.logger.LogInformation("Crawler resumed");
        }
        catch (SemaphoreFullException)
        {
            // 如果信号量已经释放，忽略
            this.logger.LogWarning("Attempted to resume crawler when not paused");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 开始爬虫（带配置）.
    /// </summary>
    /// <param name="config">高级爬取配置.</param>
    /// <param name="jobId">任务ID.</param>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 开始新的爬取任务，使用提供的高级配置。.
    /// </remarks>
    public async Task StartAsync(AdvancedCrawlConfiguration config, string? jobId = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.currentConfig = config;

        // 使用配置中的并发任务数，或默认值
        int workerCount = config.MaxConcurrentTasks > 0 ? config.MaxConcurrentTasks : 5;
        await this.StartAsync(workerCount, jobId);
    }

    /// <summary>
    /// 开始爬虫.
    /// </summary>
    /// <param name="workerCount">并发任务数.</param>
    /// <param name="jobId">任务ID.</param>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 开始新的爬取任务，使用提供的并发任务数。.
    /// </remarks>
    public async Task StartAsync(int workerCount = 5, string? jobId = null)
    {
        if (this.isRunning)
        {
            this.logger.LogWarning("Crawler is already running.");
            return;
        }

        try
        {
            this.isRunning = true;
            this.currentJobId = jobId ?? Guid.NewGuid().ToString();
            this.startTime = DateTime.UtcNow;
            this.CurrentStatus = CrawlerStatus.Running;

            // 保存初始爬取状态
            await this.SaveInitialCrawlStateAsync();

            // 初始化组件
            await this.InitializeComponentsAsync();

            // 启动工作线程
            this.cancellationTokenSource = new CancellationTokenSource();
            for (int i = 0; i < workerCount; i++)
            {
                this.workerTasks.Add(this.WorkerLoopAsync(i, this.cancellationTokenSource.Token));
            }

            // 启动线程池监控任务
            this.threadMonitoringTask = Task.Run(() => this.MonitorAndAdjustThreadPoolAsync(this.cancellationTokenSource.Token));

            // 初始化统计信息并启动更新任务
            this.currentStatistics = new CrawlStatistics();
            this.statisticsUpdateTask = Task.Run(() => this.UpdateStatisticsPeriodicallyAsync(this.cancellationTokenSource.Token));

            this.logger.LogInformation("Crawler started with {WorkerCount} workers. Job ID: {JobId}", workerCount, this.currentJobId);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to start crawler");
            this.CurrentStatus = CrawlerStatus.Error;
            this.isRunning = false;
            throw;
        }
    }

    /// <summary>
    /// 停止爬虫.
    /// </summary>
    /// <param name="saveState">是否保存最终状态.</param>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 停止当前运行的爬取任务，可选是否保存最终状态。.
    /// </remarks>
    public async Task StopAsync(bool saveState = true)
    {
        if (!this.isRunning)
        {
            this.logger.LogWarning("Crawler is not running.");
            return;
        }

        try
        {
            this.CurrentStatus = CrawlerStatus.Stopping;
            this.logger.LogInformation("Stopping crawler...");

            // 取消所有工作线程
            this.cancellationTokenSource?.Cancel();

            // 等待所有工作线程完成
            if (this.workerTasks.Count > 0)
            {
                await Task.WhenAll(this.workerTasks.Select(t => Task.WhenAny(t, Task.Delay(30000))));
            }

            // 保存最终状态
            if (saveState)
            {
                await this.SaveFinalCrawlStateAsync();
            }

            // 关闭组件
            await this.ShutdownComponentsAsync();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error stopping crawler");
            this.CurrentStatus = CrawlerStatus.Error;
        }
        finally
        {
            this.isRunning = false;
            this.CurrentStatus = CrawlerStatus.Idle;
            this.workerTasks.Clear();
            this.cancellationTokenSource?.Dispose();
            this.cancellationTokenSource = null;
            this.logger.LogInformation("Crawler stopped");
        }
    }

    /// <summary>
    /// 定期更新统计信息.
    /// </summary>
    /// <param name="cancellationToken">取消令牌，用于请求停止任务.</param>
    /// <returns>任务完成时的任务.</returns>
    private async Task UpdateStatisticsPeriodicallyAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Statistics update task started");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(this.statisticsUpdateIntervalMs, cancellationToken);

                try
                {
                    await this.UpdateStatisticsAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to update statistics");
                }
            }
        }
        catch (OperationCanceledException)
        {
            this.logger.LogInformation("Statistics update task canceled");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error in statistics update task");
        }
    }

    /// <summary>
    /// 更新统计信息.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    private async Task UpdateStatisticsAsync()
    {
        lock (this.statisticsLock)
        {
            this.currentStatistics ??= new CrawlStatistics();

            // 更新全局统计信息
            this.currentStatistics.TotalUrlsDiscovered = this.scheduler.QueuedCount + this.scheduler.ProcessedCount;
            this.currentStatistics.TotalUrlsProcessed = this.scheduler.ProcessedCount;
            this.currentStatistics.ErrorCount = this.scheduler.ErrorCount;
            this.currentStatistics.SuccessCount = this.scheduler.ProcessedCount - this.scheduler.ErrorCount;
            this.currentStatistics.LastUpdateTime = DateTime.UtcNow;
        }

        // 持久化统计信息
        try
        {
            await this.PersistStatisticsAsync();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to persist statistics");
        }
    }

    /// <summary>
    /// 持久化统计信息.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    private async Task PersistStatisticsAsync()
    {
        lock (this.statisticsLock)
        {
            if (this.currentStatistics == null)
            {
                return;
            }
        }

        try
        {
            // 保存到存储提供器
            await this.storage.SaveStatisticsAsync(this.currentStatistics);

            // 同时更新元数据存储中的爬取状态
            if (this.metadataStore != null)
            {
                var crawlState = await this.GetCurrentCrawlStateAsync();
                crawlState.Statistics = this.currentStatistics;
                await this.metadataStore.SaveCrawlStateAsync(crawlState);
            }

            this.logger.LogDebug("Statistics persisted successfully");
        }
        catch (NotImplementedException)
        {
            // 如果存储提供器不支持保存统计信息，忽略此错误
            this.logger.LogDebug("Storage provider does not support saving statistics");
        }
    }

    /// <summary>
    /// 暂停爬虫.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 暂停当前运行的爬取任务，允许稍后恢复。.
    /// </remarks>
    public async Task PauseAsync()
    {
        if (!this.isRunning || this.CurrentStatus == CrawlerStatus.Paused)
        {
            return;
        }

        await this.pauseSemaphore.WaitAsync();
        try
        {
            this.CurrentStatus = CrawlerStatus.Paused;
            this.logger.LogInformation("Crawler paused");
        }
        finally
        {
            // 这里不释放信号量，保持暂停状态
        }
    }

    /// <summary>
    /// 添加种子URL.
    /// </summary>
    /// <param name="urls">要添加的种子URL列表.</param>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 将种子URL添加到爬取队列中，用于初始化爬取。.
    /// </remarks>
    public async Task AddSeedUrlsAsync(IEnumerable<string> urls)
    {
        var seedUrls = urls ?? [];
        if (!seedUrls.Any())
        {
            return;
        }

        var seedRequests = seedUrls.Select(url => new CrawlRequest
        {
            Url = url,
            Depth = 0,
            Priority = 10, // 种子URL高优先级
        }).ToList();

        var addedCount = await this.scheduler.AddUrlsAsync(seedRequests);
        this.logger.LogInformation("Added {AddedCount} seed URLs out of {TotalCount}", addedCount, seedRequests.Count);
    }

    /// <summary>
    /// 释放所有资源.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放所有资源.
    /// </summary>
    /// <param name="disposing">是否释放托管资源.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            this.pauseSemaphore?.Dispose();
            this.cancellationTokenSource?.Dispose();

            // 释放组件资源
            (this.scheduler as IDisposable)?.Dispose();
            (this.downloader as IDisposable)?.Dispose();
            (this.parser as IDisposable)?.Dispose();
            (this.storage as IDisposable)?.Dispose();
            (this.metadataStore as IDisposable)?.Dispose();
            (this.antiBotService as IDisposable)?.Dispose();
            (this.retryStrategy as IDisposable)?.Dispose();
            (this.robotsTxtParser as IDisposable)?.Dispose();
        }

        this.disposed = true;
    }

    /// <summary>
    /// 工作线程循环.
    /// </summary>
    /// <param name="workerId">工作线程ID.</param>
    /// <param name="cancellationToken">取消令牌，用于请求停止工作线程.</param>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 工作线程循环，用于持续处理爬取请求。每个工作线程：.
    /// <list type="bullet">
    /// <item>从调度器获取下一个待处理的爬取请求</item>
    /// <item>处理暂停状态，等待恢复信号</item>
    /// <item>执行爬取流程：下载内容、解析内容、存储结果</item>
    /// <item>处理取消请求，安全退出线程</item>
    /// </list>
    /// </remarks>
    private async Task WorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Worker {WorkerId} started", workerId);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.CurrentStatus == CrawlerStatus.Paused)
                {
                    try
                    {
                        // 等待恢复信号或取消
                        if (await this.pauseSemaphore.WaitAsync(100, cancellationToken))
                        {
                            this.pauseSemaphore.Release(); // 立即释放，保持运行状态
                            continue;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                // 获取下一个请求
                var request = await this.scheduler.GetNextAsync();
                if (request == null)
                {
                    // 没有更多请求，检查是否需要移除线程
                    if (Interlocked.Decrement(ref this.workersToRemove) >= 0)
                    {
                        this.logger.LogInformation("Worker {WorkerId} exiting naturally as part of thread reduction", workerId);
                        break;
                    }
                    else
                    {
                        // 如果不需要移除，将计数器加回
                        Interlocked.Increment(ref this.workersToRemove);
                    }

                    // 短暂休息后继续检查
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                // 设置取消令牌
                request.CancellationToken = cancellationToken;

                // 处理请求
                await this.ProcessRequestAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            this.logger.LogInformation("Worker {WorkerId} was canceled", workerId);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error in worker {WorkerId}", workerId);
        }
        finally
        {
            this.logger.LogInformation("Worker {WorkerId} stopped", workerId);
        }
    }

    /// <summary>
    /// 处理单个爬取请求.
    /// </summary>
    /// <param name="request">要处理的爬取请求.</param>
    /// <param name="cancellationToken">取消令牌，用于中断爬取过程.</param>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 处理单个爬取请求的完整流程：.
    /// <list type="bullet">
    /// <item>检查请求是否为空</item>
    /// <item>检查是否超过最大爬取深度</item>
    /// <item>下载网页内容</item>
    /// <item>解析下载的内容，提取信息和新的URL</item>
    /// <item>存储解析后的结果</item>
    /// <item>将新发现的URL添加到爬取队列</item>
    /// <item>记录域名性能数据</item>
    /// <item>触发相应的事件（如URL发现、爬取完成、错误发生等）</item>
    /// </list>
    /// </remarks>
    /// <exception cref="OperationCanceledException">当爬取过程被取消时抛出.</exception>
    private async Task ProcessRequestAsync(CrawlRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return;
        }

        this.logger.LogDebug("Processing URL: {Url} (Depth: {Depth})", request.Url, request.Depth);

        try
        {
            // 检查是否超过最大深度
            var crawlConfig = request.Configuration as CrawlConfiguration;
            if (crawlConfig != null && request.Depth > crawlConfig.MaxDepth)
            {
                this.logger.LogDebug("URL {Url} exceeds max depth ({Depth})", request.Url, request.Depth);
                this.metrics?.RecordUrlFailed(request.Url);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 下载阶段
            var downloadResult = await this.DownloadContentAsync(request);
            if (downloadResult == null)
            {
                this.logger.LogWarning("Failed to download {Url}", request.Url);
                this.metrics?.RecordUrlFailed(request.Url);
                return;
            }

            // 记录域名性能数据用于动态调整任务优先级
            try
            {
                var domain = new Uri(request.Url).Host;
                this.scheduler.RecordDomainPerformance(domain, downloadResult.DownloadTimeMs, downloadResult.IsSuccess);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to record domain performance for {Url}", request.Url);
            }

            // 解析阶段
            var parseResult = await this.ParseContentAsync(downloadResult, request);
            if (parseResult == null)
            {
                this.logger.LogWarning("Failed to parse {Url}", request.Url);
                this.metrics?.RecordUrlFailed(request.Url);
                return;
            }

            // 存储阶段
            var storageStartTime = Stopwatch.GetTimestamp();
            await this.StoreResultAsync(parseResult, request);
            var storageDurationMs = (double)(Stopwatch.GetTimestamp() - storageStartTime) * 1000 / Stopwatch.Frequency;

            // 记录成功处理的URL和各个阶段的指标
            if (this.metrics != null)
            {
                try
                {
                    var domain = new Uri(request.Url).Host;
                    var contentLength = downloadResult.Content?.Length ?? 0;
                    var statusCode = downloadResult.StatusCode;

                    this.metrics.RecordUrlProcessed(
                        domain,
                        statusCode,
                        contentLength,
                        downloadResult.DownloadTimeMs,
                        parseResult.ParseTimeMs,
                        storageDurationMs);

                    // 记录下载的字节数
                    this.metrics.RecordBytesDownloaded(contentLength);

                    // 记录HTTP状态码
                    this.metrics.RecordHttpStatusCode(statusCode, domain);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to record metrics for {Url}", request.Url);
                }
            }

            // 批量添加发现的链接
            if (parseResult.Links?.Count > 0 && crawlConfig != null && request.Depth < crawlConfig.MaxDepth)
            {
                var discoveredRequests = parseResult.Links.Select(url => new CrawlRequest
                {
                    Url = url,
                    Depth = request.Depth + 1,
                    Priority = request.Priority - 1, // 优先级递减
                    Configuration = request.Configuration,
                }).ToList();

                var addedCount = await this.scheduler.AddUrlsAsync(discoveredRequests);
                this.logger.LogDebug(
                    "Discovered {TotalUrls} URLs from {SourceUrl}, added {AddedUrls} to scheduler",
                    parseResult.Links.Count,
                    request.Url,
                    addedCount);

                // 触发URL发现事件
                this.OnUrlDiscovered?.Invoke(this, new UrlDiscoveredEventArgs
                {
                    SourceUrl = request.Url,
                    DiscoveredUrls = [.. parseResult.Links],
                    AddedUrls = addedCount,
                });
            }

            // 触发爬取完成事件
            this.OnCrawlCompleted?.Invoke(this, new CrawlCompletedEventArgs
            {
                Url = request.Url,
                Depth = request.Depth,
                ContentType = parseResult.ContentType,
                ContentLength = parseResult.Content?.Length ?? 0,
                DiscoveredUrls = parseResult.DiscoveredUrls?.Count ?? 0,
            });
        }
        catch (OperationCanceledException)
        {
            this.logger.LogDebug("Request processing canceled for {Url}", request.Url);
            this.metrics?.RecordUrlFailed(request.Url);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error processing {Url}", request.Url);

            // 记录错误
            try
            {
                var domain = new Uri(request.Url).Host;
                this.metrics?.RecordError(domain, ex.GetType().Name, ex.Message);
            }
            catch (UriFormatException)
            {
                // 如果URL格式无效，使用"unknown"作为域名
                this.metrics?.RecordError("unknown", ex.GetType().Name, ex.Message);
            }

            this.metrics?.RecordUrlFailed(request.Url);

            // 触发爬取错误事件
            this.OnCrawlError?.Invoke(this, new CrawlErrorEventArgs
            {
                Url = request.Url,
                Depth = request.Depth,
                ErrorMessage = ex.Message,
                Exception = ex,
            });
        }
    }

    /// <summary>
    /// 下载内容.
    /// </summary>
    /// <param name="request">要下载的爬取请求.</param>
    /// <returns>下载结果.</returns>
    /// <remarks>
    /// 下载指定URL的内容，包括检查robots.txt规则、防机器人检查和重试逻辑。.
    /// </remarks>
    private async Task<DownloadResult?> DownloadContentAsync(CrawlRequest request)
    {
        try
        {
            string? domain = null;
            try
            {
                domain = new Uri(request.Url).Host;
            }
            catch (UriFormatException)
            {
                domain = "invalid_url";
            }

            // 检查robots.txt规则
            if (this.robotsTxtParser != null && !await this.robotsTxtParser.IsAllowedAsync(request.Url))
            {
                var robotsEx = new RobotsTxtException(request.Url, $"URL {request.Url} is disallowed by robots.txt");
                this.logger.LogDebug("URL {Url} is disallowed by robots.txt", request.Url);

                // 使用错误处理服务处理robots.txt禁止访问的情况
                if (this.errorHandlingService != null)
                {
                    return this.errorHandlingService.HandleDownloadException(request.Url, robotsEx);
                }
                else
                {
                    // 回退处理
                    this.metrics?.RecordUrlFailed(domain, "robots_txt_disallowed");
                    return null;
                }
            }

            // 防机器人检查
            if (this.antiBotService != null && !this.antiBotService.ShouldProcess(request.Url))
            {
                this.logger.LogDebug("URL {Url} is skipped due to anti-bot rules", request.Url);
                this.metrics?.RecordUrlFailed(domain, "anti_bot_skipped");
                return null;
            }

            // 获取Downloader类型的插件，并按优先级排序（降序）
            var downloaderPlugins = this.GetPluginsByType(PluginType.Downloader)
                .Where(p => p.EntryPointType.IsAssignableTo(typeof(IDownloader)))
                .OrderByDescending(p => p.Priority)
                .ToList();

            DownloadResult? downloadResult = null;

            // 如果有下载器插件
            if (downloaderPlugins.Count != 0)
            {
                // 实现简单的链式调用：使用优先级最高的插件，如果失败则回退到下一个插件
                foreach (var plugin in downloaderPlugins)
                {
                    try
                    {
                        var pluginDownloader = await this.GetOrCreatePluginInstanceAsync<IDownloader>(plugin);
                        this.logger.LogInformation("Using plugin downloader '{Name}' (priority: {Priority}) for {Url}", plugin.PluginName, plugin.Priority, request.Url);
                        downloadResult = await pluginDownloader.DownloadAsync(request);

                        // 如果下载成功，跳出循环
                        if (downloadResult != null && !string.IsNullOrEmpty(downloadResult.Content))
                        {
                            break;
                        }

                        this.logger.LogWarning("Plugin downloader '{Name}' returned empty content for {Url}", plugin.PluginName, request.Url);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Plugin downloader '{Name}' failed for {Url}", plugin.PluginName, request.Url);

                        // 继续尝试下一个插件
                        continue;
                    }
                }

                // 如果所有插件都失败，使用默认下载器
                if (downloadResult == null || string.IsNullOrEmpty(downloadResult.Content))
                {
                    this.logger.LogInformation("All plugin downloaders failed, using default downloader for {Url}", request.Url);
                    downloadResult = await this.downloader.DownloadAsync(request);
                }
            }
            else
            {
                // 否则使用默认下载器
                downloadResult = await this.downloader.DownloadAsync(request);
            }

            // 记录下载结果指标
            if (downloadResult != null && this.metrics != null)
            {
                try
                {
                    var bytesDownloaded = downloadResult.Content?.Length ?? 0;
                    var statusCode = downloadResult.StatusCode;

                    // 记录下载字节数
                    this.metrics.RecordBytesDownloaded(bytesDownloaded, domain);

                    // 记录HTTP状态码
                    this.metrics.RecordHttpStatusCode(statusCode, domain);

                    // 记录下载持续时间
                    this.metrics.RecordDownloadDuration(domain, downloadResult.DownloadTimeMs);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to record download metrics for {Url}", request.Url);
                }
            }

            return downloadResult;
        }
        catch (Exception ex)
        {
            // 使用错误处理服务处理下载异常
            DownloadResult? downloadResult = null;
            if (this.errorHandlingService != null)
            {
                downloadResult = this.errorHandlingService.HandleDownloadException(request.Url, ex);
            }
            else
            {
                // 回退到传统日志记录
                this.logger.LogError(ex, "Download error for {Url}", request.Url);
            }

            // 记录下载失败指标
            try
            {
                string? host = null;
                try
                {
                    host = new Uri(request.Url).Host;
                }
                catch (UriFormatException)
                {
                    host = "invalid_url";
                }

                this.metrics?.RecordUrlFailed(host, "download_exception");
                this.metrics?.RecordError(host, ex.GetType().Name);

                // 重试逻辑
                if (!string.IsNullOrEmpty(host) && this.retryStrategy != null && await this.retryStrategy.ShouldRetryAsync(host, ex, request.RetryCount))
                {
                    request.RetryCount++;
                    await this.scheduler.AddUrlAsync(request);
                    this.logger.LogInformation("Retrying {Url} ({RetryCount})", request.Url, request.RetryCount);

                    // 记录重试尝试
                    this.metrics?.RecordRetryAttempt(host, ex.GetType().Name);
                }
            }
            catch (UriFormatException)
            { /* URL格式错误，不重试 */
            }

            return downloadResult;
        }
    }

    /// <summary>
    /// 解析内容.
    /// </summary>
    /// <param name="downloadResult">要解析的下载结果.</param>
    /// <param name="request">对应的爬取请求.</param>
    /// <returns>解析结果.</returns>
    /// <remarks>
    /// 解析下载的内容，提取出有用的信息。.
    /// </remarks>
    private async Task<ParseResult?> ParseContentAsync(DownloadResult downloadResult, CrawlRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // 获取Parser类型的插件，并按优先级排序（降序）
            var parserPlugins = this.GetPluginsByType(PluginType.Parser)
                .Where(p => p.EntryPointType.IsAssignableTo(typeof(IParser)))
                .OrderByDescending(p => p.Priority)
                .ToList();

            ParseResult? parseResult = null;

            // 如果有解析器插件
            if (parserPlugins.Count != 0)
            {
                // 实现简单的链式调用：使用优先级最高的插件，如果失败则回退到下一个插件
                foreach (var plugin in parserPlugins)
                {
                    try
                    {
                        var pluginParser = await this.GetOrCreatePluginInstanceAsync<IParser>(plugin);
                        this.logger.LogInformation("Using plugin parser '{Name}' (priority: {Priority}) for {Url}", plugin.PluginName, plugin.Priority, downloadResult.Url);
                        parseResult = await pluginParser.ParseAsync(downloadResult);

                        // 如果解析成功，跳出循环
                        if (parseResult != null)
                        {
                            break;
                        }

                        this.logger.LogWarning("Plugin parser '{Name}' returned null result for {Url}", plugin.PluginName, downloadResult.Url);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Plugin parser '{Name}' failed for {Url}", plugin.PluginName, downloadResult.Url);

                        // 继续尝试下一个插件
                        continue;
                    }
                }

                // 如果所有插件都失败，使用默认解析器
                if (parseResult == null)
                {
                    this.logger.LogInformation("All plugin parsers failed, using default parser for {Url}", downloadResult.Url);
                    parseResult = await this.parser.ParseAsync(downloadResult);
                }
            }
            else
            {
                // 否则使用默认解析器
                parseResult = await this.parser.ParseAsync(downloadResult);
            }

            // 记录解析持续时间
            stopwatch.Stop();
            if (this.metrics != null)
            {
                try
                {
                    string? domain = null;
                    try
                    {
                        domain = new Uri(request.Url).Host;
                    }
                    catch (UriFormatException)
                    {
                        domain = "invalid_url";
                    }

                    this.metrics.RecordParseDuration(domain, stopwatch.Elapsed.TotalMilliseconds, downloadResult.ContentType ?? "html");
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to record parse metrics for {Url}", request.Url);
                }
            }

            return parseResult;
        }
        catch (Exception ex)
        {
            // 记录解析失败指标
            stopwatch.Stop();

            // 使用错误处理服务处理解析异常
            ParseResult? parseResult = null;
            if (this.errorHandlingService != null)
            {
                parseResult = this.errorHandlingService.HandleParseException(request.Url, ex);
            }
            else
            {
                // 回退到传统日志记录
                this.logger.LogError(ex, "Parse error for {Url}", request.Url);
            }

            try
            {
                string? domain = null;
                try
                {
                    domain = new Uri(request.Url).Host;
                }
                catch (UriFormatException)
                {
                    domain = "invalid_url";
                }

                this.metrics?.RecordUrlFailed(domain, "parse_exception");
                this.metrics?.RecordError(domain, ex.GetType().Name);
                this.metrics?.RecordParseDuration(domain, stopwatch.Elapsed.TotalMilliseconds, "error");

                // 解析错误重试逻辑
                if (!string.IsNullOrEmpty(domain) && this.retryStrategy != null && await this.retryStrategy.ShouldRetryAsync(domain, ex, request.RetryCount))
                {
                    request.RetryCount++;
                    await this.scheduler.AddUrlAsync(request);
                    this.logger.LogInformation("Retrying parsing {Url} ({RetryCount})", request.Url, request.RetryCount);

                    // 记录重试尝试
                    this.metrics?.RecordRetryAttempt(domain, ex.GetType().Name);
                }
            }
            catch (Exception metricsEx)
            {
                this.logger.LogError(metricsEx, "Failed to record parse error metrics for {Url}", request.Url);
            }

            return parseResult;
        }
    }

    /// <summary>
    /// 存储结果.
    /// </summary>
    /// <param name="parseResult">要存储的解析结果.</param>
    /// <param name="request">对应的爬取请求.</param>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 将解析结果和爬取请求存储到数据库中。.
    /// </remarks>
    private async Task StoreResultAsync(ParseResult parseResult, CrawlRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = new CrawlResult
            {
                Request = request,
                ParseResult = parseResult,
                ProcessedAt = DateTime.UtcNow,
            };

            // 获取Storage类型的插件，并按优先级排序（降序）
            var storagePlugins = this.GetPluginsByType(PluginType.Storage)
                .Where(p => p.EntryPointType.IsAssignableTo(typeof(IStorageProvider)))
                .OrderByDescending(p => p.Priority)
                .ToList();

            bool storageSuccess = false;

            // 如果有存储插件
            if (storagePlugins.Count != 0)
            {
                // 实现简单的链式调用：使用优先级最高的插件，如果失败则回退到下一个插件
                foreach (var plugin in storagePlugins)
                {
                    try
                    {
                        var pluginStorage = await this.GetOrCreatePluginInstanceAsync<IStorageProvider>(plugin);
                        this.logger.LogInformation("Using plugin storage '{Name}' (priority: {Priority}) for {Url}", plugin.PluginName, plugin.Priority, request.Url);
                        await pluginStorage.SaveAsync(result);
                        storageSuccess = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Plugin storage '{Name}' failed for {Url}", plugin.PluginName, request.Url);

                        // 继续尝试下一个插件
                        continue;
                    }
                }

                // 如果所有插件都失败，使用默认存储提供器
                if (!storageSuccess)
                {
                    this.logger.LogInformation("All plugin storages failed, using default storage for {Url}", request.Url);
                    await this.storage.SaveAsync(result);
                }
            }
            else
            {
                // 否则使用默认存储提供器
                await this.storage.SaveAsync(result);
            }

            // 记录存储持续时间
            stopwatch.Stop();
            if (this.metrics != null)
            {
                try
                {
                    string? domain = null;
                    try
                    {
                        domain = new Uri(request.Url).Host;
                    }
                    catch (UriFormatException)
                    {
                        domain = "invalid_url";
                    }

                    this.metrics.RecordStorageDuration(domain, stopwatch.Elapsed.TotalMilliseconds, "database");
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to record storage metrics for {Url}", request.Url);
                }
            }
        }
        catch (Exception ex)
        {
            // 记录存储失败指标
            stopwatch.Stop();
            this.logger.LogError(ex, "Storage error for {Url}", request.Url);
            try
            {
                string? domain = null;
                try
                {
                    domain = new Uri(request.Url).Host;
                }
                catch (UriFormatException)
                {
                    domain = "invalid_url";
                }

                this.metrics?.RecordUrlFailed(domain, "storage_exception");
                this.metrics?.RecordError(domain, ex.GetType().Name);
                this.metrics?.RecordStorageDuration(domain, stopwatch.Elapsed.TotalMilliseconds, "error");
            }
            catch (Exception metricsEx)
            {
                this.logger.LogError(metricsEx, "Failed to record storage error metrics for {Url}", request.Url);
            }

            throw; // 存储错误应该被上层捕获并处理
        }
    }

    /// <summary>
    /// 初始化组件.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 初始化所有必要的组件，包括调度器、下载器、解析器、存储和其他服务。.
    /// </remarks>
    private async Task InitializeComponentsAsync()
    {
        try
        {
            // 初始化各个组件
            await this.scheduler.InitializeAsync();
            await this.downloader.InitializeAsync();
            await this.parser.InitializeAsync();
            await this.storage.InitializeAsync();

            // 初始化其他组件
            if (this.robotsTxtParser != null)
            {
                await this.robotsTxtParser.InitializeAsync();
            }

            if (this.antiBotService != null)
            {
                await this.antiBotService.InitializeAsync();
            }

            if (this.retryStrategy != null)
            {
                await ((this.retryStrategy as ICrawlerComponent)?.InitializeAsync() ?? Task.CompletedTask);
            }

            // 加载插件
            if (this.pluginLoader != null)
            {
                // 使用配置中的插件目录，如果没有配置则使用默认目录
                string pluginsDirectory = this.currentConfig?.PluginsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Plugins");
                var loadedPlugins = await this.pluginLoader.LoadPluginsAsync(pluginsDirectory);
                this.logger.LogInformation("Loaded {PluginCount} plugins from directory: {PluginsDirectory}", loadedPlugins.Count(), pluginsDirectory);

                // 将插件按类型分类存储
                foreach (var plugin in loadedPlugins)
                {
                    if (!this.pluginsByType.TryGetValue(plugin.PluginType, out List<IPlugin>? value))
                    {
                        value = [];
                        this.pluginsByType[plugin.PluginType] = value;
                    }

                    value.Add(plugin);
                    this.logger.LogInformation("Added plugin '{Name}' (v{Version}) of type {Type}", plugin.PluginName, plugin.Version, plugin.PluginType);

                    // 预创建插件实例并初始化，确保生命周期管理完整
                    try
                    {
                        var instance = (ICrawlerComponent)Activator.CreateInstance(plugin.EntryPointType) !;
                        await instance.InitializeAsync();
                        this.pluginInstanceCache[plugin.EntryPointType] = instance;
                        this.logger.LogInformation("Initialized plugin instance for '{Name}'", plugin.PluginName);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Failed to initialize plugin instance for '{Name}'", plugin.PluginName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error initializing components");
            throw;
        }
    }

    /// <summary>
    /// 关闭组件.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 关闭所有初始化的组件，释放资源。.
    /// </remarks>
    private async Task ShutdownComponentsAsync()
    {
        try
        {
            // 卸载插件
            if (this.pluginLoader != null)
            {
                await this.pluginLoader.UnloadAllPluginsAsync();
                this.logger.LogInformation("Unloaded all plugins");
            }

            // 关闭并清理缓存中的插件实例
            foreach (var instance in this.pluginInstanceCache.Values)
            {
                try
                {
                    await instance.ShutdownAsync();
                    this.logger.LogInformation("Shutdown cached plugin instance of type {Type}", instance.GetType().FullName);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to shutdown cached plugin instance of type {Type}", instance.GetType().FullName);
                }
            }

            this.pluginInstanceCache.Clear();

            // 关闭各个组件 - 按相反顺序关闭
            if (this.retryStrategy != null)
            {
                await ((this.retryStrategy as ICrawlerComponent)?.ShutdownAsync() ?? Task.CompletedTask);
            }

            if (this.antiBotService != null)
            {
                await this.antiBotService.ShutdownAsync();
            }

            if (this.robotsTxtParser != null)
            {
                await this.robotsTxtParser.ShutdownAsync();
            }

            await this.storage.ShutdownAsync();
            await this.parser.ShutdownAsync();
            await this.downloader.ShutdownAsync();
            await this.scheduler.ShutdownAsync();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error shutting down components");
        }
    }

    /// <summary>
    /// 保存初始爬取状态.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 保存当前爬取的初始状态，包括作业ID、开始时间和当前状态。.
    /// </remarks>
    private async Task SaveInitialCrawlStateAsync()
    {
        try
        {
            if (this.metadataStore != null)
            {
                var state = new CrawlState
                {
                    JobId = this.currentJobId ?? string.Empty,
                    StartTime = this.startTime,
                    Status = this.CurrentStatus,
                };
                await this.metadataStore.SaveCrawlStateAsync(state);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error saving initial crawl state");
        }
    }

    /// <summary>
    /// 监控并调整线程池大小.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>任务完成时的任务.</returns>
    private async Task MonitorAndAdjustThreadPoolAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && this.isRunning)
            {
                await Task.Delay(this.threadAdjustIntervalMs, cancellationToken);

                // 获取当前队列长度和工作线程数
                int queueLength = this.scheduler.QueuedCount;
                int currentWorkerCount;

                lock (this.workerTasksLock)
                {
                    currentWorkerCount = this.workerTasks.Count;
                }

                // 动态调整线程数
                if (queueLength > this.queueThresholdHigh && currentWorkerCount < this.maxWorkerCount)
                {
                    // 队列堆积，智能增加线程数
                    int newWorkersToAdd = Math.Min(this.maxWorkerCount - currentWorkerCount, Math.Max(1, queueLength / 10));
                    this.AddWorkerThreads(newWorkersToAdd);
                    this.logger.LogInformation(
                        "Increased worker count to {NewCount} due to high queue length ({QueueLength})",
                        currentWorkerCount + newWorkersToAdd,
                        queueLength);
                }
                else if (queueLength < this.queueThresholdLow && currentWorkerCount > this.minWorkerCount)
                {
                    // 队列较空，减少线程数
                    int workersToRemove = Math.Min(2, currentWorkerCount - this.minWorkerCount);
                    this.RemoveWorkerThreads(workersToRemove);
                    this.logger.LogInformation(
                        "Decreased worker count to {NewCount} due to low queue length ({QueueLength})",
                        currentWorkerCount - workersToRemove,
                        queueLength);
                }

                // 自动停止逻辑
                if (this.enableAutoStop)
                {
                    if (queueLength == 0)
                    {
                        // 任务队列为空，记录起始时间
                        if (this.queueEmptyStartTime == null)
                        {
                            this.queueEmptyStartTime = DateTime.UtcNow;
                            this.logger.LogInformation("Task queue became empty. Auto-stop timer started.");
                        }
                        else
                        {
                            // 检查是否超过超时时间
                            if (DateTime.UtcNow - this.queueEmptyStartTime >= this.autoStopTimeout)
                            {
                                this.logger.LogInformation(
                                    "Auto-stop timeout reached. Queue has been empty for {Duration}. Stopping crawler.",
                                    DateTime.UtcNow - this.queueEmptyStartTime);
                                await this.StopAsync();
                                break;
                            }
                            else
                            {
                                this.logger.LogDebug(
                                    "Queue remains empty. Auto-stop timer: {Elapsed} / {Timeout}",
                                    DateTime.UtcNow - this.queueEmptyStartTime,
                                    this.autoStopTimeout);
                            }
                        }
                    }
                    else
                    {
                        // 任务队列不为空，重置计时器
                        if (this.queueEmptyStartTime != null)
                        {
                            this.queueEmptyStartTime = null;
                            this.logger.LogInformation("Task queue is no longer empty. Auto-stop timer reset.");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            this.logger.LogInformation("Thread pool monitoring task was canceled");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error in thread pool monitoring task");
        }
    }

    /// <summary>
    /// 添加工作线程.
    /// </summary>
    /// <param name="count">要添加的线程数.</param>
    private void AddWorkerThreads(int count)
    {
        if (this.cancellationTokenSource?.IsCancellationRequested == true)
        {
            return;
        }

        lock (this.workerTasksLock)
        {
            int currentCount = this.workerTasks.Count;
            for (int i = 0; i < count; i++)
            {
                int workerId = currentCount + i;
                this.workerTasks.Add(this.WorkerLoopAsync(workerId, this.cancellationTokenSource!.Token));
            }
        }
    }

    /// <summary>
    /// 移除工作线程.
    /// </summary>
    /// <param name="count">要移除的线程数.</param>
    private void RemoveWorkerThreads(int count)
    {
        // 计算可以安全移除的线程数
        lock (this.workerTasksLock)
        {
            if (this.workerTasks.Count > this.minWorkerCount)
            {
                int actualWorkersToRemove = Math.Min(count, this.workerTasks.Count - this.minWorkerCount);

                if (actualWorkersToRemove > 0)
                {
                    // 增加需要移除的线程计数
                    Interlocked.Add(ref this.workersToRemove, actualWorkersToRemove);

                    this.logger.LogInformation("Scheduled {Count} workers for removal", actualWorkersToRemove);
                    this.logger.LogDebug("Total workers to remove now: {Total}", this.workersToRemove);
                }
            }
        }
    }

    /// <summary>
    /// 保存最终爬取状态.
    /// </summary>
    /// <returns>任务完成时的任务.</returns>
    /// <remarks>
    /// 保存当前爬取的最终状态，包括作业ID、开始时间、结束时间和当前状态。.
    /// </remarks>
    private async Task SaveFinalCrawlStateAsync()
    {
        try
        {
            if (this.metadataStore != null)
            {
                var state = new CrawlState
                {
                    JobId = this.currentJobId ?? string.Empty,
                    StartTime = this.startTime,
                    EndTime = DateTime.UtcNow,
                    Status = this.CurrentStatus,
                };
                await this.metadataStore.SaveCrawlStateAsync(state);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error saving final crawl state");
        }
    }
}