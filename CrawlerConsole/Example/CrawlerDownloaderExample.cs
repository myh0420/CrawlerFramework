// <copyright file="CrawlerDownloaderExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerConsole.Example
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using CrawlerDownloader;
    using CrawlerDownloader.Services;
    using CrawlerEntity.Models;
    using CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 示例：使用依赖注入配置下载器.
    /// </summary>
    internal class CrawlerDownloaderExample
    {
        /// <summary>
        /// 示例：使用依赖注入配置下载器.
        /// </summary>
        public static void CrawlerDownloaderTest()
        {
            // 使用依赖注入
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
                .AddHttpClient()
                .AddSingleton<IDownloader, AdvancedDownloader>()
                .BuildServiceProvider();

            var downloader = serviceProvider.GetRequiredService<IDownloader>();

            // 或者手动创建
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<AdvancedDownloader>();
            var httpClient = new HttpClient();

            //// 不使用代理
            // var downloader1 = new AdvancedDownloader(httpClient, logger);

            //// 使用代理
            // var downloader2 = new AdvancedDownloader(httpClient, logger, true);

            //// 使用特定代理列表
            ////var proxyList = new[] { "192.168.1.100:8080", "192.168.1.101:8080" };
            // ProxyManager proxyManager = new();
            // proxyManager.AddProxy(new() { Port = 8080, Host = "192.168.1.100" });
            // proxyManager.AddProxy(new() { Port = 8080, Host = "192.168.1.101" });
            // var downloader3 = new AdvancedDownloader(httpClient, logger, true , proxyManager);
        }
    }
}
