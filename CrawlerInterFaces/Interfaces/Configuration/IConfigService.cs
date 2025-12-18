// <copyright file="IConfigService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Consul;
    using CrawlerFramework.CrawlerEntity.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 配置服务接口，定义了加载、保存和获取配置的方法.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// 配置改变事件，当配置发生变化时触发.
        /// </summary>
        event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        /// <summary>
        /// 异步加载配置.
        /// </summary>
        /// <param name="configPath">配置文件路径（可选，仅适用于文件配置服务）.</param>
        /// <returns>应用爬虫配置实例.</returns>
        Task<AppCrawlerConfig> LoadConfigAsync(string? configPath = null);

        /// <summary>
        /// 异步保存配置.
        /// </summary>
        /// <param name="config">要保存的应用爬虫配置实例.</param>
        /// <param name="configPath">配置文件路径（可选，仅适用于文件配置服务）.</param>
        /// <returns>任务.</returns>
        Task SaveConfigAsync(AppCrawlerConfig config, string? configPath = null);

        /// <summary>
        /// 获取当前配置实例.
        /// </summary>
        /// <returns>当前应用爬虫配置实例.</returns>
        AppCrawlerConfig GetCurrentConfig();
    }
}
