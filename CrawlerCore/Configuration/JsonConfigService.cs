// <copyright file="JsonConfigService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Configuration
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Consul;
    using CrawlerFramework.CrawlerCore.Security;
    using CrawlerFramework.CrawlerEntity.Configuration;
    using CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// JSON配置服务实现，用于从JSON文件加载和保存配置.
    /// </summary>
    public class JsonConfigService : IConfigService
    {
        /// <summary>
        /// 内部 ILogger 实例.
        /// </summary>
        private readonly ILogger<JsonConfigService> logger;

        /// <summary>
        /// 内部 IConfigValidator 实例.
        /// </summary>
        private readonly IConfigValidator validator;

        /// <summary>
        /// 内部 ISecurityService 实例，用于敏感数据加密解密.
        /// </summary>
        private readonly ISecurityService securityService;

        /// <summary>
        /// 内部默认配置文件路径.
        /// </summary>
        private readonly string defaultConfigPath;

        /// <summary>
        /// 内部 JSON 反序列化选项.
        /// </summary>
        private readonly JsonSerializerOptions deserializeOptions = new ()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// 内部 JSON 序列化选项.
        /// </summary>
        private readonly JsonSerializerOptions serializeOptions = new ()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// 内部 AppCrawlerConfig 实例.
        /// </summary>
        private AppCrawlerConfig currentConfig = new ();

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonConfigService"/> class.
        /// 初始化 <see cref="JsonConfigService"/> 类的新实例.
        /// </summary>
        /// <param name="logger">日志记录器实例（可选）.</param>
        /// <param name="validator">配置验证器实例（可选）.</param>
        /// <param name="securityService">安全服务实例（可选）.</param>
        /// <param name="defaultConfigPath">默认配置文件路径（可选，默认为appsettings.json）.</param>
        public JsonConfigService(
            ILogger<JsonConfigService>? logger,
            IConfigValidator? validator,
            ISecurityService? securityService,
            string defaultConfigPath = "appsettings.json")
        {
            var loggerFactory = new LoggerFactory();
            this.logger = logger ?? new Logger<JsonConfigService>(loggerFactory);
            this.validator = validator ?? new ConfigValidator();

            // 创建AesSecurityService专用的logger实例
            var aesLogger = loggerFactory.CreateLogger<AesSecurityService>();
            this.securityService = securityService ?? new AesSecurityService(aesLogger);
            this.defaultConfigPath = defaultConfigPath;
        }

        /// <summary>
        /// 配置改变事件
        /// </summary>
        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged = null;

        /// <summary>
        /// 异步加载配置文件.
        /// </summary>
        /// <param name="configPath">配置文件路径（可选）.</param>
        /// <returns>应用爬虫配置.</returns>
        public async Task<AppCrawlerConfig> LoadConfigAsync(string? configPath = null)
        {
            var path = configPath ?? this.defaultConfigPath;

            try
            {
                if (!File.Exists(path))
                {
                    this.logger.LogWarning("Config file not found at {Path}, creating default config", path);
                    this.currentConfig = CreateDefaultConfig();
                    await this.SaveConfigAsync(this.currentConfig, path);
                    return this.currentConfig;
                }

                var json = await File.ReadAllTextAsync(path);
                var config = JsonSerializer.Deserialize<AppCrawlerConfig>(json, this.deserializeOptions);

                if (config == null)
                {
                    this.logger.LogError("Failed to deserialize config, creating default config");
                    this.currentConfig = CreateDefaultConfig();
                    return this.currentConfig;
                }

                // 验证配置
                var validationResult = this.validator.Validate(config);
                if (!validationResult.IsValid)
                {
                    this.logger.LogError("Config validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                    this.currentConfig = CreateDefaultConfig();
                    return this.currentConfig;
                }

                // 记录警告
                foreach (var warning in validationResult.Warnings)
                {
                    this.logger.LogWarning("Config warning: {Warning}", warning);
                }

                // 解密敏感数据
                await this.DecryptSensitiveDataAsync(config);

                this.currentConfig = config;
                this.logger.LogInformation("Config loaded and validated successfully from {Path}", path);

                return this.currentConfig;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load config from {Path}", path);
                this.currentConfig = CreateDefaultConfig();
                return this.currentConfig;
            }
        }

        /// <summary>
        /// 异步保存配置文件.
        /// </summary>
        /// <param name="config">应用爬虫配置.</param>
        /// <param name="configPath">配置文件路径（可选）.</param>
        /// <returns>任务.</returns>
        public async Task SaveConfigAsync(AppCrawlerConfig config, string? configPath = null)
        {
            var path = configPath ?? this.defaultConfigPath;

            try
            {
                // 验证配置
                var validationResult = this.validator.Validate(config);
                if (!validationResult.IsValid)
                {
                    this.logger.LogError("Config validation failed, cannot save invalid config: {Errors}", string.Join(", ", validationResult.Errors));
                    throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validationResult.Errors)}");
                }

                // 记录警告
                foreach (var warning in validationResult.Warnings)
                {
                    this.logger.LogWarning("Config warning: {Warning}", warning);
                }

                var oldConfig = this.currentConfig;
                this.currentConfig = config;

                // 创建配置副本并加密敏感数据
                var configToSave = this.CloneConfig(config);
                await this.EncryptSensitiveDataAsync(configToSave);

                var json = JsonSerializer.Serialize(new { configToSave.CrawlerConfig, configToSave.Logging, configToSave.AllowedHosts }, this.serializeOptions);
                await File.WriteAllTextAsync(path, json);

                this.logger.LogInformation("Config saved successfully to {Path}", path);

                // 触发配置改变事件
                this.ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
                {
                    OldConfig = oldConfig,
                    NewConfig = config,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save config to {Path}", path);
                throw;
            }
        }

        /// <summary>
        /// 获取当前配置.
        /// </summary>
        /// <returns>应用爬虫配置.</returns>
        public AppCrawlerConfig GetCurrentConfig()
        {
            return this.currentConfig ?? CreateDefaultConfig();
        }

        /// <summary>
        /// 监视配置文件变化，当文件发生修改时自动重新加载配置.
        /// </summary>
        /// <param name="configPath">配置文件路径（可选，默认为默认配置文件路径）.</param>
        public void WatchConfigFile(string? configPath = null)
        {
            var path = configPath ?? this.defaultConfigPath;
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);

            if (directory == null)
            {
                return;
            }

            var watcher = new FileSystemWatcher
            {
                Path = directory,
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite,
            };

            watcher.Changed += async (sender, e) =>
            {
                this.logger.LogInformation("Config file changed, reloading...");
                await Task.Delay(500); // 等待文件写入完成
                await this.LoadConfigAsync(path);
            };

            watcher.EnableRaisingEvents = true;
            this.logger.LogInformation("Started watching config file: {Path}", path);
        }

        /// <summary>
        /// 创建默认配置.
        /// </summary>
        /// <returns>应用爬虫配置.</returns>
        private static AppCrawlerConfig CreateDefaultConfig()
        {
            return new AppCrawlerConfig
            {
                CrawlerConfig = new CrawlerConfigSection(),
            };
        }

        /// <summary>
        /// 克隆配置对象，避免修改原始配置.
        /// </summary>
        /// <param name="config">要克隆的配置对象.</param>
        /// <returns>克隆后的配置对象.</returns>
        private AppCrawlerConfig CloneConfig(AppCrawlerConfig config)
        {
            var json = JsonSerializer.Serialize(config, this.serializeOptions);
            return JsonSerializer.Deserialize<AppCrawlerConfig>(json, this.deserializeOptions) ?? CreateDefaultConfig();
        }

        /// <summary>
        /// 异步加密配置中的敏感数据.
        /// </summary>
        /// <param name="config">要加密的配置对象.</param>
        /// <returns>任务.</returns>
        private async Task EncryptSensitiveDataAsync(AppCrawlerConfig config)
        {
            if (config?.CrawlerConfig?.Proxy == null)
            {
                return;
            }

            // 加密代理URL
            if (config.CrawlerConfig.Proxy.ProxyUrls != null && config.CrawlerConfig.Proxy.ProxyUrls.Length > 0)
            {
                config.CrawlerConfig.Proxy.EncryptedProxyUrls = await this.securityService.EncryptArrayAsync(config.CrawlerConfig.Proxy.ProxyUrls);
                config.CrawlerConfig.Proxy.ProxyUrls = [];
            }
        }

        /// <summary>
        /// 异步解密配置中的敏感数据.
        /// </summary>
        /// <param name="config">要解密的配置对象.</param>
        /// <returns>任务.</returns>
        private async Task DecryptSensitiveDataAsync(AppCrawlerConfig config)
        {
            if (config?.CrawlerConfig?.Proxy == null)
            {
                return;
            }

            // 解密代理URL
            if (config.CrawlerConfig.Proxy.EncryptedProxyUrls != null && config.CrawlerConfig.Proxy.EncryptedProxyUrls.Length > 0)
            {
                config.CrawlerConfig.Proxy.ProxyUrls = await this.securityService.DecryptArrayAsync(config.CrawlerConfig.Proxy.EncryptedProxyUrls);
            }
        }
    }
}