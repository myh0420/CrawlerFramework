// <copyright file="ConsulConfigService.cs" company="PlaceholderCompany">
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
    using CrawlerFramework.CrawlerEntity.Configuration;
    using CrawlerFramework.CrawlerInterFaces.Interfaces.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Consul配置服务实现，用于从Consul配置中心加载和保存配置.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConsulConfigService"/> class.
    /// 初始化 <see cref="ConsulConfigService"/> 类的新实例.
    /// </remarks>
    public class ConsulConfigService : IConfigService
    {
        /// <summary>
        /// 内部日志记录器实例.
        /// </summary>
        private readonly ILogger<ConsulConfigService> logger;

        /// <summary>
        /// 内部配置验证器实例.
        /// </summary>
        private readonly IConfigValidator validator;

        /// <summary>
        /// 内部JSON序列化选项，用于将配置对象转换为JSON字符串.
        /// </summary>
        private readonly JsonSerializerOptions serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        /// <summary>
        /// 内部JSON反序列化选项，用于将JSON字符串转换为配置对象.
        /// </summary>
        private readonly JsonSerializerOptions deserializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// 当前配置实例.
        /// </summary>
        private AppCrawlerConfig currentConfig;

        /// <summary>
        /// Consul客户端实例，用于与Consul配置中心通信.
        /// </summary>
        private ConsulClient? consulClient;

        /// <summary>
        /// 配置刷新定时器，用于定期从Consul获取最新配置.
        /// </summary>
        private Timer? configRefreshTimer;

        /// <summary>
        /// Consul中配置的键前缀.
        /// </summary>
        private string configPrefix = "crawler/config";

        /// <summary>
        /// 配置刷新间隔（秒）.
        /// </summary>
        private int refreshIntervalSeconds = 30;

        /// <summary>
        /// 配置监控状态标志，指示是否正在监控配置变化.
        /// </summary>
        private bool isMonitoring = false;

        /// <inheritdoc/>
        /// <summary>
        /// 配置改变事件，当配置发生变化时触发.
        /// </summary>
        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsulConfigService"/> class.
        /// 初始化 <see cref="ConsulConfigService"/> 类的新实例.
        /// </summary>
        /// <param name="logger">日志记录器实例，用于记录配置服务相关的日志信息.</param>
        /// <param name="validator">配置验证器实例，用于验证配置的有效性.</param>
        /// <param name="localConfig">本地配置实例，作为默认配置使用.</param>
        public ConsulConfigService(ILogger<ConsulConfigService>? logger, IConfigValidator? validator, AppCrawlerConfig localConfig)
        {
            this.logger = logger ?? new Logger<ConsulConfigService>(new LoggerFactory());
            this.validator = validator ?? new ConfigValidator();
            this.currentConfig = localConfig;
        }

        /// <summary>
        /// 异步从Consul加载配置.
        /// </summary>
        /// <param name="configPath">Consul中配置的键前缀，默认值为 <see cref="configPrefix"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task<AppCrawlerConfig> LoadConfigAsync(string? configPath = null)
        {
            if (this.consulClient == null)
            {
                this.InitializeConsulClient();
            }

            if (this.consulClient == null)
            {
                this.logger.LogWarning("Cannot load config from Consul, using local config");
                return this.currentConfig;
            }

            try
            {
                // 加载主配置
                var mainConfigKey = $"{this.configPrefix}/main";
                var mainConfigResponse = await this.consulClient.KV.Get(mainConfigKey);

                if (mainConfigResponse.Response == null)
                {
                    this.logger.LogWarning("Config not found in Consul at {Key}, using local config", mainConfigKey);
                    return this.currentConfig;
                }

                var mainConfigJson = System.Text.Encoding.UTF8.GetString(mainConfigResponse.Response.Value);
                var config = JsonSerializer.Deserialize<AppCrawlerConfig>(mainConfigJson, this.deserializerOptions);

                if (config == null)
                {
                    this.logger.LogError("Failed to deserialize config from Consul");
                    return this.currentConfig;
                }

                // 验证配置
                var validationResult = this.validator.Validate(config);
                if (!validationResult.IsValid)
                {
                    this.logger.LogError("Config validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                    return this.currentConfig;
                }

                // 记录警告
                foreach (var warning in validationResult.Warnings)
                {
                    this.logger.LogWarning("Config warning: {Warning}", warning);
                }

                var oldConfig = this.currentConfig;
                this.currentConfig = config;

                // 触发配置改变事件
                this.ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
                {
                    OldConfig = oldConfig,
                    NewConfig = config,
                });

                this.logger.LogInformation("Config loaded successfully from Consul");
                return this.currentConfig;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load config from Consul");
                return this.currentConfig;
            }
        }

        /// <summary>
        /// 异步保存配置到Consul.
        /// </summary>
        /// <param name="config">要保存的配置实例.</param>
        /// <param name="configPath">Consul中配置的键前缀，默认值为 <see cref="configPrefix"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task SaveConfigAsync(AppCrawlerConfig config, string? configPath = null)
        {
            if (this.consulClient == null)
            {
                this.InitializeConsulClient();
            }

            if (this.consulClient == null)
            {
                this.logger.LogWarning("Cannot save config to Consul, config center is not enabled");
                return;
            }

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

                // 保存主配置
                var mainConfigKey = $"{this.configPrefix}/main";
                var configJson = JsonSerializer.Serialize(config, this.serializerOptions);
                var configBytes = System.Text.Encoding.UTF8.GetBytes(configJson);

                await this.consulClient.KV.Put(new KVPair(mainConfigKey) { Value = configBytes });
                this.logger.LogInformation("Config saved successfully to Consul");

                var oldConfig = this.currentConfig;
                this.currentConfig = config;

                // 触发配置改变事件
                this.ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
                {
                    OldConfig = oldConfig,
                    NewConfig = config,
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save config to Consul");
                throw;
            }
        }

        /// <summary>
        /// 获取当前配置实例.
        /// </summary>
        /// <returns>当前的应用爬虫配置.</returns>
        public AppCrawlerConfig GetCurrentConfig()
        {
            return this.currentConfig;
        }

        /// <summary>
        /// 开始监控配置变化.
        /// </summary>
        public void StartMonitoring()
        {
            if (this.isMonitoring)
            {
                this.logger.LogInformation("Config monitoring is already running");
                return;
            }

            if (this.consulClient == null)
            {
                this.InitializeConsulClient();
            }

            if (this.consulClient == null)
            {
                this.logger.LogWarning("Cannot start monitoring, Consul client is not initialized");
                return;
            }

            this.configRefreshTimer = new Timer(
                async (state) => await this.RefreshConfigAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(this.refreshIntervalSeconds));

            this.isMonitoring = true;
            this.logger.LogInformation("Config monitoring started with refresh interval: {Interval} seconds", this.refreshIntervalSeconds);
        }

        /// <summary>
        /// 停止监控配置变化.
        /// </summary>
        public void StopMonitoring()
        {
            if (this.configRefreshTimer != null)
            {
                this.configRefreshTimer.Dispose();
                this.configRefreshTimer = null;
            }

            this.isMonitoring = false;
            this.logger.LogInformation("Config monitoring stopped");
        }

        /// <summary>
        /// 异步刷新配置，从Consul重新加载配置并触发配置变更事件（如果配置发生变化）.
        /// </summary>
        private async Task RefreshConfigAsync()
        {
            try
            {
                await this.LoadConfigAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to refresh config from Consul");
            }
        }

        /// <summary>
        /// 初始化Consul客户端.
        /// </summary>
        private void InitializeConsulClient()
        {
            if (this.currentConfig.ConfigCenter == null || !this.currentConfig.ConfigCenter.Enabled)
            {
                this.logger.LogWarning("Config center is not enabled or not configured");
                return;
            }

            var consulConfig = this.currentConfig.ConfigCenter.Consul;
            this.configPrefix = consulConfig.ConfigPrefix;
            this.refreshIntervalSeconds = consulConfig.RefreshIntervalSeconds;

            var consulClientConfig = new ConsulClientConfiguration
            {
                Address = new Uri(consulConfig.Address),
            };

            if (!string.IsNullOrEmpty(consulConfig.Datacenter))
            {
                consulClientConfig.Datacenter = consulConfig.Datacenter;
            }

            if (!string.IsNullOrEmpty(consulConfig.Token))
            {
                consulClientConfig.Token = consulConfig.Token;
            }

            this.consulClient = new ConsulClient(consulClientConfig);
            this.logger.LogInformation("Consul client initialized with address: {Address}", consulConfig.Address);
        }
    }
}