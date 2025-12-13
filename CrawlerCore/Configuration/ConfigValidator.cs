// CrawlerCore/Configuration/ConfigValidator.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrawlerCore.Configuration
{
    /// <summary>
    /// 配置验证服务
    /// </summary>
    public interface IConfigValidator
    {
        ValidationResult Validate(AppCrawlerConfig config);
        IEnumerable<string> GetValidationErrors(AppCrawlerConfig config);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
    }

    public class ConfigValidator : IConfigValidator
    {
        public ValidationResult Validate(AppCrawlerConfig config)
        {
            var result = new ValidationResult();
            var crawlerConfig = config.CrawlerConfig;

            // 基础配置验证
            if (crawlerConfig.Basic.MaxConcurrentTasks <= 0)
                result.Errors.Add("MaxConcurrentTasks must be greater than 0");

            if (crawlerConfig.Basic.MaxConcurrentTasks > 100)
                result.Warnings.Add("High concurrency may cause performance issues");

            if (crawlerConfig.Basic.MaxDepth < 0)
                result.Errors.Add("MaxDepth cannot be negative");

            if (crawlerConfig.Basic.MaxDepth > 10)
                result.Warnings.Add("Very deep crawling may take long time");

            if (crawlerConfig.Basic.MaxPages <= 0)
                result.Errors.Add("MaxPages must be greater than 0");

            if (crawlerConfig.Basic.RequestDelay.TotalMilliseconds < 0)
                result.Errors.Add("RequestDelay cannot be negative");

            if (crawlerConfig.Basic.TimeoutSeconds <= 0)
                result.Errors.Add("TimeoutSeconds must be greater than 0");

            // 域名配置验证
            if (crawlerConfig.Domains.AllowedDomains != null)
            {
                foreach (var domain in crawlerConfig.Domains.AllowedDomains)
                {
                    if (string.IsNullOrWhiteSpace(domain))
                        result.Errors.Add("AllowedDomains contains empty domain");

                    if (!Uri.TryCreate($"http://{domain}", UriKind.Absolute, out _))
                        result.Warnings.Add($"Domain '{domain}' may not be valid");
                }
            }

            if (crawlerConfig.Domains.BlockedPatterns != null)
            {
                foreach (var pattern in crawlerConfig.Domains.BlockedPatterns)
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                        result.Errors.Add("BlockedPatterns contains empty pattern");
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
                result.Errors.Add("TestIntervalMinutes must be greater than 0");

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
                        result.Errors.Add($"Invalid seed URL: {url}");
                }
            }

            // 性能配置验证
            if (crawlerConfig.Performance.MemoryLimitMB <= 0)
                result.Errors.Add("MemoryLimitMB must be greater than 0");

            if (crawlerConfig.Performance.MemoryLimitMB > 2048)
                result.Warnings.Add("Very high memory limit may cause system issues");

            if (crawlerConfig.Performance.MaxQueueSize <= 0)
                result.Errors.Add("MaxQueueSize must be greater than 0");

            // 反爬虫配置验证
            if (crawlerConfig.AntiBot.RetryPolicy.MaxRetries < 0)
                result.Errors.Add("AntiBot.RetryPolicy.MaxRetries cannot be negative");

            if (crawlerConfig.AntiBot.RetryPolicy.InitialDelay.TotalMilliseconds < 0)
                result.Errors.Add("AntiBot.RetryPolicy.InitialDelay cannot be negative");

            if (crawlerConfig.AntiBot.RetryPolicy.BackoffMultiplier < 1.0)
                result.Errors.Add("AntiBot.RetryPolicy.BackoffMultiplier must be at least 1.0");

            if (crawlerConfig.AntiBot.RetryPolicy.MaxDelay.TotalMilliseconds < crawlerConfig.AntiBot.RetryPolicy.InitialDelay.TotalMilliseconds)
                result.Errors.Add("AntiBot.RetryPolicy.MaxDelay cannot be less than InitialDelay");

            // 存储配置验证
            if (string.IsNullOrWhiteSpace(crawlerConfig.Storage.Type))
                result.Errors.Add("Storage.Type cannot be empty");

            if (crawlerConfig.Storage.Type.Equals("FileSystem", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(crawlerConfig.Storage.FileSystemPath))
                    result.Errors.Add("Storage.FileSystemPath cannot be empty when Type is FileSystem");
            }
            else if (crawlerConfig.Storage.Type.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(crawlerConfig.Storage.DatabaseConnection))
                    result.Errors.Add("Storage.DatabaseConnection cannot be empty when Type is Database");
            }
            else if (!crawlerConfig.Storage.Type.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add($"Unknown storage type: {crawlerConfig.Storage.Type}");
            }

            // 监控配置验证
            if (crawlerConfig.Monitoring.MetricsIntervalSeconds <= 0)
                result.Errors.Add("Monitoring.MetricsIntervalSeconds must be greater than 0");

            // 数据处理配置验证
            if (crawlerConfig.DataProcessing.MinContentLength < 0)
                result.Errors.Add("DataProcessing.MinContentLength cannot be negative");

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

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

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

        public IEnumerable<string> GetValidationErrors(AppCrawlerConfig config)
        {
            var result = Validate(config);
            return result.Errors.Concat(result.Warnings);
        }
    }
}