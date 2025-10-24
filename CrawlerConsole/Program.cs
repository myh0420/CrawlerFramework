// CrawlerConsole/Program.cs

using CrawlerCore;
using CrawlerEntity.Configuration;
using CrawlerEntity.Models;
using CrawlerServiceDependencyInjection.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrawlerConsole
{
    class Program
    {
        static async Task Main()
        {
            // 创建服务集合
            var services = new ServiceCollection();
            ConfigureServices(services);

            // 构建服务提供者
            var serviceProvider = services.BuildServiceProvider();
            var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

            // 配置爬虫
            var config = new AdvancedCrawlConfiguration
            {
                MaxConcurrentTasks = 5,
                MaxDepth = 2,
                MaxPages = 100,
                RequestDelay = TimeSpan.FromMilliseconds(1000),
                AllowedDomains = [ "example.com" ],

                // 新增配置
                EnableAntiBotDetection = true,
                RespectRobotsTxt = true,
                MemoryLimitMB = 500,
                RetryPolicy = new RetryPolicy
                {
                    MaxRetries = 3,
                    InitialDelay = TimeSpan.FromSeconds(1),
                    BackoffMultiplier = 2.0
                },
                ProxySettings = new ProxySettings
                {
                    Enabled = false
                },
                MonitoringSettings = new MonitoringSettings
                {
                    EnableMetrics = true,
                    MetricsIntervalSeconds = 30
                }
            };

            // 注册事件
            //crawler.OnCrawlCompleted += (s, e) =>
            //{
            //    Console.WriteLine($"Completed: {e.Result.Request.Url}");
            //};

            //crawler.OnCrawlError += (s, e) =>
            //{
            //    Console.WriteLine($"Error: {e.Request.Url} - {e.Exception.Message}");
            //};
            //var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

            // 注册所有事件
            crawler.OnCrawlCompleted += (s, e) =>
            {
                Console.WriteLine($"[COMPLETED] {e.Result.Request.Url} ({e.Result.DownloadResult.StatusCode})");
            };

            crawler.OnCrawlError += (s, e) =>
            {
                Console.WriteLine($"[ERROR] {e.Request.Url}: {e.Exception.Message}");
            };

            crawler.OnUrlDiscovered += (s, e) =>
            {
                Console.WriteLine($"[DISCOVERED] {e.DiscoveredUrl} (Depth: {e.Depth}) from {e.SourceUrl ?? "seed"}");
            };

            crawler.OnStatusChanged += (s, e) =>
            {
                Console.WriteLine($"[STATUS] {e.PreviousStatus} → {e.CurrentStatus}: {e.Message}");
            };

            // 启动爬虫
            await crawler.StartAsync(config);

            // 添加种子URL
            await crawler.AddSeedUrlsAsync([
                "https://example.com",
                "https://example.com/news"
            ]);

            Console.WriteLine("Crawler started. Press any key to stop...");
            Console.ReadKey();

            await crawler.StopAsync();
        }

        // 高级配置示例 - 在 CrawlerConsole/Program.cs 中
        static void ConfigureServices(IServiceCollection services)
        {
            // 添加日志
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
            });
            // 添加高级爬虫服务
            services.AddAdvancedCrawler(config =>
            {
                // 可以在这里配置默认值
                config.EnableAntiBotDetection = true;
                config.RespectRobotsTxt = true;
            });
            // 添加HttpClient并配置
            services.AddHttpClient("CrawlerClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            });

            // 添加爬虫核心服务
            services.AddCrawlerCore();

            // 配置存储
            var storageType = "FileSystem"; // 或 "SQLite"
            if (storageType == "SQLite")
            {
                services.AddSQLiteStorage("crawler_data.db");
            }
            else
            {
                services.AddFileSystemStorage("crawler_data");
            }

            // 配置下载器
            services.AddCrawlerDownloader(useProxies: false);

            // 配置解析器
            services.AddCrawlerParser();

            // 配置调度器
            services.AddCrawlerScheduler();

            // 注册配置
            services.Configure<CrawlConfiguration>(config =>
            {
                config.MaxConcurrentTasks = 5;
                config.MaxDepth = 3;
                config.MaxPages = 1000;
                config.RequestDelay = TimeSpan.FromMilliseconds(500);
                config.TimeoutSeconds = 30;
            });
        }
    }
}