// CrawlerScheduler/PriorityScheduler.cs
using CrawlerInterFaces.Interfaces;
using CrawlerEntity.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace CrawlerScheduler;
public class PriorityScheduler(IUrlFilter urlFilter, IDomainDelayManager delayManager,
    ILogger<PriorityScheduler> logger) : IScheduler
{
    private readonly PriorityQueue<CrawlRequest, int> _priorityQueue = new();
    private readonly ConcurrentDictionary<string, bool> _processedUrls = new();
    private readonly IUrlFilter _urlFilter = urlFilter;
    private readonly IDomainDelayManager _delayManager = delayManager;
    private readonly ILogger<PriorityScheduler> _logger = logger;

    public int QueuedCount => _priorityQueue.Count;
    public int ProcessedCount => _processedUrls.Count;

    public async Task<bool> AddUrlAsync(CrawlRequest request)
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
        if (!await _delayManager.CanProcessAsync(domain))
            return false;

        // 添加到队列
        var priority = CalculatePriority(request);
        _priorityQueue.Enqueue(request, priority);
        _processedUrls[normalizedUrl] = true;

        _logger.LogDebug("URL added to queue: {Url} with priority {Priority}", 
            normalizedUrl, priority);

        return true;
    }

    public async Task<CrawlRequest?> GetNextAsync()
    {
        while (_priorityQueue.Count > 0)
        {
            if (_priorityQueue.TryDequeue(out var request, out _))
            {
                var domain = new Uri(request.Url).Host;
                await _delayManager.RecordAccessAsync(domain);
                return request;
            }
        }
        return null;
    }

    private static string NormalizeUrl(string url)
    {
        // URL标准化逻辑
        return url.ToLowerInvariant().Trim();
    }

    private static int CalculatePriority(CrawlRequest request)
    {
        var priority = request.Priority;
        
        // 根据深度调整优先级
        priority -= request.Depth * 10;
        
        // 根据URL模式调整优先级
        if (request.Url.Contains("/article/") || request.Url.Contains("/news/"))
            priority += 5;

        return Math.Max(1, priority);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task ShutdownAsync() => Task.CompletedTask;
}