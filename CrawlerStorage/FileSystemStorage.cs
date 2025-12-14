// CrawlerStorage/FileSystemStorage.cs
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace CrawlerStorage;
/// <summary>
/// 文件系统存储提供程序
/// </summary>
public class FileSystemStorage : IStorageProvider, IMetadataStore
{
    /// <summary>
    /// 基础目录
    /// </summary>
    private readonly string _baseDirectory;
    /// <summary>
    /// 内容目录
    /// </summary>
    private readonly string _contentDirectory;
    /// <summary>
    /// 元数据目录
    /// </summary>
    private readonly string _metadataDirectory;
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<FileSystemStorage> _logger;
    /// <summary>
    /// JSON序列化设置
    /// </summary>
    private readonly JsonSerializerSettings _jsonSettings;
    /// <summary>
    /// 初始化文件系统存储提供程序
    /// </summary>
    /// <param name="baseDirectory">基础目录</param>
    /// <param name="logger">日志记录器</param>
    public FileSystemStorage(string? baseDirectory, ILogger<FileSystemStorage>? logger)
    {
        _baseDirectory = baseDirectory ?? "crawler_data";
        _contentDirectory = Path.Combine(_baseDirectory, "content");
        _metadataDirectory = Path.Combine(_baseDirectory, "metadata");
        _logger = logger ?? new Logger<FileSystemStorage>(new LoggerFactory());
        
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        EnsureDirectories();
    }
    /// <summary>
    /// 确保目录存在
    /// </summary>
    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_contentDirectory);
        Directory.CreateDirectory(_metadataDirectory);
        Directory.CreateDirectory(Path.Combine(_metadataDirectory, "crawl_states"));
        Directory.CreateDirectory(Path.Combine(_metadataDirectory, "url_states"));
    }
    /// <summary>
    /// 保存爬取结果
    /// </summary>
    /// <param name="result">爬取结果</param>
    /// <returns>任务</returns>
    public async Task SaveAsync(CrawlResult result)
    {
        try
        {
            // 保存内容
            var contentPath = GetContentFilePath(result.Request.Url);
            await SaveContentAsync(contentPath, result);

            // 保存元数据
            var urlState = new UrlState
            {
                Url = result.Request.Url,
                DiscoveredAt = DateTime.UtcNow.AddMinutes(-5), // 假设5分钟前发现
                ProcessedAt = result.ProcessedAt,
                StatusCode = result.DownloadResult.StatusCode,
                ContentLength = result.DownloadResult.RawData?.Length ?? 0,
                ContentType = result.DownloadResult.ContentType,
                DownloadTime = TimeSpan.FromMilliseconds(result.DownloadResult.DownloadTimeMs)
            };

            await SaveUrlStateAsync(urlState);

            _logger.LogDebug("Saved content for {Url}", result.Request.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save content for {Url}", result.Request.Url);
            throw;
        }
    }
    /// <summary>
    /// 保存爬取内容
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="result">爬取结果</param>
    /// <returns>任务</returns>
    private async Task SaveContentAsync(string filePath, CrawlResult result)
    {
        var contentInfo = new
        {
            result.Request.Url,
            DownloadedAt = result.ProcessedAt,
            Metadata = new
            {
                result.DownloadResult.StatusCode,
                result.DownloadResult.ContentType,
                result.DownloadResult.DownloadTimeMs,
                ContentLength = result.DownloadResult.RawData?.Length ?? 0,
                LinksFound = result.ParseResult?.Links?.Count ?? 0
            },
            Content = new
            {
                Html = result.DownloadResult.Content,
                RawData = result.DownloadResult.RawData != null ? 
                    Convert.ToBase64String(result.DownloadResult.RawData) : null,
                result.ParseResult?.ExtractedData,
                result.ParseResult?.Links
            }
        };

        var json = JsonConvert.SerializeObject(contentInfo, _jsonSettings);
        await File.WriteAllTextAsync(filePath, json);
    }
    /// <summary>
    /// 获取内容文件路径
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>文件路径</returns>
    private string GetContentFilePath(string url)
    {
        var uri = new Uri(url);
        var host = uri.Host.Replace('.', '_');
        var path = uri.AbsolutePath.Replace('/', '_').Trim('_');
        
        if (string.IsNullOrEmpty(path))
            path = "index";
        
        // 限制文件名长度
        if (path.Length > 100)
            path = path[..100];
        
        var fileName = $"{host}_{path}_{Guid.NewGuid():N}.json";
        return Path.Combine(_contentDirectory, fileName);
    }
    /// <summary>
    /// 获取指定域名的爬取结果
    /// </summary>
    /// <param name="domain">域名</param>
    /// <param name="limit">最大返回数量</param>
    /// <returns>爬取结果列表</returns>
    public async Task<IEnumerable<CrawlResult>> GetByDomainAsync(string domain, int limit = 100)
    {
        var results = new List<CrawlResult>();
        var files = Directory.GetFiles(_contentDirectory, $"*{domain}*", SearchOption.AllDirectories)
                           .Take(limit);

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var result = DeserializeCrawlResult(content);
                if (result != null)
                    results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read content file {File}", file);
            }
        }

        return results;
    }
    /// <summary>
    /// 获取指定URL的爬取结果
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>爬取结果</returns>
    public async Task<CrawlResult?> GetByUrlAsync(string url)
    {
        var files = Directory.GetFiles(_contentDirectory, "*.json", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var result = DeserializeCrawlResult(content);
                if (result?.Request?.Url == url)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read content file {File}", file);
            }
        }

        return null;
    }
    /// <summary>
    /// 反序列化爬取结果
    /// </summary>
    /// <param name="json">JSON字符串</param>
    /// <returns>爬取结果</returns>
    private CrawlResult? DeserializeCrawlResult(string json)
    {
        try
        {
            var contentInfo = JsonConvert.DeserializeObject<dynamic>(json);
            if (contentInfo == null) return null;

            return new CrawlResult
            {
                Request = new CrawlRequest { Url = contentInfo.Url },
                ProcessedAt = contentInfo.DownloadedAt,
                DownloadResult = new DownloadResult
                {
                    Url = contentInfo.Url,
                    Content = contentInfo?.Content?.Html ?? string.Empty,
                    ContentType = contentInfo?.Metadata?.ContentType ?? string.Empty,
                    StatusCode = contentInfo?.Metadata?.StatusCode,
                    DownloadTimeMs = contentInfo?.Metadata?.DownloadTimeMs
                },
                ParseResult = new ParseResult
                {
                    Links = contentInfo?.Content?.Links?.ToObject<List<string>>() ?? new List<string>(),
                    ExtractedData = contentInfo?.Content?.ExtractedData?.ToObject<Dictionary<string, object>>() 
                        ?? new Dictionary<string, object>()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize crawl result");
            return null;
        }
    }
    /// <summary>
    /// 获取总记录数
    /// </summary>
    /// <returns>记录数</returns>
    public async Task<long> GetTotalCountAsync()
    {
        var files = Directory.GetFiles(_contentDirectory, "*.json", SearchOption.AllDirectories);
        return await Task.FromResult(files.LongLength);
    }
    /// <summary>
    /// 删除指定URL的爬取结果
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>是否删除成功</returns>
    public async Task<bool> DeleteAsync(string url)
    {
        var files = Directory.GetFiles(_contentDirectory, "*.json", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var contentInfo = JsonConvert.DeserializeObject<dynamic>(content);
                if (contentInfo?.Url == url)
                {
                    File.Delete(file);
                    
                    // 同时删除URL状态
                    var urlStateFile = GetUrlStateFilePath(url);
                    if (File.Exists(urlStateFile))
                        File.Delete(urlStateFile);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete content for {Url}", url);
            }
        }

        return false;
    }
    /// <summary>
    /// 保存爬取状态
    /// </summary>
    /// <param name="state">爬取状态</param>
    /// <returns>任务</returns>
    public async Task SaveCrawlStateAsync(CrawlState state)
    {
        var filePath = Path.Combine(_metadataDirectory, "crawl_states", $"{state.JobId}.json");
        var json = JsonConvert.SerializeObject(state, _jsonSettings);
        await File.WriteAllTextAsync(filePath, json);
    }
    /// <summary>
    /// 获取指定JobID的爬取状态
    /// </summary>
    /// <param name="jobId">JobID</param>
    /// <returns>爬取状态</returns>
    public async Task<CrawlState?> GetCrawlStateAsync(string? jobId)
    {
        var filePath = Path.Combine(_metadataDirectory, "crawl_states", $"{jobId}.json");
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonConvert.DeserializeObject<CrawlState>(json);
    }
    /// <summary>
    /// 保存URL状态
    /// </summary>
    /// <param name="state">URL状态</param>
    /// <returns>任务</returns>
    public async Task SaveUrlStateAsync(UrlState state)
    {
        var filePath = GetUrlStateFilePath(state.Url);
        var json = JsonConvert.SerializeObject(state, _jsonSettings);
        await File.WriteAllTextAsync(filePath, json);
    }
    /// <summary>
    /// 获取指定URL的URL状态
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>URL状态</returns>
    public async Task<UrlState?> GetUrlStateAsync(string url)
    {
        var filePath = GetUrlStateFilePath(url);
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonConvert.DeserializeObject<UrlState>(json);
    }
    /// <summary>
    /// 获取URL状态文件路径
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>文件路径</returns>
    private string GetUrlStateFilePath(string url)
    {
        var urlHash = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(_metadataDirectory, "url_states", $"{urlHash}.json");
    }
    /// <summary>
    /// 初始化文件系统存储
    /// </summary>
    /// <returns>任务</returns>
    public Task InitializeAsync()
    {
        _logger.LogInformation("File system storage initialized at {BaseDirectory}", _baseDirectory);
        return Task.CompletedTask;
    }
    /// <summary>
    /// 关闭文件系统存储
    /// </summary>
    /// <returns>任务</returns>
    public Task ShutdownAsync()
    {
        _logger.LogInformation("File system storage shutdown");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    public async Task<CrawlStatistics> GetStatisticsAsync()
    {
        var stats = new CrawlStatistics();

        try
        {
            var contentFiles = Directory.GetFiles(_contentDirectory, "*.json", SearchOption.AllDirectories);
            var urlStateFiles = Directory.GetFiles(Path.Combine(_metadataDirectory, "url_states"), "*.json", SearchOption.AllDirectories);

            stats.TotalUrlsProcessed = contentFiles.Length;
            stats.TotalUrlsDiscovered = urlStateFiles.Length;

            // 分析内容文件获取详细统计
            long totalDownloadSize = 0;
            long totalDownloadTime = 0;
            int successCount = 0;
            int errorCount = 0;
            var domainStats = new Dictionary<string, DomainStatistics>();

            foreach (var file in contentFiles.Take(1000)) // 限制分析的文件数量以避免性能问题
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var contentInfo = JsonConvert.DeserializeObject<dynamic>(content);

                    if (contentInfo?.Metadata != null)
                    {
                        // 获取状态码
                        int statusCode = contentInfo.Metadata.StatusCode ?? 0;
                        bool isSuccess = statusCode >= 200 && statusCode < 400;

                        if (isSuccess)
                            successCount++;
                        else
                            errorCount++;

                        // 累计下载大小和时间
                        long contentLength = contentInfo.Metadata.ContentLength ?? 0;
                        long downloadTime = contentInfo.Metadata.DownloadTimeMs ?? 0;

                        totalDownloadSize += contentLength;
                        totalDownloadTime += downloadTime;

                        // 域名统计
                        string url = contentInfo?.Url?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(url))
                        {
                            var domain = GetDomainFromUrl(url);
                            if (!string.IsNullOrEmpty(domain))
                            {
                                if (!domainStats.TryGetValue(domain, out DomainStatistics? domainStat))
                                {
                                    domainStat = new DomainStatistics();
                                    domainStats[domain] = domainStat;
                                }

                                domainStat.UrlsProcessed++;
                                domainStat.TotalDownloadSize += contentLength;
                                domainStat.AverageDownloadTimeMs = (domainStat.AverageDownloadTimeMs * (domainStat.UrlsProcessed - 1) + downloadTime) / domainStat.UrlsProcessed;

                                if (isSuccess)
                                    domainStat.SuccessCount++;
                                else
                                    domainStat.ErrorCount++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze content file: {File}", file);
                }
            }

            // 更新统计信息
            stats.SuccessCount = successCount;
            stats.ErrorCount = errorCount;
            stats.TotalDownloadSize = totalDownloadSize;
            stats.AverageDownloadTimeMs = contentFiles.Length > 0 ? (double)totalDownloadTime / contentFiles.Length : 0;
            stats.DomainStats = domainStats;
            stats.LastUpdateTime = DateTime.UtcNow;

            _logger.LogInformation("Generated statistics: {TotalUrls} URLs, {SuccessCount} successes, {ErrorCount} errors",
                stats.TotalUrlsProcessed, stats.SuccessCount, stats.ErrorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate statistics");
        }

        return stats;
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    /// <returns>任务</returns>
    public async Task ClearAllAsync()
    {
        try
        {
            // 删除所有子目录和文件
            var directories = new[] { _contentDirectory, _metadataDirectory };

            foreach (var directory in directories)
            {
                if (Directory.Exists(directory))
                {
                    await DeleteDirectoryRecursiveAsync(directory);
                    _logger.LogInformation("Cleared directory: {Directory}", directory);
                }
            }

            // 重新创建目录结构
            EnsureDirectories();

            _logger.LogInformation("All data cleared successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear all data");
            throw;
        }
    }

    /// <summary>
    /// 递归删除目录
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <returns>任务</returns>
    private async Task DeleteDirectoryRecursiveAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        // 先删除所有文件
        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
                await Task.Delay(1); // 稍微延迟以避免文件系统压力
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file: {File}", file);
            }
        }

        // 然后删除所有目录（从最深的开始）
        var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length);

        foreach (var dir in directories)
        {
            try
            {
                Directory.Delete(dir, false);
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete directory: {Directory}", dir);
            }
        }

        // 最后删除根目录
        try
        {
            Directory.Delete(directoryPath, false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete root directory: {Directory}", directoryPath);
        }
    }

    /// <summary>
    /// 备份数据
    /// </summary>
    /// <param name="backupPath">备份路径</param>
    /// <returns>任务</returns>
    public async Task BackupAsync(string backupPath)
    {
        try
        {
            if (string.IsNullOrEmpty(backupPath))
                throw new ArgumentException("Backup path cannot be null or empty", nameof(backupPath));

            // 确保备份目录存在
            var backupDir = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            // 如果备份路径是文件，则创建zip备份
            if (Path.HasExtension(backupPath))
            {
                await CreateZipBackupAsync(backupPath);
            }
            else
            {
                // 如果是目录，则复制整个目录结构
                await CopyDirectoryAsync(_baseDirectory, backupPath);
            }

            _logger.LogInformation("Backup created successfully: {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup: {BackupPath}", backupPath);
            throw;
        }
    }

    /// <summary>
    /// 创建ZIP备份
    /// </summary>
    /// <param name="zipFilePath">ZIP文件路径</param>
    /// <returns>任务</returns>
    private async Task CreateZipBackupAsync(string zipFilePath)
    {
        // 注意：这里需要引用 System.IO.Compression 命名空间
        // 在项目中添加对 System.IO.Compression.FileSystem 的引用（如果使用 .NET Framework）
        // 对于 .NET Core/5+，使用 System.IO.Compression.ZipFile

        try
        {
            // 检查是否已存在备份文件，如果存在则删除
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            // 使用 System.IO.Compression.ZipFile 创建压缩包
            System.IO.Compression.ZipFile.CreateFromDirectory(_baseDirectory, zipFilePath,
                System.IO.Compression.CompressionLevel.Optimal, false);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create ZIP backup: {ZipFilePath}", zipFilePath);
            throw;
        }
    }

    /// <summary>
    /// 复制目录
    /// </summary>
    /// <param name="sourceDir">源目录</param>
    /// <param name="destinationDir">目标目录</param>
    /// <returns>任务</returns>
    private async Task CopyDirectoryAsync(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        // 创建目标目录
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        // 复制所有文件
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destinationDir, relativePath);

            // 确保目标目录存在
            var destDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(file, destFile, true);

            // 添加小延迟以避免文件系统压力
            await Task.Delay(1);
        }

        _logger.LogDebug("Copied {FileCount} files from {SourceDir} to {DestinationDir}",
            files.Length, sourceDir, destinationDir);
    }

    /// <summary>
    /// 从URL中提取域名
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>域名</returns>
    private static string? GetDomainFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从URL中提取路径
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>路径</returns>
    private static string? GetPathFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.AbsolutePath;
        }
        catch
        {
            return null;
        }
    }
    
}