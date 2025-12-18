// <copyright file="ConfigServiceFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Configuration
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Consul;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// 配置服务工厂类.
    /// </summary>
    public static class ConfigServiceFactory
    {
        /// <summary>
        /// 创建配置服务实例，根据配置中心是否启用自动选择JsonConfigService或ConsulConfigService.
        /// </summary>
        /// <param name="loggerFactory">日志工厂实例.</param>
        /// <param name="validator">配置验证器实例（可选）.</param>
        /// <param name="localConfig">本地配置实例（可选）.</param>
        /// <returns>配置服务实例.</returns>
        public static IConfigService CreateConfigService(ILoggerFactory loggerFactory, IConfigValidator? validator = null, AppCrawlerConfig? localConfig = null)
        {
            var logger = loggerFactory.CreateLogger<JsonConfigService>();
            validator ??= new ConfigValidator();
            localConfig ??= new AppCrawlerConfig();

            // 如果配置中心已启用，使用ConsulConfigService，否则使用JsonConfigService
            if (localConfig.ConfigCenter != null && localConfig.ConfigCenter.Enabled)
            {
                return new ConsulConfigService(loggerFactory.CreateLogger<ConsulConfigService>(), validator, localConfig);
            }

            return new JsonConfigService(logger, validator, null);
        }
    }
}