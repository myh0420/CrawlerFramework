// CrawlerCore/DependencyInjection/CrawlerServiceCollectionExtensions.cs
using CrawlerCore;
using CrawlerCore.AntiBot;
using CrawlerCore.Configuration;
using CrawlerCore.Export;
using CrawlerCore.Extractors;
using CrawlerCore.Health;
using CrawlerCore.Metrics;
using CrawlerCore.Retry;
using CrawlerCore.Robots;
using CrawlerCore.Services;
using CrawlerDownloader;
using CrawlerDownloader.Services;
using CrawlerEntity.Configuration;
using CrawlerInterFaces.Interfaces;
using CrawlerParser;
using CrawlerScheduler;
using CrawlerStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CrawlerServiceDependencyInjection.DependencyInjection
{
    /// <summary>
    /// 爬虫服务集合扩展方法
    /// </summary>
    public static class CrawlerServiceCollectionExtensions
    {
        // 添加配置服务
        public static IServiceCollection AddCrawlerConfiguration(this IServiceCollection services, string configPath = "appsettings.json")
        {
            services.AddSingleton<IConfigService>(provider =>
                new JsonConfigService(provider.GetRequiredService<ILogger<JsonConfigService>>(), configPath));

            services.AddSingleton<IConfigValidator, ConfigValidator>();

            return services;
        }
        /// <summary>
        /// 添加爬虫核心服务
        /// </summary>
        public static IServiceCollection AddCrawlerCore(this IServiceCollection services)
        {

            //services.TryAddSingleton<IStorage, SimpleUrlFilter>();
            // 注册服务和组件
            services.TryAddSingleton<IUrlFilter, SimpleUrlFilter>();
            services.TryAddSingleton<IDomainDelayManager, SimpleDomainDelayManager>();
            
            // 注册内容提取器
            services.TryAddSingleton<IContentExtractor, LinkExtractor>();
            services.TryAddSingleton<IContentExtractor, MetadataExtractor>();
            services.TryAddSingleton<IContentExtractor, ContentExtractor>();
            // 注册核心服务
            services.TryAddSingleton<CrawlerEngine>();

            return services;
        }
        // 新增：添加高级爬虫服务
        public static IServiceCollection AddAdvancedCrawler(this IServiceCollection services, Action<AdvancedCrawlConfiguration>? configure = null)
        {
            // 加载配置 // 配置高级爬虫设置
            var configService = services.BuildServiceProvider().GetRequiredService<IConfigService>();
            var config = configService.GetCurrentConfig().CrawlerConfig.ToAdvancedCrawlConfiguration();

            
            //var config = new AdvancedCrawlConfiguration();
            configure?.Invoke(config);

            services.AddSingleton(config);

            // 注册高级服务
            services.TryAddSingleton<AntiBotDetectionService>();
            var retryStrategy = new AdaptiveRetryStrategy(null,config.RetryPolicy?.MaxRetries ?? 3 );
            services.TryAddSingleton<AdaptiveRetryStrategy>(retryStrategy);
            services.TryAddSingleton<DataExportService>();
            services.TryAddSingleton<CrawlerMetrics>();
            services.TryAddSingleton<RobotsTxtParser>();

            // 注册健康检查
            services.AddHealthChecks()
                .AddCheck<CrawlerHealthCheck>("crawler_health");

            return services;
        }

        // 新增：添加 HttpClient 工厂
        public static IServiceCollection AddCrawlerHttpClient(this IServiceCollection services)
        {
            services.AddHttpClient("CrawlerClient", (provider, client) =>
            {
                // 加载配置
                var configService = provider.GetRequiredService<IConfigService>();
                var config = configService.LoadConfigAsync().GetAwaiter().GetResult().CrawlerConfig.ToAdvancedCrawlConfiguration();

                //var config = provider.GetService<AdvancedCrawlConfiguration>() ?? new AdvancedCrawlConfiguration();

                client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                UseCookies = false
            });

            return services;
        }

        public static IServiceCollection AddStorageProvider(this IServiceCollection services, string? databasePath = null) {
            // 构建服务提供者
            var serviceProvider = services.BuildServiceProvider();
            // 加载配置
            var configService = serviceProvider.GetRequiredService<IConfigService>();
            var config = configService.LoadConfigAsync().GetAwaiter().GetResult();
            // 添加存储
            // 配置存储
            var storageType = config.CrawlerConfig.Storage.Type ?? "FileSystem"; // 或 "SQLite"
            if (storageType == "SQLite")
            {
                return services.AddSQLiteStorage(databasePath ?? "crawler_data.db");
            }
            else
            {
                return services.AddFileSystemStorage(databasePath ?? "crawler_data");
            }
        }

        /// <summary>
        /// 添加文件系统存储
        /// </summary>
        public static IServiceCollection AddFileSystemStorage(this IServiceCollection services, string? baseDirectory = null)
        {
            services.TryAddSingleton<IStorageProvider>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<FileSystemStorage>>();
                return new FileSystemStorage(baseDirectory, logger);
            });

            services.TryAddSingleton<IMetadataStore>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<FileSystemStorage>>();
                return new FileSystemStorage(baseDirectory, logger);
            });

            return services;
        }

        /// <summary>
        /// 添加SQLite存储
        /// </summary>
        public static IServiceCollection AddSQLiteStorage(this IServiceCollection services, string? databasePath = null)
        {
            services.TryAddSingleton<IStorageProvider>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<SQLiteStorage>>();
                return new SQLiteStorage(databasePath, logger);
            });

            services.TryAddSingleton<IMetadataStore>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<SQLiteStorage>>();
                return new SQLiteStorage(databasePath, logger);
            });

            return services;
        }

        /// <summary>
        /// 添加下载器服务
        /// </summary>
        public static IServiceCollection AddCrawlerDownloader(this IServiceCollection services, bool useProxies = false)
        {
            services.TryAddSingleton<IDownloader>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<AdvancedDownloader>>();
                var configService = provider.GetRequiredService<IConfigService>();
                var config = configService.LoadConfigAsync().GetAwaiter().GetResult().CrawlerConfig.ToAdvancedCrawlConfiguration();

                //var config = provider.GetService<AdvancedCrawlConfiguration>() ?? new AdvancedCrawlConfiguration();
                var antiBotService = provider.GetService<AntiBotDetectionService>();
                var retryStrategy = new AdaptiveRetryStrategy(null, config.RetryPolicy?.MaxRetries ?? 3);
                services.TryAddSingleton<AdaptiveRetryStrategy>(retryStrategy);
                //var retryStrategy = provider.GetService<AdaptiveRetryStrategy>();
                var robotsTxtParser = provider.GetService<RobotsTxtParser>();
                var metrics = provider.GetService<CrawlerMetrics>();
                var dataExporter = provider.GetService<DataExportService>();
                var proxyManager = provider.GetService<ProxyManager>() ?? new ProxyManager();

                // 如果启用了代理，配置代理设置
                if (useProxies)
                {
                    config.ProxySettings = new ProxySettings
                    {
                        Enabled = true,
                        ProxyUrls = [] // 可以在配置中设置具体代理
                    };
                }

                return new AdvancedDownloader(
                    logger: logger,
                    config: config,
                    antiBotService: antiBotService,
                    retryStrategy: retryStrategy,
                    robotsTxtParser: robotsTxtParser,
                    metrics: metrics,
                    dataExporter: dataExporter,
                    proxyManager: proxyManager,
                    maxHttpClients: config.MaxConcurrentTasks
                );
            });

            // 注册 HttpClient
            services.AddHttpClient();

            return services;
        }

        /// <summary>
        /// 添加解析器服务
        /// </summary>
        public static IServiceCollection AddCrawlerParser(this IServiceCollection services)
        {
            services.TryAddSingleton<IParser, AdvancedParser>();
            return services;
        }

        /// <summary>
        /// 添加调度器服务
        /// </summary>
        public static IServiceCollection AddCrawlerScheduler(this IServiceCollection services)
        {
            services.TryAddSingleton<IScheduler, PriorityScheduler>();
            return services;
        }
    }
}