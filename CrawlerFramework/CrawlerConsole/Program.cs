// CrawlerConsole/Program.cs
class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        var serviceProvider = services.BuildServiceProvider();
        var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

        // 配置爬虫
        var config = new CrawlConfiguration
        {
            MaxConcurrentTasks = 5,
            MaxDepth = 2,
            MaxPages = 100,
            RequestDelay = TimeSpan.FromMilliseconds(1000),
            AllowedDomains = new[] { "example.com" }
        };

        // 注册事件
        crawler.OnCrawlCompleted += (s, e) => 
        {
            Console.WriteLine($"Completed: {e.Result.Request.Url}");
        };

        // 启动爬虫
        await crawler.StartAsync(config);
        
        // 添加种子URL
        await crawler.AddSeedUrlsAsync(new[] 
        {
            "https://example.com",
            "https://example.com/news"
        });

        Console.WriteLine("Crawler started. Press any key to stop...");
        Console.ReadKey();

        await crawler.StopAsync();
    }

    static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole());
        
        services.AddSingleton<IScheduler, PriorityScheduler>();
        services.AddSingleton<IDownloader, AdvancedDownloader>();
        services.AddSingleton<IParser, AdvancedParser>();
        services.AddSingleton<IStorage, FileStorage>();
        services.AddSingleton<CrawlerEngine>();
        
        services.AddHttpClient();
    }
}