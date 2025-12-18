// CrawlerScheduler/PriorityScheduler.cs
using CrawlerFramework.CrawlerInterFaces.Interfaces;
using CrawlerFramework.CrawlerEntity.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

namespace CrawlerFramework.CrawlerScheduler;
/// <summary>
/// 优先级调度器，负责管理爬取请求的队列和优先级，并支持域名请求节流
/// </summary>
/// <remarks>
/// <para>PriorityScheduler是爬虫框架的任务调度核心组件，负责：</para>
/// <list type="bullet">
/// <item>根据URL优先级、深度、域名性能等因素动态计算请求优先级</item>
/// <item>管理爬取请求队列，确保高优先级请求优先处理</item>
/// <item>支持域名请求节流，避免对目标网站造成过大压力</item>
/// <item>记录和分析域名性能数据，用于动态调整优先级</item>
/// <item>处理URL标准化和去重</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var scheduler = new PriorityScheduler(urlFilter, delayManager, logger);
/// var added = await scheduler.AddUrlAsync(new CrawlRequest { Url = "https://example.com", Priority = 10, Depth = 0 });
/// </code>
/// </example>
public class PriorityScheduler : IScheduler
{
    /// <summary>
    /// 优先级队列
    /// </summary>
    private readonly PriorityQueue<CrawlRequest, int> _priorityQueue = new();
    /// <summary>
    /// 队列锁
    /// </summary>
    private readonly ReaderWriterLockSlim _queueLock = new();
    /// <summary>
    /// 已处理URL字典
    /// </summary>
    private readonly ConcurrentDictionary<string, bool> _processedUrls = new();
    /// <summary>
    /// URL过滤规则
    /// </summary>
    private readonly IUrlFilter _urlFilter;
    /// <summary>
    /// 域名延迟管理器
    /// </summary>
    private readonly IDomainDelayManager _delayManager;
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<PriorityScheduler> _logger;

    /// <summary>
    /// 初始化 <see cref="PriorityScheduler"/> 类的新实例.
    /// </summary>
    /// <param name="urlFilter">URL过滤器，用于判断是否允许处理特定URL</param>
    /// <param name="delayManager">域名延迟管理器，用于控制对同一域名的请求频率</param>
    /// <param name="logger">日志记录器，用于记录运行时信息和错误</param>
    /// <exception cref="ArgumentNullException">当urlFilter或delayManager参数为null时抛出.</exception>
    public PriorityScheduler(IUrlFilter urlFilter, IDomainDelayManager delayManager,
        ILogger<PriorityScheduler> logger)
    {
        _urlFilter = urlFilter ?? throw new ArgumentNullException(nameof(urlFilter), "URL过滤器不能为空");
        _delayManager = delayManager ?? throw new ArgumentNullException(nameof(delayManager), "域名延迟管理器不能为空");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger), "日志记录器不能为空");
    }

    /// <summary>
    /// 任务ID生成器
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _taskCounter = new();
    /// <summary>
    /// 队列中待处理URL数量
    /// </summary>
    public int QueuedCount
    {
        get
        {
            _queueLock.EnterReadLock();
            try
            {
                return _priorityQueue.Count;
            }
            finally
            {
                _queueLock.ExitReadLock();
            }
        }
    }
    /// <summary>
    /// 已处理URL数量
    /// </summary>
    public int ProcessedCount => _processedUrls.Count;
    /// <summary>
    /// 任务ID前缀
    /// </summary>
    private static string TaskIdPrefix => $"task_{Environment.MachineName}_";

    /// <summary>
    /// 域名性能数据
    /// </summary>
    private class DomainPerformanceData
    {
        public int SuccessCount { get; set; } = 0;
        public int ErrorCount { get; set; } = 0;
        public long TotalDownloadTimeMs { get; set; } = 0;
        public long LastDownloadTimeMs { get; set; } = 0;
        public DateTime LastSuccessTime { get; set; } = DateTime.MinValue;
        public DateTime LastErrorTime { get; set; } = DateTime.MinValue;

        public double AverageDownloadTimeMs => SuccessCount > 0 ? (double)TotalDownloadTimeMs / SuccessCount : 0;
        public double ErrorRate => (SuccessCount + ErrorCount) > 0 ? (double)ErrorCount / (SuccessCount + ErrorCount) : 0;
    }

    /// <summary>
    /// 域名性能数据字典
    /// </summary>
    private readonly ConcurrentDictionary<string, DomainPerformanceData> _domainPerformanceData = [];

    /// <summary>
    /// 记录域名的下载性能数据
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="downloadTimeMs">下载时间（毫秒）</param>
    /// <param name="isSuccess">是否下载成功</param>
    public void RecordDomainPerformance(string domain, long downloadTimeMs, bool isSuccess)
    {
        var performanceData = _domainPerformanceData.GetOrAdd(domain, _ => new DomainPerformanceData());

        lock (performanceData)
        {
            if (isSuccess)
            {
                performanceData.SuccessCount++;
                performanceData.TotalDownloadTimeMs += downloadTimeMs;
                performanceData.LastDownloadTimeMs = downloadTimeMs;
                performanceData.LastSuccessTime = DateTime.UtcNow;
            }
            else
            {
                performanceData.ErrorCount++;
                performanceData.LastErrorTime = DateTime.UtcNow;
            }
        }
    }
    /// <summary>
    /// 初始化组件
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("PriorityScheduler initialized with distributed support");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 添加URL到队列
    /// </summary>
    /// <param name="request">要添加的爬取请求</param>
    /// <returns>如果URL被添加到队列则返回true，否则返回false</returns>
    public async Task<bool> AddUrlAsync(CrawlRequest request)
    {
        try
        {
            // URL标准化
            var normalizedUrl = NormalizeUrl(request.Url);
            request.Url = normalizedUrl;

            // 检查URL过滤规则
            if (!_urlFilter.IsAllowed(normalizedUrl))
                return false;

            // 检查是否已处理
            if (_processedUrls.ContainsKey(normalizedUrl))
                return false;

            // 检查域名延迟
            var domain = new Uri(normalizedUrl).Host;
            string requestType = GetRequestType(request);
            if (!await _delayManager.CanProcessAsync(domain, requestType))
                return false;

            // 生成唯一任务ID
            request.TaskId = GenerateTaskId(domain);
            request.QueuedAt = DateTime.UtcNow;

            // 添加到队列
            var priority = CalculatePriority(request);
            _queueLock.EnterWriteLock();
            try
            {
                _priorityQueue.Enqueue(request, priority);
            }
            finally
            {
                _queueLock.ExitWriteLock();
            }
            _processedUrls[normalizedUrl] = true;

            _logger.LogDebug("URL added to queue: {Url} with priority {Priority}, task ID: {TaskId}",
                normalizedUrl, priority, request.TaskId);

            return true;
        }
        catch (UriFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid URL format: {Url}", request.Url);
            return false;
        }
    }

    /// <summary>
    /// 批量添加URL到队列
    /// </summary>
    /// <param name="requests">要添加的爬取请求集合</param>
    /// <returns>成功添加到队列的URL数量</returns>
    public async Task<int> AddUrlsAsync(IEnumerable<CrawlRequest> requests)
    {
        int addedCount = 0;
        List<CrawlRequest> requestsToAdd = [];
        ConcurrentDictionary<string, bool> processedDomains = new();

        // 预处理请求
        foreach (var request in requests)
        {
            try
            {
                // URL标准化
                var normalizedUrl = NormalizeUrl(request.Url);
                request.Url = normalizedUrl;

                // 检查URL过滤规则
                if (!_urlFilter.IsAllowed(normalizedUrl))
                    continue;

                // 检查是否已处理
                if (_processedUrls.ContainsKey(normalizedUrl))
                    continue;

                var domain = new Uri(normalizedUrl).Host;
                string requestType = GetRequestType(request);

                // 批量检查域名延迟
                if (!processedDomains.TryGetValue(domain, out bool canProcessDomain))
                {
                    canProcessDomain = await _delayManager.CanProcessAsync(domain, requestType);
                    processedDomains[domain] = canProcessDomain;
                }

                if (canProcessDomain)
                {
                    // 生成唯一任务ID
                    request.TaskId = GenerateTaskId(domain);
                    request.QueuedAt = DateTime.UtcNow;
                    requestsToAdd.Add(request);
                }
            }
            catch (UriFormatException ex)
            {
                _logger.LogWarning(ex, "Invalid URL format: {Url}", request.Url);
            }
        }

        // 批量添加到队列
        if (requestsToAdd.Count > 0)
        {
            _queueLock.EnterWriteLock();
            try
            {
                foreach (var request in requestsToAdd)
                {
                    var priority = CalculatePriority(request);
                    _priorityQueue.Enqueue(request, priority);
                    _processedUrls[request.Url] = true;
                    addedCount++;

                    _logger.LogDebug("URL added to queue: {Url} with priority {Priority}, task ID: {TaskId}",
                        request.Url, priority, request.TaskId);
                }
            }
            finally
            {
                _queueLock.ExitWriteLock();
            }
        }

        return addedCount;
    }

    /// <summary>
    /// 从队列中获取下一个待处理URL
    /// </summary>
    /// <returns>下一个待处理的爬取请求，如果队列为空则返回null</returns>
    public async Task<CrawlRequest?> GetNextAsync()
    {
        while (true)
        {
            CrawlRequest? request = null;

            _queueLock.EnterWriteLock();
            try
            {
                if (_priorityQueue.Count == 0)
                {
                    return null;
                }

                if (_priorityQueue.TryDequeue(out var req, out _))
                {
                    request = req;
                }
            }
            finally
            {
                _queueLock.ExitWriteLock();
            }

            if (request != null)
            {
                try
                {
                    var domain = new Uri(request.Url).Host;
                    string requestType = GetRequestType(request);
                    await _delayManager.RecordAccessAsync(domain, requestType);
                    request.StartedAt = DateTime.UtcNow;
                    return request;
                }
                catch (UriFormatException ex)
                {
                    _logger.LogError(ex, "Invalid URL format when processing: {Url}", request.Url);
                    // 跳过无效URL，继续处理下一个
                }
            }
        }
    }

    /// <summary>
    /// 标准化URL
    /// </summary>
    /// <param name="url">要标准化的URL</param>
    /// <returns>标准化后的URL</returns>
    private static string NormalizeUrl(string url)
    {
        // 基本标准化
        var normalized = url.ToLowerInvariant().Trim();

        // 移除URL参数排序和重复参数
        if (normalized.Contains('?'))
        {
            var parts = normalized.Split('?');
            if (parts.Length == 2)
            {
                var baseUrl = parts[0];
                var queryString = parts[1];

                // 解析查询参数并排序
                var paramDict = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var paramsList = queryString.Split('&');

                foreach (var param in paramsList)
                {
                    if (!string.IsNullOrEmpty(param))
                    {
                        var paramParts = param.Split('=');
                        if (paramParts.Length >= 1)
                        {
                            var key = paramParts[0].Trim();
                            var value = paramParts.Length > 1 ? paramParts[1].Trim() : string.Empty;
                            paramDict[key] = value;
                        }
                    }
                }

                // 重建查询字符串
                var sortedParams = string.Join('&', paramDict.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                normalized = $"{baseUrl}?{sortedParams}";
            }
        }

        return normalized;
    }

    /// <summary>
    /// 计算URL的优先级
    /// </summary>
    /// <param name="request">要计算优先级的爬取请求</param>
    /// <returns>计算后的优先级值</returns>
    private int CalculatePriority(CrawlRequest request)
    {
        var priority = request.Priority;

        // 根据深度调整优先级
        priority -= request.Depth * 10;

        // 根据URL模式调整优先级
        if (request.Url.Contains("/article/") || request.Url.Contains("/news/") || request.Url.Contains("/blog/"))
            priority += 10;
        else if (request.Url.Contains("/category/") || request.Url.Contains("/tag/"))
            priority += 5;
        else if (request.Url.EndsWith(".pdf") || request.Url.EndsWith(".doc") || request.Url.EndsWith(".docx"))
            priority += 8;

        // 根据域名重要性调整优先级
        var domain = new Uri(request.Url).Host;
        if (IsHighPriorityDomain(domain))
            priority += 15;

        // 根据域名性能数据动态调整优先级
        if (_domainPerformanceData.TryGetValue(domain, out var performanceData))
        {
            lock (performanceData)
            {
                // 下载速度越快，优先级越高（每快100ms增加1优先级）
                if (performanceData.AverageDownloadTimeMs > 0)
                {
                    var speedBonus = (int)((1000 - Math.Min(1000, performanceData.AverageDownloadTimeMs)) / 100);
                    priority += speedBonus;
                }

                // 错误率越高，优先级越低（错误率每增加10%降低2优先级）
                var errorPenalty = (int)(performanceData.ErrorRate * 20);
                priority -= errorPenalty;

                // 连续错误的域名进一步降低优先级
                if (performanceData.LastSuccessTime < performanceData.LastErrorTime &&
                    performanceData.ErrorCount > 3)
                {
                    priority -= 5;
                }
            }
        }

        // 根据队列等待时间调整优先级（避免饥饿）
        if (request.QueuedAt.HasValue)
        {
            var waitTime = DateTime.UtcNow - request.QueuedAt.Value;
            priority += (int)(waitTime.TotalSeconds / 10); // 每等待10秒增加1优先级
        }

        return Math.Max(1, priority);
    }

    /// <summary>
    /// 判断是否为高优先级域名
    /// </summary>
    /// <param name="domain">域名</param>
    /// <returns>如果是高优先级域名则返回true，否则返回false</returns>
    private static bool IsHighPriorityDomain(string domain)
    {
        // 示例高优先级域名列表
        var highPriorityDomains = new[] { "example.com", "news.example.com", "blog.example.com" };
        return highPriorityDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取请求类型
    /// </summary>
    /// <param name="request">爬取请求</param>
    /// <returns>请求类型</returns>
    private static string GetRequestType(CrawlRequest request)
    {
        if (request.Url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return "pdf";
        if (request.Url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            request.Url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            request.Url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            return "image";
        if (request.Url.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            return "api";
        return "html";
    }

    /// <summary>
    /// 生成任务ID
    /// </summary>
    /// <param name="domain">域名</param>
    /// <returns>唯一任务ID</returns>
    private string GenerateTaskId(string domain)
    {
        var counter = _taskCounter.AddOrUpdate(domain, 1, (_, current) => current + 1);
        return $"{TaskIdPrefix}{domain.Replace('.', '_')}_{DateTime.UtcNow.Ticks}_{counter}";
    }

    /// <summary>
    /// 关闭组件
    /// </summary>  
    public async Task ShutdownAsync()
    {
        _logger.LogInformation("PriorityScheduler shutting down. Processed {Count} URLs.", ProcessedCount);
        await Task.CompletedTask;
    }
}