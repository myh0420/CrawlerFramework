// <copyright file="CrawlerServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerServiceDependencyInjection.DependencyInjection
{
    using CrawlerFramework.CrawlerCore;
    using CrawlerFramework.CrawlerCore.Security;
    using CrawlerFramework.CrawlerCore.AI;
    using CrawlerFramework.CrawlerCore.AntiBot;
    using CrawlerFramework.CrawlerCore.Configuration;
    using CrawlerFramework.CrawlerCore.ErrorHandling;
    using CrawlerFramework.CrawlerCore.Export;
    using CrawlerFramework.CrawlerCore.Extractors;
    using CrawlerFramework.CrawlerCore.Health;
    using CrawlerFramework.CrawlerCore.Metrics;
    using CrawlerFramework.CrawlerCore.Plugins;
    using CrawlerFramework.CrawlerCore.Retry;
    using CrawlerFramework.CrawlerCore.Robots;
    using CrawlerFramework.CrawlerCore.Services;
    using CrawlerFramework.CrawlerCore.Utils;
    using CrawlerFramework.CrawlerDownloader;
    using CrawlerFramework.CrawlerDownloader.Services;
    using CrawlerFramework.CrawlerEntity.Configuration;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
    using CrawlerFramework.CrawlerParser;
    using CrawlerFramework.CrawlerScheduler;
    using CrawlerFramework.CrawlerStorage;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;
    using CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration;

    /// <summary>
    /// 爬虫服务集合扩展方法.
    /// </summary>
    public static class CrawlerServiceCollectionExtensions
    {
        // 添加配置服务

        /// <summary>
        /// 添加爬虫配置服务.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="configPath">配置文件路径.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        public static IServiceCollection AddCrawlerConfiguration(this IServiceCollection services, string configPath = "appsettings.json")
        {
            services.AddSingleton<IConfigValidator, ConfigValidator>();
            services.AddSingleton<ISecurityService, AesSecurityService>();

            // 先创建一个临时的JsonConfigService来加载初始配置
            services.AddSingleton(provider =>
            {
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                var validator = provider.GetRequiredService<IConfigValidator>();
                var securityService = provider.GetRequiredService<ISecurityService>();

                // 创建临时JsonConfigService加载本地配置
                var tempConfigService = new JsonConfigService(
                    logger: loggerFactory.CreateLogger<JsonConfigService>(),
                    validator: validator,
                    securityService: securityService,
                    defaultConfigPath: configPath);

                // 同步加载配置（在DI容器构建期间）
                var localConfig = tempConfigService.LoadConfigAsync().GetAwaiter().GetResult();

                // 使用ConfigServiceFactory创建最终的配置服务
                var configService = ConfigServiceFactory.CreateConfigService(loggerFactory, validator, localConfig);

                // 如果是ConsulConfigService，启动监控
                if (configService is ConsulConfigService consulConfigService)
                {
                    consulConfigService.StartMonitoring();
                }
                else if (configService is JsonConfigService jsonConfigService)
                {
                    // 如果是JsonConfigService，启动文件监控
                    jsonConfigService.WatchConfigFile(configPath);
                }

                return configService;
            });

            return services;
        }

        /// <summary>
        /// 添加爬虫核心服务.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        public static IServiceCollection AddCrawlerCore(this IServiceCollection services)
        {
            // services.TryAddSingleton<IStorage, SimpleUrlFilter>();
            // 注册服务和组件
            services.TryAddSingleton<IUrlFilter, SimpleUrlFilter>();
            services.TryAddSingleton<IDomainDelayManager, SimpleDomainDelayManager>();

            // 注册内容提取器
            services.TryAddSingleton<IContentExtractor, LinkExtractor>();
            services.TryAddSingleton<IContentExtractor, MetadataExtractor>();
            services.TryAddSingleton<IContentExtractor, ContentExtractor>();

            // 注册错误处理服务
            services.TryAddSingleton<IErrorHandlingService, ErrorHandlingService>();

            // 注册插件加载器
            services.TryAddSingleton<IPluginLoader, PluginLoader>();

            // 注册核心服务
            services.TryAddSingleton<CrawlerEngine>();

            return services;
        }

        // 新增：添加高级爬虫服务

        /// <summary>
        /// 添加高级爬虫服务.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="configure">配置回调.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        public static IServiceCollection AddAdvancedCrawler(this IServiceCollection services, Action<AdvancedCrawlConfiguration>? configure = null)
        {
            // 创建默认配置
            var config = new AdvancedCrawlConfiguration();
            configure?.Invoke(config);

            services.AddSingleton(config);

            // 注册高级服务
            services.TryAddSingleton<AntiBotDetectionService>();
            services.TryAddSingleton<IAntiBotEvasionService, AntiBotEvasionService>();
            services.TryAddSingleton<AdaptiveRetryStrategy>(sp =>
                new AdaptiveRetryStrategy(
                    sp.GetService<ILogger<AdaptiveRetryStrategy>>(),
                    config.RetryPolicy?.MaxRetries ?? 3));
            services.TryAddSingleton<DataExportService>();
            services.TryAddSingleton<CrawlerMetrics>();
            services.TryAddSingleton<RobotsTxtParser>();
            services.TryAddSingleton<IAIHelper, AIAssistedHelper>();

            // AIAssistedExtractor is managed by AdvancedParser, not registered directly

            // 注册健康检查
            services.AddHealthChecks()
                .AddCheck<CrawlerHealthCheck>("crawler_health");

            return services;
        }

        // 新增：添加 HttpClient 工厂

        /// <summary>
        /// 添加 HttpClient 工厂.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <returns>
        /// ///服务集合.
        /// </returns>
        public static IServiceCollection AddCrawlerHttpClient(this IServiceCollection services)
        {
            services.AddHttpClient("CrawlerClient", (provider, client) =>
            {
                // 加载配置
                var config = provider.GetService<AdvancedCrawlConfiguration>() ?? new AdvancedCrawlConfiguration();

                client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
                client.DefaultRequestHeaders.Add(
                    "User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add(
                    "Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                UseCookies = false,
            });

            return services;
        }

        /// <summary>
        /// 添加存储提供程序.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="databasePath">数据库路径.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        /// <remarks>
        /// 此方法默认使用文件系统存储，避免在注册时依赖配置服务。.
        /// </remarks>
        public static IServiceCollection AddStorageProvider(this IServiceCollection services, string? databasePath = null)
        {
            // 默认使用文件系统存储，避免在注册时依赖配置服务
            return services.AddFileSystemStorage(databasePath ?? "crawler_data");
        }

        /// <summary>
        /// 添加存储提供程序.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="baseDirectory">基础目录.</param>
        /// <returns>
        /// ///服务集合.
        /// </returns>
        /// <remarks>
        /// 此方法默认使用文件系统存储，避免在注册时依赖配置服务。.
        /// </remarks>
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
        /// 添加SQLite存储.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="databasePath">数据库路径.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        /// <remarks>
        /// 此方法默认使用文件系统存储，避免在注册时依赖配置服务。.
        /// </remarks>
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
        /// 添加MySQL存储.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="connectionString">数据库连接字符串.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        public static IServiceCollection AddMySQLStorage(this IServiceCollection services, string? connectionString = null)
        {
            services.TryAddSingleton<IStorageProvider>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<MySQLStorage>>();
                return new MySQLStorage(connectionString, logger);
            });

            services.TryAddSingleton<IMetadataStore>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<MySQLStorage>>();
                return new MySQLStorage(connectionString, logger);
            });

            return services;
        }

        /// <summary>
        /// 添加MongoDB存储.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="connectionString">数据库连接字符串.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        public static IServiceCollection AddMongoDBStorage(this IServiceCollection services, string? connectionString = null)
        {
            services.TryAddSingleton<IStorageProvider>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<MongoDBStorage>>();
                return new MongoDBStorage(connectionString, logger);
            });

            services.TryAddSingleton<IMetadataStore>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<MongoDBStorage>>();
                return new MongoDBStorage(connectionString, logger);
            });

            return services;
        }

        /// <summary>
        /// 添加下载器服务.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <param name="useProxies">是否使用代理.</param>
        /// <remarks>
        /// 此方法默认使用文件系统存储，避免在注册时依赖配置服务。.
        /// </remarks>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        public static IServiceCollection AddCrawlerDownloader(this IServiceCollection services, bool useProxies = false)
        {
            // 注册下载器相关服务
            services.TryAddSingleton<ProxyManager>();
            services.TryAddSingleton<RotatingUserAgentService>();
            services.TryAddSingleton<SimpleHttpClientManager>(sp =>
            {
                var config = sp.GetRequiredService<AdvancedCrawlConfiguration>();
                return new SimpleHttpClientManager(config.MaxConcurrentTasks);
            });

            services.TryAddSingleton<IDownloader>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<AdvancedDownloader>>();
                var config = provider.GetRequiredService<AdvancedCrawlConfiguration>();
                var httpClientManager = provider.GetRequiredService<SimpleHttpClientManager>();
                var userAgentService = provider.GetRequiredService<RotatingUserAgentService>();
                var proxyManager = provider.GetRequiredService<ProxyManager>();
                var errorHandlingService = provider.GetService<IErrorHandlingService>();
                var antiBotService = provider.GetService<AntiBotDetectionService>();
                var antiBotEvasionService = provider.GetService<IAntiBotEvasionService>();
                var retryStrategy = provider.GetRequiredService<AdaptiveRetryStrategy>();
                var robotsTxtParser = provider.GetService<RobotsTxtParser>();
                var metrics = provider.GetService<CrawlerMetrics>();
                var dataExporter = provider.GetService<DataExportService>();

                // 如果启用了代理，配置代理设置
                if (useProxies)
                {
                    config.ProxySettings = new ProxySettings
                    {
                        Enabled = true,
                        ProxyUrls = [], // 可以在配置中设置具体代理
                    };
                }

                return new AdvancedDownloader(
                    logger: logger,
                    config: config,
                    httpClientManager: httpClientManager,
                    userAgentService: userAgentService,
                    proxyManager: proxyManager,
                    errorHandlingService: errorHandlingService,
                    antiBotService: antiBotService,
                    antiBotEvasionService: antiBotEvasionService,
                    retryStrategy: retryStrategy,
                    robotsTxtParser: robotsTxtParser,
                    metrics: metrics,
                    dataExporter: dataExporter);
            });

            // 注册 HttpClient
            services.AddHttpClient();

            return services;
        }

        /// <summary>
        /// 添加解析器服务.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        /// <remarks>
        /// 此方法默认使用文件系统存储，避免在注册时依赖配置服务。.
        /// </remarks>
        public static IServiceCollection AddCrawlerParser(this IServiceCollection services)
        {
            services.TryAddSingleton<IParser, AdvancedParser>();
            return services;
        }

        /// <summary>
        /// 添加调度器服务.
        /// </summary>
        /// <param name="services">服务集合.</param>
        /// <returns>
        /// /// 服务集合.
        /// </returns>
        /// <remarks>
        /// 此方法默认使用文件系统存储，避免在注册时依赖配置服务。.
        /// </remarks>
        public static IServiceCollection AddCrawlerScheduler(this IServiceCollection services)
        {
            services.TryAddSingleton<IScheduler, PriorityScheduler>();
            return services;
        }
    }
}