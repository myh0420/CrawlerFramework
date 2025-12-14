// CrawlerCore/Configuration/ConfigService.cs
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CrawlerCore.Configuration
{
    /// <summary>
    /// 配置服务
    /// </summary>
    public interface IConfigService
    {
        Task<AppCrawlerConfig> LoadConfigAsync(string? configPath = null);
        Task SaveConfigAsync(AppCrawlerConfig config, string? configPath = null);
        AppCrawlerConfig GetCurrentConfig();
        event EventHandler<ConfigChangedEventArgs> ConfigChanged;
    }

    public class ConfigChangedEventArgs : EventArgs
    {
        public AppCrawlerConfig NewConfig { get; set; } = new();
        public AppCrawlerConfig OldConfig { get; set; } = new();
    }

    /// <summary>
    /// JSON配置服务实现
    /// </summary>
    public class JsonConfigService(ILogger<JsonConfigService>? logger, IConfigValidator? validator, string defaultConfigPath = "appsettings.json") : IConfigService
    {
        /// <summary>
        /// 内部 ILogger 实例
        /// </summary>
        private readonly ILogger<JsonConfigService> _logger = logger ?? new Logger<JsonConfigService>(new LoggerFactory());
        /// <summary>
        /// 内部 IConfigValidator 实例
        /// </summary>
        private readonly IConfigValidator _validator = validator ?? new ConfigValidator();
        /// <summary>
        /// 内部 AppCrawlerConfig 实例
        /// </summary>
        private AppCrawlerConfig _currentConfig = new();
        /// <summary>
        /// 内部默认配置文件路径
        /// </summary>
        private readonly string _defaultConfigPath = defaultConfigPath;
        /// <summary>
        /// 内部 JSON 反序列化选项
        /// </summary>
        private readonly JsonSerializerOptions _deserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        /// <summary>
        /// 内部 JSON 序列化选项
        /// </summary>
        private readonly JsonSerializerOptions _serializeOptions = new() 
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        /// <summary>
        /// 配置改变事件
        /// </summary>
        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged = null;
        /// <summary>
        /// 异步加载配置文件
        /// </summary>
        /// <param name="configPath">配置文件路径（可选）</param>
        /// <returns>应用爬虫配置</returns>
        public async Task<AppCrawlerConfig> LoadConfigAsync(string? configPath = null)
        {
            var path = configPath ?? _defaultConfigPath;
            
            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogWarning("Config file not found at {Path}, creating default config", path);
                    _currentConfig = CreateDefaultConfig();
                    await SaveConfigAsync(_currentConfig, path);
                    return _currentConfig;
                }

                var json = await File.ReadAllTextAsync(path);
                var config = JsonSerializer.Deserialize<AppCrawlerConfig>(json, _deserializeOptions);

                if (config == null)
                {
                    _logger.LogError("Failed to deserialize config, creating default config");
                    _currentConfig = CreateDefaultConfig();
                    return _currentConfig;
                }

                // 验证配置
                var validationResult = _validator.Validate(config);
                if (!validationResult.IsValid)
                {
                    _logger.LogError("Config validation failed: {Errors}", string.Join(", ", validationResult.Errors));
                    _currentConfig = CreateDefaultConfig();
                    return _currentConfig;
                }

                // 记录警告
                foreach (var warning in validationResult.Warnings)
                {
                    _logger.LogWarning("Config warning: {Warning}", warning);
                }

                _currentConfig = config;
                _logger.LogInformation("Config loaded and validated successfully from {Path}", path);
                
                return _currentConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config from {Path}", path);
                _currentConfig = CreateDefaultConfig();
                return _currentConfig;
            }
        }
        /// <summary>
        /// 异步保存配置文件
        /// </summary>
        /// <param name="config">应用爬虫配置</param>
        /// <param name="configPath">配置文件路径（可选）</param>
        /// <returns>任务</returns>
        public async Task SaveConfigAsync(AppCrawlerConfig config, string? configPath = null)
        {
            var path = configPath ?? _defaultConfigPath;
            
            try
            {
                // 验证配置
                var validationResult = _validator.Validate(config);
                if (!validationResult.IsValid)
                {
                    _logger.LogError("Config validation failed, cannot save invalid config: {Errors}", string.Join(", ", validationResult.Errors));
                    throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", validationResult.Errors)}");
                }

                // 记录警告
                foreach (var warning in validationResult.Warnings)
                {
                    _logger.LogWarning("Config warning: {Warning}", warning);
                }

                var oldConfig = _currentConfig;
                _currentConfig = config;

                var json = JsonSerializer.Serialize(new { config.CrawlerConfig, config.Logging, config.AllowedHosts }, _serializeOptions);
                await File.WriteAllTextAsync(path, json);

                _logger.LogInformation("Config saved successfully to {Path}", path);

                // 触发配置改变事件
                ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
                {
                    OldConfig = oldConfig,
                    NewConfig = config
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save config to {Path}", path);
                throw;
            }
        }
        /// <summary>
        /// 获取当前配置
        /// </summary>
        /// <returns>应用爬虫配置</returns>
        public AppCrawlerConfig GetCurrentConfig()
        {
            return _currentConfig ?? CreateDefaultConfig();
        }
        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>应用爬虫配置</returns>
        private static AppCrawlerConfig CreateDefaultConfig()
        {
            return new AppCrawlerConfig
            {
                CrawlerConfig = new CrawlerConfigSection()
            };
        }

        /// <summary>
        /// 监视配置文件变化（可选功能）
        /// </summary>
        public void WatchConfigFile(string? configPath = null)
        {
            var path = configPath ?? _defaultConfigPath;
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);

            if (directory == null) return;

            var watcher = new FileSystemWatcher
            {
                Path = directory,
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite
            };

            watcher.Changed += async (sender, e) =>
            {
                _logger.LogInformation("Config file changed, reloading...");
                await Task.Delay(500); // 等待文件写入完成
                await LoadConfigAsync(path);
            };

            watcher.EnableRaisingEvents = true;
            _logger.LogInformation("Started watching config file: {Path}", path);
        }
    }
}