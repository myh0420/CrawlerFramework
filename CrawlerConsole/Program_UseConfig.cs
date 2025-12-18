// <copyright file="Program_UseConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerConsole
{
    using CrawlerFramework.CrawlerCore;
    using CrawlerFramework.CrawlerCore.Configuration;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
    using CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration;
    using CrawlerFramework.CrawlerServiceDependencyInjection.DependencyInjection;
    using CrawlerFramework.CrawlerStorage;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 程序入口类，使用配置文件版本.
    /// </summary>
    internal class Program_UseConfig
    {
        /// <summary>
        /// 程序入口点，使用配置文件版本.
        /// </summary>
        private static async Task MainUseConfig()
        {
            // 创建服务集合
            var services = new ServiceCollection();
            ConfigureServices(services);

            // 构建服务提供者
            var serviceProvider = services.BuildServiceProvider();

            // 加载配置
            var configService = serviceProvider.GetRequiredService<IConfigService>();
            var config = await configService.LoadConfigAsync();

            var crawler = serviceProvider.GetRequiredService<CrawlerEngine>();

            // 注册事件
            crawler.OnCrawlCompleted += (s, e) =>
            {
                Console.WriteLine($"Completed: {e.Url} (Depth: {e.Depth})");
            };

            crawler.OnCrawlError += (s, e) =>
            {
                Console.WriteLine($"Error: {e.Url} (Depth: {e.Depth}): {e.Exception.Message}");
            };

            crawler.OnUrlDiscovered += (s, e) =>
            {
                Console.WriteLine($"Discovered: {e.AddedUrls} new URLs from {e.SourceUrl}, Total discovered: {e.DiscoveredUrls.Count}");
            };

            crawler.OnStatusChanged += (s, e) =>
            {
                Console.WriteLine($"[STATUS] {e.PreviousStatus} → {e.CurrentStatus}: {e.Message}");
            };

            // 启动爬虫
            await crawler.StartAsync(config.CrawlerConfig.ToAdvancedCrawlConfiguration());

            // 添加种子URL
            await crawler.AddSeedUrlsAsync(config.CrawlerConfig.Seeds.SeedUrls);

            Console.WriteLine("Crawler started. Press any key to stop...");
            Console.ReadKey();

            await crawler.StopAsync();
        }

        /// <summary>
        /// 配置服务集合.
        /// </summary>
        /// <param name="services">服务集合.</param>
        private static void ConfigureServices(IServiceCollection services)
        {
            // 添加日志
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // 添加配置服务
            services.AddSingleton<IConfigService, JsonConfigService>();
            services.AddSingleton<IConfigValidator, ConfigValidator>();

            // 添加爬虫核心服务
            services.AddCrawlerCore();

            // 添加存储（根据配置决定使用哪种存储）
            services.AddTransient<IStorageProvider>(provider =>
            {
                var configService = provider.GetRequiredService<IConfigService>();
                var config = configService.GetCurrentConfig();
                var logger = provider.GetRequiredService<ILogger<IStorageProvider>>();

                return config.CrawlerConfig.Storage.Type.ToLower() switch
                {
                    "sqlite" => new SQLiteStorage(config.CrawlerConfig.Storage.DatabaseConnection, null),
                    _ => new FileSystemStorage(config.CrawlerConfig.Storage.FileSystemPath, null),
                };
            });

            services.AddTransient<IMetadataStore>(provider =>
            {
                var configService = provider.GetRequiredService<IConfigService>();
                var config = configService.GetCurrentConfig();
                var logger = provider.GetRequiredService<ILogger<IMetadataStore>>();

                return config.CrawlerConfig.Storage.Type.ToLower() switch
                {
                    "sqlite" => new SQLiteStorage(config.CrawlerConfig.Storage.DatabaseConnection, null),
                    _ => new FileSystemStorage(config.CrawlerConfig.Storage.FileSystemPath, null),
                };
            });

            // 添加下载器
            services.AddCrawlerDownloader();

            // 添加解析器
            services.AddCrawlerParser();

            // 添加调度器
            services.AddCrawlerScheduler();
        }
    }
}