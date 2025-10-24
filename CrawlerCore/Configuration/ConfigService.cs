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
    public class JsonConfigService(ILogger<JsonConfigService>? logger, string defaultConfigPath = "appsettings.json") : IConfigService
    {
        private readonly ILogger<JsonConfigService> _logger = logger ?? new Logger<JsonConfigService>(new LoggerFactory());
        private AppCrawlerConfig _currentConfig = new();
        private readonly string _defaultConfigPath = defaultConfigPath;
        private readonly JsonSerializerOptions _deserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        private readonly JsonSerializerOptions _serializeOptions = new() 
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged = null;

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

                _currentConfig = config ?? CreateDefaultConfig();
                _logger.LogInformation("Config loaded successfully from {Path}", path);
                
                return _currentConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config from {Path}", path);
                _currentConfig = CreateDefaultConfig();
                return _currentConfig;
            }
        }

        public async Task SaveConfigAsync(AppCrawlerConfig config, string? configPath = null)
        {
            var path = configPath ?? _defaultConfigPath;
            
            try
            {
                var oldConfig = _currentConfig;
                _currentConfig = config;

                var json = JsonSerializer.Serialize(new { config.CrawlerConfig }, _serializeOptions);
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

        public AppCrawlerConfig GetCurrentConfig()
        {
            return _currentConfig ?? CreateDefaultConfig();
        }

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