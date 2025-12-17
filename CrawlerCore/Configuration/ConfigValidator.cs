// <copyright file="ConfigValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// 配置验证服务实现.
    /// </summary>
    public class ConfigValidator : IConfigValidator
    {
        /// <summary>
        /// 验证应用爬虫配置的有效性，包括基础配置、域名配置、代理配置、种子URL、性能配置、反爬虫配置、存储配置、监控配置、数据处理配置和日志配置的验证.
        /// </summary>
        /// <param name="config">要验证的应用爬虫配置实例.</param>
        /// <returns>验证结果，包含验证是否通过、错误信息和警告信息.</returns>
        public ValidationResult Validate(AppCrawlerConfig config)
        {
            var result = new ValidationResult();
            var crawlerConfig = config.CrawlerConfig;

            // 基础配置验证
            if (crawlerConfig.Basic.MaxConcurrentTasks <= 0)
            {
                result.Errors.Add("MaxConcurrentTasks must be greater than 0");
            }

            if (crawlerConfig.Basic.MaxConcurrentTasks > 100)
            {
                result.Warnings.Add("High concurrency may cause performance issues");
            }

            if (crawlerConfig.Basic.MaxDepth < 0)
            {
                result.Errors.Add("MaxDepth cannot be negative");
            }

            if (crawlerConfig.Basic.MaxDepth > 10)
            {
                result.Warnings.Add("Very deep crawling may take long time");
            }

            if (crawlerConfig.Basic.MaxPages <= 0)
            {
                result.Errors.Add("MaxPages must be greater than 0");
            }

            if (crawlerConfig.Basic.RequestDelay.TotalMilliseconds < 0)
            {
                result.Errors.Add("RequestDelay cannot be negative");
            }

            if (crawlerConfig.Basic.TimeoutSeconds <= 0)
            {
                result.Errors.Add("TimeoutSeconds must be greater than 0");
            }

            // 域名配置验证
            if (crawlerConfig.Domains.AllowedDomains != null)
            {
                foreach (var domain in crawlerConfig.Domains.AllowedDomains)
                {
                    if (string.IsNullOrWhiteSpace(domain))
                    {
                        result.Errors.Add("AllowedDomains contains empty domain");
                    }

                    if (!Uri.TryCreate($"http://{domain}", UriKind.Absolute, out _))
                    {
                        result.Warnings.Add($"Domain '{domain}' may not be valid");
                    }
                }
            }

            if (crawlerConfig.Domains.BlockedPatterns != null)
            {
                foreach (var pattern in crawlerConfig.Domains.BlockedPatterns)
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                    {
                        result.Errors.Add("BlockedPatterns contains empty pattern");
                    }
                }
            }

            // 代理配置验证
            if (crawlerConfig.Proxy.Enabled && crawlerConfig.Proxy.ProxyUrls != null)
            {
                foreach (var proxy in crawlerConfig.Proxy.ProxyUrls)
                {
                    if (string.IsNullOrWhiteSpace(proxy))
                    {
                        result.Errors.Add("ProxyUrls contains empty proxy");
                        continue;
                    }

                    var parts = proxy.Split(':');
                    if (parts.Length < 2)
                    {
                        result.Errors.Add($"Invalid proxy format: {proxy}");
                    }
                }
            }

            if (crawlerConfig.Proxy.TestIntervalMinutes <= 0)
            {
                result.Errors.Add("TestIntervalMinutes must be greater than 0");
            }

            // 种子URL验证
            if (crawlerConfig.Seeds.SeedUrls != null)
            {
                foreach (var url in crawlerConfig.Seeds.SeedUrls)
                {
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        result.Errors.Add("SeedUrls contains empty URL");
                        continue;
                    }

                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        result.Errors.Add($"Invalid seed URL: {url}");
                    }
                }
            }

            // 性能配置验证
            if (crawlerConfig.Performance.MemoryLimitMB <= 0)
            {
                result.Errors.Add("MemoryLimitMB must be greater than 0");
            }

            if (crawlerConfig.Performance.MemoryLimitMB > 2048)
            {
                result.Warnings.Add("Very high memory limit may cause system issues");
            }

            if (crawlerConfig.Performance.MaxQueueSize <= 0)
            {
                result.Errors.Add("MaxQueueSize must be greater than 0");
            }

            // 反爬虫配置验证
            if (crawlerConfig.AntiBot.RetryPolicy.MaxRetries < 0)
            {
                result.Errors.Add("AntiBot.RetryPolicy.MaxRetries cannot be negative");
            }

            if (crawlerConfig.AntiBot.RetryPolicy.InitialDelay.TotalMilliseconds < 0)
            {
                result.Errors.Add("AntiBot.RetryPolicy.InitialDelay cannot be negative");
            }

            if (crawlerConfig.AntiBot.RetryPolicy.BackoffMultiplier < 1.0)
            {
                result.Errors.Add("AntiBot.RetryPolicy.BackoffMultiplier must be at least 1.0");
            }

            if (crawlerConfig.AntiBot.RetryPolicy.MaxDelay.TotalMilliseconds < crawlerConfig.AntiBot.RetryPolicy.InitialDelay.TotalMilliseconds)
            {
                result.Errors.Add("AntiBot.RetryPolicy.MaxDelay cannot be less than InitialDelay");
            }

            // 存储配置验证
            if (string.IsNullOrWhiteSpace(crawlerConfig.Storage.Type))
            {
                result.Errors.Add("Storage.Type cannot be empty");
            }

            if (crawlerConfig.Storage.Type.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(crawlerConfig.Storage.FileSystemPath))
                {
                    result.Errors.Add("Storage.FileSystemPath cannot be empty when Type is FileSystem");
                }
            }
            else if (crawlerConfig.Storage.Type.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(crawlerConfig.Storage.DatabaseConnection))
                {
                    result.Errors.Add("Storage.DatabaseConnection cannot be empty when Type is Database");
                }
            }
            else if (!crawlerConfig.Storage.Type.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add($"Unknown storage type: {crawlerConfig.Storage.Type}");
            }

            // 监控配置验证
            if (crawlerConfig.Monitoring.MetricsIntervalSeconds <= 0)
            {
                result.Errors.Add("Monitoring.MetricsIntervalSeconds must be greater than 0");
            }

            // 数据处理配置验证
            if (crawlerConfig.DataProcessing.MinContentLength < 0)
            {
                result.Errors.Add("DataProcessing.MinContentLength cannot be negative");
            }

            // Logging配置验证
            if (config.Logging?.LogLevel == null)
            {
                result.Errors.Add("Logging.LogLevel configuration is missing");
            }
            else
            {
                // 验证日志级别是否有效
                var validLogLevels = new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None" };
                ValidateLogLevel(config.Logging.LogLevel.Default, "Logging.LogLevel.Default", validLogLevels, result);
                ValidateLogLevel(config.Logging.LogLevel.Microsoft, "Logging.LogLevel.Microsoft", validLogLevels, result);
                ValidateLogLevel(config.Logging.LogLevel.System, "Logging.LogLevel.System", validLogLevels, result);
                ValidateLogLevel(config.Logging.LogLevel.MicrosoftAspNetCore, "Logging.LogLevel.Microsoft.AspNetCore", validLogLevels, result);
            }

            // 配置中心验证
            if (config.ConfigCenter != null && config.ConfigCenter.Enabled)
            {
                if (string.IsNullOrWhiteSpace(config.ConfigCenter.Type))
                {
                    result.Errors.Add("ConfigCenter.Type cannot be empty when ConfigCenter is enabled");
                }
                else if (config.ConfigCenter.Type.Equals("Consul", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(config.ConfigCenter.Consul.Address))
                    {
                        result.Errors.Add("ConfigCenter.Consul.Address cannot be empty when ConfigCenter.Type is Consul");
                    }
                    else if (!Uri.TryCreate(config.ConfigCenter.Consul.Address, UriKind.Absolute, out _))
                    {
                        result.Errors.Add($"ConfigCenter.Consul.Address is invalid URL: {config.ConfigCenter.Consul.Address}");
                    }

                    if (string.IsNullOrWhiteSpace(config.ConfigCenter.Consul.ConfigPrefix))
                    {
                        result.Errors.Add("ConfigCenter.Consul.ConfigPrefix cannot be empty when ConfigCenter.Type is Consul");
                    }

                    if (config.ConfigCenter.Consul.RefreshIntervalSeconds <= 0)
                    {
                        result.Errors.Add("ConfigCenter.Consul.RefreshIntervalSeconds must be greater than 0");
                    }
                }
                else
                {
                    result.Warnings.Add($"Unknown ConfigCenter.Type: {config.ConfigCenter.Type}");
                }
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// 获取应用爬虫配置的验证错误信息和警告信息.
        /// </summary>
        /// <param name="config">要验证的应用爬虫配置实例.</param>
        /// <returns>验证错误信息和警告信息的合并列表.</returns>
        public IEnumerable<string> GetValidationErrors(AppCrawlerConfig config)
        {
            var result = this.Validate(config);
            return result.Errors.Concat(result.Warnings);
        }

        /// <summary>
        /// 验证日志级别是否有效.
        /// </summary>
        /// <param name="logLevel">日志级别.</param>
        /// <param name="configPath">配置路径.</param>
        /// <param name="validLogLevels">有效日志级别数组.</param>
        /// <param name="result">验证结果.</param>
        private static void ValidateLogLevel(string logLevel, string configPath, string[] validLogLevels, ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(logLevel))
            {
                result.Errors.Add($"{configPath} cannot be empty");
                return;
            }

            if (!validLogLevels.Contains(logLevel, StringComparer.OrdinalIgnoreCase))
            {
                result.Errors.Add($"{configPath} contains invalid log level: '{logLevel}'. Valid levels: {string.Join(", ", validLogLevels)}");
            }
        }
    }
}