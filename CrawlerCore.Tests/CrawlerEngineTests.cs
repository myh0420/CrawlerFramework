using CrawlerFramework.CrawlerCore;
using  CrawlerFramework.CrawlerEntity.Configuration;
using  CrawlerFramework.CrawlerEntity.Enums;
using  CrawlerFramework.CrawlerEntity.Events;
using  CrawlerFramework.CrawlerEntity.Models;
using CrawlerFramework.CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CrawlerFramework.CrawlerCore.Tests;

public class CrawlerEngineTests
{
    private readonly Mock<IScheduler> _mockScheduler;
    private readonly Mock<IDownloader> _mockDownloader;
    private readonly Mock<IParser> _mockParser;
    private readonly Mock<IStorageProvider> _mockStorage;
    private readonly Mock<ILogger<CrawlerEngine>> _mockLogger;
    private readonly CrawlerEngine _crawlerEngine;

    public CrawlerEngineTests()
    {
        // 创建模拟对象
        _mockScheduler = new Mock<IScheduler>();
        _mockDownloader = new Mock<IDownloader>();
        _mockParser = new Mock<IParser>();
        _mockStorage = new Mock<IStorageProvider>();
        _mockLogger = new Mock<ILogger<CrawlerEngine>>();

        // 初始化CrawlerEngine
        _crawlerEngine = new CrawlerEngine(
            _mockScheduler.Object,
            _mockDownloader.Object,
            _mockParser.Object,
            _mockStorage.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task StartAsync_ShouldInitializeAndStartCrawler()
    {
        // 模拟调度器返回null，使工作线程快速退出
        _mockScheduler.Setup(s => s.GetNextAsync()).ReturnsAsync((CrawlRequest)null);

        // 执行测试
        await _crawlerEngine.StartAsync(1);

        // 验证状态
        Assert.True(_crawlerEngine.IsRunning);
        Assert.Equal(CrawlerStatus.Running, _crawlerEngine.CurrentStatus);
        Assert.NotNull(_crawlerEngine.CurrentJobId);

        // 停止爬虫以清理资源
        await _crawlerEngine.StopAsync(false);
    }

    [Fact]
    public async Task StartAsync_WithAdvancedConfiguration_ShouldUseConfiguredWorkerCount()
    {
        // 准备
        var config = new AdvancedCrawlConfiguration
        {
            MaxConcurrentTasks = 3
        };

        // 模拟调度器返回null，使工作线程快速退出
        _mockScheduler.Setup(s => s.GetNextAsync()).ReturnsAsync((CrawlRequest)null);

        // 执行测试
        await _crawlerEngine.StartAsync(config);

        // 验证状态
        Assert.True(_crawlerEngine.IsRunning);
        Assert.Equal(CrawlerStatus.Running, _crawlerEngine.CurrentStatus);

        // 停止爬虫以清理资源
        await _crawlerEngine.StopAsync(false);
    }

    [Fact]
    public async Task StopAsync_ShouldStopCrawlerAndCleanup()
    {
        // 模拟调度器返回null，使工作线程快速退出
        _mockScheduler.Setup(s => s.GetNextAsync()).ReturnsAsync((CrawlRequest)null);

        // 先启动爬虫
        await _crawlerEngine.StartAsync(1);
        Assert.True(_crawlerEngine.IsRunning);

        // 执行测试
        await _crawlerEngine.StopAsync(false);

        // 验证状态
        Assert.False(_crawlerEngine.IsRunning);
        Assert.Equal(CrawlerStatus.Idle, _crawlerEngine.CurrentStatus);
    }

    [Fact]
    public async Task PauseAsync_ShouldChangeStatusToPaused()
    {
        // 模拟调度器返回null，使工作线程快速退出
        _mockScheduler.Setup(s => s.GetNextAsync()).ReturnsAsync((CrawlRequest)null);

        // 先启动爬虫
        await _crawlerEngine.StartAsync(1);
        Assert.Equal(CrawlerStatus.Running, _crawlerEngine.CurrentStatus);

        // 执行测试
        await _crawlerEngine.PauseAsync();

        // 验证状态
        Assert.Equal(CrawlerStatus.Paused, _crawlerEngine.CurrentStatus);

        // 停止爬虫以清理资源
        await _crawlerEngine.StopAsync(false);
    }

    [Fact]
    public async Task ResumeAsync_ShouldChangeStatusToRunning()
    {
        // 模拟调度器返回null，使工作线程快速退出
        _mockScheduler.Setup(s => s.GetNextAsync()).ReturnsAsync((CrawlRequest)null);

        // 先启动爬虫
        await _crawlerEngine.StartAsync(1);
        await _crawlerEngine.PauseAsync();
        Assert.Equal(CrawlerStatus.Paused, _crawlerEngine.CurrentStatus);

        // 执行测试
        await _crawlerEngine.ResumeAsync();

        // 验证状态
        Assert.Equal(CrawlerStatus.Running, _crawlerEngine.CurrentStatus);

        // 停止爬虫以清理资源
        await _crawlerEngine.StopAsync(false);
    }

    [Fact]
    public async Task AddSeedUrlsAsync_ShouldAddUrlsToScheduler()
    {
        // 准备
        var seedUrls = new List<string> { "https://example.com", "https://example.org" };
        _mockScheduler.Setup(s => s.AddUrlsAsync(It.IsAny<IEnumerable<CrawlRequest>>()))
            .ReturnsAsync(2);

        // 执行测试
        await _crawlerEngine.AddSeedUrlsAsync(seedUrls);

        // 验证
        _mockScheduler.Verify(s => s.AddUrlsAsync(It.IsAny<IEnumerable<CrawlRequest>>()), Times.Once);
    }

    [Fact]
    public async Task AddSeedUrlsAsync_WithEmptyList_ShouldNotCallScheduler()
    {
        // 准备
        var seedUrls = new List<string>();

        // 执行测试
        await _crawlerEngine.AddSeedUrlsAsync(seedUrls);

        // 验证
        _mockScheduler.Verify(s => s.AddUrlsAsync(It.IsAny<IEnumerable<CrawlRequest>>()), Times.Never);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnCorrectStatistics()
    {
        // 准备
        _mockScheduler.Setup(s => s.QueuedCount).Returns(10);
        _mockScheduler.Setup(s => s.ProcessedCount).Returns(50);

        // 执行测试
        var stats = await _crawlerEngine.GetStatisticsAsync();

        // 验证
        Assert.Contains("Status", stats);
        Assert.Contains("Uptime", stats);
        Assert.Contains("JobId", stats);
        Assert.Contains("WorkerCount", stats);
        Assert.Contains("QueueLength", stats);
        Assert.Contains("ProcessedCount", stats);
        Assert.Equal(10, stats["QueueLength"]);
        Assert.Equal(50, stats["ProcessedCount"]);
    }

    [Fact]
    public async Task GetCurrentCrawlStateAsync_ShouldReturnCorrectState()
    {
        // 准备
        _mockScheduler.Setup(s => s.QueuedCount).Returns(0);
        _mockScheduler.Setup(s => s.ProcessedCount).Returns(25);
        _mockScheduler.Setup(s => s.ErrorCount).Returns(0);

        // 执行测试
        var state = await _crawlerEngine.GetCurrentCrawlStateAsync();

        // 验证
        Assert.Equal(CrawlerStatus.Idle, state.Status);
        Assert.Equal(_crawlerEngine.CurrentJobId, state.JobId);
        Assert.Equal(25, state.TotalUrlsProcessed);
        Assert.Equal(25, state.TotalUrlsDiscovered); // 0 + 25 = 25
        Assert.Equal(0, state.TotalErrors);
    }

    [Fact]
    public void StatusChangedEvent_ShouldBeRaisedWhenStatusChanges()
    {
        // 准备
        bool eventRaised = false;
        _crawlerEngine.OnStatusChanged += (sender, args) => eventRaised = true;

        // 模拟调度器返回null，使工作线程快速退出
        _mockScheduler.Setup(s => s.GetNextAsync()).ReturnsAsync((CrawlRequest)null);

        // 执行测试（异步操作，需要等待）
        var task = Task.Run(async () => await _crawlerEngine.StartAsync(1));
        Task.WaitAll(task, Task.Delay(100)); // 等待事件触发

        // 验证
        Assert.True(eventRaised);

        // 清理
        _crawlerEngine.OnStatusChanged -= (sender, args) => eventRaised = true;
        task = Task.Run(async () => await _crawlerEngine.StopAsync(false));
        Task.WaitAll(task, Task.Delay(100));
    }
}
