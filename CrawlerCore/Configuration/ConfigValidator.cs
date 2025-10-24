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

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        public IEnumerable<string> GetValidationErrors(AppCrawlerConfig config)
        {
            var result = Validate(config);
            return result.Errors.Concat(result.Warnings);
        }
    }
}