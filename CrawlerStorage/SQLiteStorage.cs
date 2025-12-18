// <copyright file="SQLiteStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerStorage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SQLite;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using CrawlerEntity.Enums;
    using CrawlerEntity.Models;
    using CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// SQLite 存储实现.
    /// </summary>
    public class SQLiteStorage : IStorageProvider, IMetadataStore, IDisposable
    {
        /// <summary>
        /// SQLite 存储实现.
        /// </summary>
        private readonly SQLiteConnection connection;

        /// <summary>
        /// 日志记录器.
        /// </summary>
        private readonly ILogger<SQLiteStorage> logger;

        /// <summary>
        /// 数据库路径.
        /// </summary>
        private readonly string databasePath;

        /// <summary>
        /// 是否已释放.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SQLiteStorage"/> class.
        /// 构造函数.
        /// </summary>
        /// <param name="databasePath">数据库路径.</param>
        /// <param name="logger">日志记录器.</param>
        public SQLiteStorage(string? databasePath, ILogger<SQLiteStorage>? logger)
        {
            this.databasePath = databasePath ?? "crawler.db";
            this.logger = logger ?? new Logger<SQLiteStorage>(new LoggerFactory());
            this.connection = new SQLiteConnection($"Data Source={this.databasePath};Version=3;");
        }

        /// <summary>
        /// 初始化数据库.
        /// </summary>
        /// <returns>任务.</returns>
        public async Task InitializeAsync()
        {
            try
            {
                await this.connection.OpenAsync();
                await this.CreateTablesAsync();
                this.logger.LogInformation("SQLite storage initialized with database: {DatabasePath}", this.databasePath);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize SQLite storage");
                throw;
            }
        }

        /// <summary>
        /// 异步保存爬取结果.
        /// </summary>
        /// <param name="result">爬取结果.</param>
        /// <returns>任务.</returns>
        public async Task SaveAsync(CrawlResult result)
        {
            await this.BatchSaveAsync([result]);
        }

        /// <summary>
        /// 异步批量保存爬取结果.
        /// </summary>
        /// <param name="results">爬取结果列表.</param>
        /// <returns>任务.</returns>
        public async Task BatchSaveAsync(List<CrawlResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return;
            }

            using var transaction = this.connection.BeginTransaction();

            try
            {
                // 批量插入CrawlResults
                var crawlResultsCommand = new SQLiteCommand(
                    @"INSERT OR REPLACE INTO CrawlResults 
                      (Url, Content, RawData, ContentType, StatusCode, DownloadTimeMs, Depth, Referrer, Title, TextContent, ErrorMessage, IsSuccess)
                      VALUES (@Url, @Content, @RawData, @ContentType, @StatusCode, @DownloadTimeMs, @Depth, @Referrer, @Title, @TextContent, @ErrorMessage, @IsSuccess)",
                    this.connection,
                    transaction);

                // 批量插入ExtractedData
                var extractedDataCommand = new SQLiteCommand(
                    @"INSERT INTO ExtractedData (Url, DataKey, DataValue, DataType)
                      VALUES (@Url, @Key, @Value, @Type)",
                    this.connection,
                    transaction);

                // 批量插入Links
                var linksCommand = new SQLiteCommand(
                    @"INSERT OR IGNORE INTO Links (SourceUrl, TargetUrl)
                      VALUES (@SourceUrl, @TargetUrl)",
                    this.connection,
                    transaction);

                // 批量插入Images
                var imagesCommand = new SQLiteCommand(
                    @"INSERT INTO Images (Url, ImageUrl)
                      VALUES (@Url, @ImageUrl)",
                    this.connection,
                    transaction);

                // 批量插入UrlStates
                var urlStatesCommand = new SQLiteCommand(
                    @"INSERT OR REPLACE INTO UrlStates 
                      (Url, DiscoveredAt, ProcessedAt, StatusCode, ContentLength, ContentType, DownloadTimeMs, ErrorMessage, RetryCount)
                      VALUES (@Url, @DiscoveredAt, @ProcessedAt, @StatusCode, @ContentLength, @ContentType, @DownloadTimeMs, @ErrorMessage, @RetryCount)",
                    this.connection,
                    transaction);

                // 准备参数
                var crawlResultsParams = new SQLiteParameterCollectionWrapper(crawlResultsCommand.Parameters);
                var extractedDataParams = new SQLiteParameterCollectionWrapper(extractedDataCommand.Parameters);
                var linksParams = new SQLiteParameterCollectionWrapper(linksCommand.Parameters);
                var imagesParams = new SQLiteParameterCollectionWrapper(imagesCommand.Parameters);
                var urlStatesParams = new SQLiteParameterCollectionWrapper(urlStatesCommand.Parameters);

                foreach (var result in results)
                {
                    // 插入CrawlResults
                    crawlResultsParams["@Url"].Value = result.Request.Url;
                    crawlResultsParams["@Content"].Value = result.DownloadResult.Content;
                    crawlResultsParams["@RawData"].Value = result.DownloadResult.RawData;
                    crawlResultsParams["@ContentType"].Value = result.DownloadResult.ContentType;
                    crawlResultsParams["@StatusCode"].Value = result.DownloadResult.StatusCode;
                    crawlResultsParams["@DownloadTimeMs"].Value = result.DownloadResult.DownloadTimeMs;
                    crawlResultsParams["@Depth"].Value = result.Request.Depth;
                    crawlResultsParams["@Referrer"].Value = result.Request.Referrer ?? (object)DBNull.Value;
                    crawlResultsParams["@Title"].Value = result.ParseResult?.Title ?? (object)DBNull.Value;
                    crawlResultsParams["@TextContent"].Value = result.ParseResult?.TextContent ?? (object)DBNull.Value;
                    crawlResultsParams["@ErrorMessage"].Value = result.DownloadResult.ErrorMessage ?? (object)DBNull.Value;
                    crawlResultsParams["@IsSuccess"].Value = result.DownloadResult.IsSuccess ? 1 : 0;
                    await crawlResultsCommand.ExecuteNonQueryAsync();

                    // 插入ExtractedData
                    if (result.ParseResult?.ExtractedData != null)
                    {
                        // 先删除旧数据
                        var deleteExtractedDataCommand = new SQLiteCommand("DELETE FROM ExtractedData WHERE Url = @Url", this.connection, transaction);
                        deleteExtractedDataCommand.Parameters.AddWithValue("@Url", result.Request.Url);
                        await deleteExtractedDataCommand.ExecuteNonQueryAsync();

                        foreach (var data in result.ParseResult.ExtractedData)
                        {
                            extractedDataParams["@Url"].Value = result.Request.Url;
                            extractedDataParams["@Key"].Value = data.Key;
                            extractedDataParams["@Value"].Value = data.Value?.ToString() ?? string.Empty;
                            extractedDataParams["@Type"].Value = data.Value?.GetType().Name ?? "String";
                            await extractedDataCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // 插入Links
                    if (result.ParseResult?.Links != null)
                    {
                        // 先删除旧数据
                        var deleteLinksCommand = new SQLiteCommand("DELETE FROM Links WHERE SourceUrl = @SourceUrl", this.connection, transaction);
                        deleteLinksCommand.Parameters.AddWithValue("@SourceUrl", result.Request.Url);
                        await deleteLinksCommand.ExecuteNonQueryAsync();

                        foreach (var link in result.ParseResult.Links)
                        {
                            linksParams["@SourceUrl"].Value = result.Request.Url;
                            linksParams["@TargetUrl"].Value = link;
                            await linksCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // 插入Images
                    if (result.ParseResult?.Images != null)
                    {
                        // 先删除旧数据
                        var deleteImagesCommand = new SQLiteCommand("DELETE FROM Images WHERE Url = @Url", this.connection, transaction);
                        deleteImagesCommand.Parameters.AddWithValue("@Url", result.Request.Url);
                        await deleteImagesCommand.ExecuteNonQueryAsync();

                        foreach (var imageUrl in result.ParseResult.Images)
                        {
                            imagesParams["@Url"].Value = result.Request.Url;
                            imagesParams["@ImageUrl"].Value = imageUrl;
                            await imagesCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // 插入UrlStates
                    var urlState = new UrlState
                    {
                        Url = result.Request.Url,
                        DiscoveredAt = DateTime.UtcNow.AddMinutes(-5), // 假设5分钟前发现
                        ProcessedAt = result.ProcessedAt,
                        StatusCode = result.DownloadResult.StatusCode,
                        ContentLength = result.DownloadResult.RawData?.Length ?? 0,
                        ContentType = result.DownloadResult.ContentType,
                        DownloadTime = TimeSpan.FromMilliseconds(result.DownloadResult.DownloadTimeMs),
                        ErrorMessage = result.DownloadResult.ErrorMessage ?? string.Empty,
                    };

                    urlStatesParams["@Url"].Value = urlState.Url;
                    urlStatesParams["@DiscoveredAt"].Value = urlState.DiscoveredAt;
                    urlStatesParams["@ProcessedAt"].Value = urlState.ProcessedAt ?? (object)DBNull.Value;
                    urlStatesParams["@StatusCode"].Value = urlState.StatusCode;
                    urlStatesParams["@ContentLength"].Value = urlState.ContentLength;
                    urlStatesParams["@ContentType"].Value = urlState.ContentType ?? (object)DBNull.Value;
                    urlStatesParams["@DownloadTimeMs"].Value = urlState.DownloadTime.TotalMilliseconds;
                    urlStatesParams["@ErrorMessage"].Value = urlState.ErrorMessage ?? (object)DBNull.Value;
                    urlStatesParams["@RetryCount"].Value = urlState.RetryCount;
                    await urlStatesCommand.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                this.logger.LogDebug("Batch saved {Count} crawl results to SQLite", results.Count);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.logger.LogError(ex, "Failed to batch save crawl results");
                throw;
            }
        }

        /// <summary>
        /// 异步根据域名获取爬取结果.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="limit">限制数量.</param>
        /// <returns>爬取结果列表.</returns>
        public async Task<IEnumerable<CrawlResult>> GetByDomainAsync(string domain, int limit = 100)
        {
            var results = new List<CrawlResult>();

            var command = new SQLiteCommand(
                @"SELECT * FROM CrawlResults 
                  WHERE Url LIKE @Domain 
                  ORDER BY ProcessedAt DESC 
                  LIMIT @Limit",
                this.connection);

            command.Parameters.AddWithValue("@Domain", $"%{domain}%");
            command.Parameters.AddWithValue("@Limit", limit);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var result = await this.ReadCrawlResultAsync((SQLiteDataReader)reader);
                if (result != null)
                {
                    results.Add(result);
                }
            }

            return results;
        }

        /// <summary>
        /// 异步根据URL获取爬取结果.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>爬取结果.</returns>
        public async Task<CrawlResult?> GetByUrlAsync(string url)
        {
            var command = new SQLiteCommand(
                "SELECT * FROM CrawlResults WHERE Url = @Url",
                this.connection);

            command.Parameters.AddWithValue("@Url", url);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return await this.ReadCrawlResultAsync((SQLiteDataReader)reader);
            }

            return null;
        }

        /// <summary>
        /// 异步获取总记录数.
        /// </summary>
        /// <returns>总记录数.</returns>
        public async Task<long> GetTotalCountAsync()
        {
            var command = new SQLiteCommand("SELECT COUNT(*) FROM CrawlResults", this.connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }

        /// <summary>
        /// 异步根据URL删除爬取结果.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>是否删除成功.</returns>
        public async Task<bool> DeleteAsync(string url)
        {
            using var transaction = this.connection.BeginTransaction();

            try
            {
                // 删除相关数据（由于外键约束，应该自动级联删除）
                var deleteCommands = new[]
                {
                    "DELETE FROM CrawlResults WHERE Url = @Url",
                    "DELETE FROM UrlStates WHERE Url = @Url",
                };

                foreach (var deleteCommand in deleteCommands)
                {
                    using var command = new SQLiteCommand(deleteCommand, this.connection, transaction);
                    command.Parameters.AddWithValue("@Url", url);
                    await command.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                this.logger.LogInformation("Deleted data for URL: {Url}", url);
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.logger.LogError(ex, "Failed to delete data for {Url}", url);
                return false;
            }
        }

        /// <summary>
        /// 异步保存爬取状态.
        /// </summary>
        /// <param name="state">爬取状态.</param>
        /// <returns>任务.</returns>
        public async Task SaveCrawlStateAsync(CrawlState state)
        {
            var command = new SQLiteCommand(
                @"INSERT OR REPLACE INTO CrawlStates 
                  (JobId, StartTime, EndTime, TotalUrlsDiscovered, TotalUrlsProcessed, TotalErrors, Statistics, Status, Configuration)
                  VALUES (@JobId, @StartTime, @EndTime, @TotalUrlsDiscovered, @TotalUrlsProcessed, @TotalErrors, @Statistics, @Status, @Configuration)",
                this.connection);

            command.Parameters.AddWithValue("@JobId", state.JobId);
            command.Parameters.AddWithValue("@StartTime", state.StartTime);
            command.Parameters.AddWithValue("@EndTime", state.EndTime ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@TotalUrlsDiscovered", state.TotalUrlsDiscovered);
            command.Parameters.AddWithValue("@TotalUrlsProcessed", state.TotalUrlsProcessed);
            command.Parameters.AddWithValue("@TotalErrors", state.TotalErrors);
            command.Parameters.AddWithValue(
                "@Statistics",
                JsonConvert.SerializeObject(state.Statistics, Formatting.Indented));
            command.Parameters.AddWithValue("@Status", (int)state.Status);
            command.Parameters.AddWithValue(
                "@Configuration",
                state.Configuration != null ? JsonConvert.SerializeObject(state.Configuration) : (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 异步根据JobID获取爬取状态.
        /// </summary>
        /// <param name="jobId">作业ID.</param>
        /// <returns>爬取状态.</returns>
        public async Task<CrawlState?> GetCrawlStateAsync(string? jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                return null;
            }

            var command = new SQLiteCommand(
                "SELECT * FROM CrawlStates WHERE JobId = @JobId",
                this.connection);

            command.Parameters.AddWithValue("@JobId", jobId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CrawlState
                {
                    JobId = reader["JobId"].ToString()!,
                    StartTime = Convert.ToDateTime(reader["StartTime"]),
                    EndTime = reader["EndTime"] == DBNull.Value ? null : Convert.ToDateTime(reader["EndTime"]),
                    TotalUrlsDiscovered = Convert.ToInt32(reader["TotalUrlsDiscovered"]),
                    TotalUrlsProcessed = Convert.ToInt32(reader["TotalUrlsProcessed"]),
                    TotalErrors = Convert.ToInt32(reader["TotalErrors"]),
                    Statistics = JsonConvert.DeserializeObject<CrawlStatistics>(
                        reader["Statistics"]?.ToString() ?? "{}"),
                    Status = (CrawlerStatus)Convert.ToInt32(reader["Status"]),
                };
            }

            return null;
        }

        /// <summary>
        /// 异步保存URL状态.
        /// </summary>
        /// <param name="state">URL状态.</param>
        /// <returns>任务.</returns>
        public async Task SaveUrlStateAsync(UrlState state)
        {
            await this.SaveUrlStateAsync(state, null);
        }

        /// <summary>
        /// 异步根据URL获取URL状态.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>URL状态.</returns>
        public async Task<UrlState?> GetUrlStateAsync(string url)
        {
            var command = new SQLiteCommand(
                "SELECT * FROM UrlStates WHERE Url = @Url",
                this.connection);

            command.Parameters.AddWithValue("@Url", url);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UrlState
                {
                    Url = reader["Url"].ToString()!,
                    DiscoveredAt = Convert.ToDateTime(reader["DiscoveredAt"]),
                    ProcessedAt = reader["ProcessedAt"] == DBNull.Value ? null : Convert.ToDateTime(reader["ProcessedAt"]),
                    StatusCode = Convert.ToInt32(reader["StatusCode"]),
                    ContentLength = Convert.ToInt64(reader["ContentLength"]),
                    ContentType = reader["ContentType"]?.ToString() ?? string.Empty,
                    DownloadTime = TimeSpan.FromMilliseconds(Convert.ToDouble(reader["DownloadTimeMs"])),
                    ErrorMessage = reader["ErrorMessage"]?.ToString() ?? string.Empty,
                    RetryCount = Convert.ToInt32(reader["RetryCount"]),
                };
            }

            return null;
        }

        // 新增方法：获取统计信息

        /// <summary>
        /// 异步获取爬取统计信息.
        /// </summary>
        /// <returns>爬取统计信息.</returns>
        public async Task<CrawlStatistics> GetStatisticsAsync()
        {
            var stats = new CrawlStatistics();

            // 获取基本统计
            var basicStatsCommand = new SQLiteCommand(
                @"SELECT 
                    COUNT(*) as TotalUrls,
                    SUM(CASE WHEN IsSuccess = 1 THEN 1 ELSE 0 END) as SuccessCount,
                    SUM(CASE WHEN IsSuccess = 0 THEN 1 ELSE 0 END) as ErrorCount,
                    AVG(DownloadTimeMs) as AvgDownloadTime,
                    SUM(LENGTH(RawData)) as TotalDownloadSize
                  FROM CrawlResults",
                this.connection);

            using (var reader = await basicStatsCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    stats.TotalUrlsProcessed = Convert.ToInt32(reader["TotalUrls"]);
                    stats.SuccessCount = Convert.ToInt32(reader["SuccessCount"]);
                    stats.ErrorCount = Convert.ToInt32(reader["ErrorCount"]);
                    stats.AverageDownloadTimeMs = reader["AvgDownloadTime"] == DBNull.Value ? 0 : Convert.ToDouble(reader["AvgDownloadTime"]);
                    stats.TotalDownloadSize = reader["TotalDownloadSize"] == DBNull.Value ? 0 : Convert.ToInt64(reader["TotalDownloadSize"]);
                }
            }

            // 获取域名统计
            var domainStatsCommand = new SQLiteCommand(
                @"SELECT 
                    substr(Url, 1, instr(substr(Url, 9), '/') + 8) as Domain,
                    COUNT(*) as UrlsProcessed,
                    SUM(CASE WHEN IsSuccess = 1 THEN 1 ELSE 0 END) as SuccessCount,
                    SUM(CASE WHEN IsSuccess = 0 THEN 1 ELSE 0 END) as ErrorCount,
                    AVG(DownloadTimeMs) as AvgDownloadTime,
                    SUM(LENGTH(RawData)) as TotalDownloadSize
                  FROM CrawlResults 
                  GROUP BY Domain",
                this.connection);

            using (var reader = await domainStatsCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var domain = reader["Domain"].ToString();
                    if (string.IsNullOrEmpty(domain))
                    {
                        continue;
                    }

                    stats.DomainStats[domain] = new DomainStatistics
                    {
                        UrlsProcessed = Convert.ToInt32(reader["UrlsProcessed"]),
                        SuccessCount = Convert.ToInt32(reader["SuccessCount"]),
                        ErrorCount = Convert.ToInt32(reader["ErrorCount"]),
                        AverageDownloadTimeMs = reader["AvgDownloadTime"] == DBNull.Value ? 0 : Convert.ToDouble(reader["AvgDownloadTime"]),
                        TotalDownloadSize = reader["TotalDownloadSize"] == DBNull.Value ? 0 : Convert.ToInt64(reader["TotalDownloadSize"]),
                    };
                }
            }

            stats.LastUpdateTime = DateTime.UtcNow;
            return stats;
        }

        // 新增方法：清空所有数据

        /// <summary>
        /// 异步清空所有数据.
        /// </summary>
        /// <returns>任务.</returns>
        public async Task ClearAllAsync()
        {
            using var transaction = this.connection.BeginTransaction();

            try
            {
                var tables = new[] { "CrawlResults", "CrawlStates", "UrlStates", "ExtractedData", "Links", "Images", "Metadata" };

                foreach (var table in tables)
                {
                    var command = new SQLiteCommand($"DELETE FROM {table}", this.connection, transaction);
                    await command.ExecuteNonQueryAsync();
                }

                // 重置自增ID
                var resetCommand = new SQLiteCommand("DELETE FROM sqlite_sequence", this.connection, transaction);
                await resetCommand.ExecuteNonQueryAsync();

                transaction.Commit();
                this.logger.LogInformation("Cleared all data from database");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                this.logger.LogError(ex, "Failed to clear database");
                throw;
            }
        }

        // 新增方法：备份数据库

        /// <summary>
        /// 异步备份数据库.
        /// </summary>
        /// <param name="backupPath">备份路径.</param>
        /// <returns>任务.</returns>
        public async Task BackupAsync(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                using var backupConnection = new SQLiteConnection($"Data Source={backupPath};Version=3;");
                await backupConnection.OpenAsync();

                this.connection.BackupDatabase(backupConnection, "main", "main", -1, null, 0);

                this.logger.LogInformation("Database backed up to: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to backup database to {BackupPath}", backupPath);
                throw;
            }
        }

        /// <summary>
        /// 释放资源.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 异步关闭存储.
        /// </summary>
        /// <returns>任务.</returns>
        public Task ShutdownAsync()
        {
            this.Dispose();
            this.logger.LogInformation("SQLite storage shutdown");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 释放资源.
        /// </summary>
        /// <param name="disposing">是否释放托管资源.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.connection?.Close();
                    this.connection?.Dispose();
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// 异步保存URL状态.
        /// </summary>
        /// <param name="state">URL状态.</param>
        /// <param name="transaction">SQLite事务.</param>
        /// <returns>任务.</returns>
        private async Task SaveUrlStateAsync(UrlState state, SQLiteTransaction? transaction)
        {
            var commandText = @"INSERT OR REPLACE INTO UrlStates 
                              (Url, DiscoveredAt, ProcessedAt, StatusCode, ContentLength, ContentType, DownloadTimeMs, ErrorMessage, RetryCount)
                              VALUES (@Url, @DiscoveredAt, @ProcessedAt, @StatusCode, @ContentLength, @ContentType, @DownloadTimeMs, @ErrorMessage, @RetryCount)";

            SQLiteCommand command;
            if (transaction != null)
            {
                command = new SQLiteCommand(commandText, this.connection, transaction);
            }
            else
            {
                command = new SQLiteCommand(commandText, this.connection);
            }

            command.Parameters.AddWithValue("@Url", state.Url);
            command.Parameters.AddWithValue("@DiscoveredAt", state.DiscoveredAt);
            command.Parameters.AddWithValue("@ProcessedAt", state.ProcessedAt ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@StatusCode", state.StatusCode);
            command.Parameters.AddWithValue("@ContentLength", state.ContentLength);
            command.Parameters.AddWithValue("@ContentType", state.ContentType ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@DownloadTimeMs", state.DownloadTime.TotalMilliseconds);
            command.Parameters.AddWithValue("@ErrorMessage", state.ErrorMessage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@RetryCount", state.RetryCount);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 创建数据库表.
        /// </summary>
        /// <returns>任务.</returns>
        private async Task CreateTablesAsync()
        {
            var commands = new[]
            {
                // CrawlResults 表 - 存储爬取结果
                @"CREATE TABLE IF NOT EXISTS CrawlResults (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Url TEXT UNIQUE NOT NULL,
                    Content TEXT,
                    RawData BLOB,
                    ContentType TEXT,
                    StatusCode INTEGER,
                    DownloadTimeMs INTEGER,
                    ProcessedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    Depth INTEGER DEFAULT 0,
                    Referrer TEXT,
                    Title TEXT,
                    TextContent TEXT,
                    ErrorMessage TEXT,
                    IsSuccess INTEGER DEFAULT 1
                )",

                // CrawlStates 表 - 存储爬虫状态
                @"CREATE TABLE IF NOT EXISTS CrawlStates (
                    JobId TEXT PRIMARY KEY,
                    StartTime DATETIME NOT NULL,
                    EndTime DATETIME,
                    TotalUrlsDiscovered INTEGER DEFAULT 0,
                    TotalUrlsProcessed INTEGER DEFAULT 0,
                    TotalErrors INTEGER DEFAULT 0,
                    Statistics TEXT,
                    Status INTEGER NOT NULL,
                    Configuration TEXT
                )",

                // UrlStates 表 - 存储URL状态
                @"CREATE TABLE IF NOT EXISTS UrlStates (
                    Url TEXT PRIMARY KEY,
                    DiscoveredAt DATETIME NOT NULL,
                    ProcessedAt DATETIME,
                    StatusCode INTEGER,
                    ContentLength INTEGER,
                    ContentType TEXT,
                    DownloadTimeMs INTEGER,
                    ErrorMessage TEXT,
                    RetryCount INTEGER DEFAULT 0
                )",

                // ExtractedData 表 - 存储提取的数据
                @"CREATE TABLE IF NOT EXISTS ExtractedData (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Url TEXT NOT NULL,
                    DataKey TEXT NOT NULL,
                    DataValue TEXT,
                    DataType TEXT,
                    FOREIGN KEY (Url) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",

                // Links 表 - 存储链接关系
                @"CREATE TABLE IF NOT EXISTS Links (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SourceUrl TEXT NOT NULL,
                    TargetUrl TEXT NOT NULL,
                    LinkText TEXT,
                    FOREIGN KEY (SourceUrl) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",

                // Images 表 - 存储图片信息
                @"CREATE TABLE IF NOT EXISTS Images (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Url TEXT NOT NULL,
                    ImageUrl TEXT NOT NULL,
                    AltText TEXT,
                    FOREIGN KEY (Url) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",

                // Metadata 表 - 存储自定义元数据
                @"CREATE TABLE IF NOT EXISTS Metadata (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Url TEXT NOT NULL,
                    MetaKey TEXT NOT NULL,
                    MetaValue TEXT,
                    FOREIGN KEY (Url) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",
            };

            foreach (var commandText in commands)
            {
                try
                {
                    using var command = new SQLiteCommand(commandText, this.connection);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to execute SQL command: {Command}", commandText);
                }
            }

            await this.CreateIndexesAsync();
            await this.EnableForeignKeysAsync();
        }

        /// <summary>
        /// 创建数据库索引.
        /// </summary>
        /// <returns>任务.</returns>
        private async Task CreateIndexesAsync()
        {
            var indexCommands = new[]
            {
                "CREATE INDEX IF NOT EXISTS IX_CrawlResults_Url ON CrawlResults(Url)",
                "CREATE INDEX IF NOT EXISTS IX_CrawlResults_ProcessedAt ON CrawlResults(ProcessedAt)",
                "CREATE INDEX IF NOT EXISTS IX_CrawlResults_Depth ON CrawlResults(Depth)",
                "CREATE INDEX IF NOT EXISTS IX_CrawlResults_StatusCode ON CrawlResults(StatusCode)",
                "CREATE INDEX IF NOT EXISTS IX_UrlStates_Url ON UrlStates(Url)",
                "CREATE INDEX IF NOT EXISTS IX_UrlStates_ProcessedAt ON UrlStates(ProcessedAt)",
                "CREATE INDEX IF NOT EXISTS IX_Links_SourceUrl ON Links(SourceUrl)",
                "CREATE INDEX IF NOT EXISTS IX_Links_TargetUrl ON Links(TargetUrl)",
                "CREATE INDEX IF NOT EXISTS IX_ExtractedData_Url ON ExtractedData(Url)",
                "CREATE INDEX IF NOT EXISTS IX_ExtractedData_Key ON ExtractedData(DataKey)",
                "CREATE INDEX IF NOT EXISTS IX_Images_Url ON Images(Url)",
                "CREATE INDEX IF NOT EXISTS IX_Metadata_Url ON Metadata(Url)",
            };

            foreach (var indexCommand in indexCommands)
            {
                try
                {
                    using var command = new SQLiteCommand(indexCommand, this.connection);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to create index: {Index}", indexCommand);
                }
            }
        }

        /// <summary>
        /// 启用外键约束.
        /// </summary>
        /// <returns>任务.</returns>
        private async Task EnableForeignKeysAsync()
        {
            try
            {
                using var command = new SQLiteCommand("PRAGMA foreign_keys = ON", this.connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to enable foreign keys");
            }
        }

        /// <summary>
        /// 异步保存提取的数据.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <param name="extractedData">提取的数据.</param>
        /// <param name="transaction">数据库事务.</param>
        /// <returns>任务.</returns>
        private async Task SaveExtractedDataAsync(string url, Dictionary<string, object> extractedData, SQLiteTransaction transaction)
        {
            // 先删除旧的提取数据
            var deleteCommand = new SQLiteCommand("DELETE FROM ExtractedData WHERE Url = @Url", this.connection, transaction);
            deleteCommand.Parameters.AddWithValue("@Url", url);
            await deleteCommand.ExecuteNonQueryAsync();

            // 插入新的提取数据
            foreach (var data in extractedData)
            {
                var insertCommand = new SQLiteCommand(
                    @"INSERT INTO ExtractedData (Url, DataKey, DataValue, DataType)
                      VALUES (@Url, @Key, @Value, @Type)",
                    this.connection,
                    transaction);

                insertCommand.Parameters.AddWithValue("@Url", url);
                insertCommand.Parameters.AddWithValue("@Key", data.Key);

                var value = data.Value?.ToString() ?? string.Empty;
                insertCommand.Parameters.AddWithValue("@Value", value);
                insertCommand.Parameters.AddWithValue("@Type", data.Value?.GetType().Name ?? "String");

                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// 异步保存链接.
        /// </summary>
        /// <param name="sourceUrl">源URL.</param>
        /// <param name="links">链接列表.</param>
        /// <param name="transaction">数据库事务.</param>
        /// <returns>任务.</returns>
        private async Task SaveLinksAsync(string sourceUrl, List<string> links, SQLiteTransaction transaction)
        {
            // 先删除旧的链接
            var deleteCommand = new SQLiteCommand("DELETE FROM Links WHERE SourceUrl = @SourceUrl", this.connection, transaction);
            deleteCommand.Parameters.AddWithValue("@SourceUrl", sourceUrl);
            await deleteCommand.ExecuteNonQueryAsync();

            // 插入新的链接
            foreach (var link in links)
            {
                var insertCommand = new SQLiteCommand(
                    @"INSERT OR IGNORE INTO Links (SourceUrl, TargetUrl)
                      VALUES (@SourceUrl, @TargetUrl)",
                    this.connection,
                    transaction);

                insertCommand.Parameters.AddWithValue("@SourceUrl", sourceUrl);
                insertCommand.Parameters.AddWithValue("@TargetUrl", link);

                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// 异步保存图片.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <param name="images">图片列表.</param>
        /// <param name="transaction">数据库事务.</param>
        /// <returns>任务.</returns>
        private async Task SaveImagesAsync(string url, List<string> images, SQLiteTransaction transaction)
        {
            // 先删除旧的图片
            var deleteCommand = new SQLiteCommand("DELETE FROM Images WHERE Url = @Url", this.connection, transaction);
            deleteCommand.Parameters.AddWithValue("@Url", url);
            await deleteCommand.ExecuteNonQueryAsync();

            // 插入新的图片
            foreach (var imageUrl in images)
            {
                var insertCommand = new SQLiteCommand(
                    @"INSERT INTO Images (Url, ImageUrl)
                      VALUES (@Url, @ImageUrl)",
                    this.connection,
                    transaction);

                insertCommand.Parameters.AddWithValue("@Url", url);
                insertCommand.Parameters.AddWithValue("@ImageUrl", imageUrl);

                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// 异步读取爬取结果.
        /// </summary>
        /// <param name="reader">SQLite数据读取器.</param>
        /// <returns>爬取结果.</returns>
        private async Task<CrawlResult?> ReadCrawlResultAsync(SQLiteDataReader reader)
        {
            try
            {
                var url = reader["Url"].ToString();
                if (url == null)
                {
                    return null;
                }

                var result = new CrawlResult
                {
                    Request = new CrawlRequest
                    {
                        Url = url ?? string.Empty,
                        Depth = Convert.ToInt32(reader["Depth"]),
                        Referrer = reader["Referrer"]?.ToString() ?? string.Empty,
                    },
                    DownloadResult = new DownloadResult
                    {
                        Url = url ?? string.Empty,
                        Content = reader["Content"]?.ToString() ?? string.Empty,
                        RawData = reader["RawData"] as byte[] ?? [],
                        ContentType = reader["ContentType"]?.ToString() ?? string.Empty,
                        StatusCode = Convert.ToInt32(reader["StatusCode"]),
                        DownloadTimeMs = Convert.ToInt64(reader["DownloadTimeMs"]),
                        ErrorMessage = reader["ErrorMessage"]?.ToString() ?? string.Empty,
                        IsSuccess = Convert.ToInt32(reader["IsSuccess"]) == 1,
                    },
                    ProcessedAt = Convert.ToDateTime(reader["ProcessedAt"]),
                    ParseResult = new ParseResult
                    {
                        Title = reader["Title"]?.ToString() ?? string.Empty,
                        TextContent = reader["TextContent"]?.ToString() ?? string.Empty,
                    },
                };

                // 加载提取的数据
                result.ParseResult.ExtractedData = await this.GetExtractedDataAsync(url!);

                // 加载链接
                result.ParseResult.Links = await this.GetLinksAsync(url!);

                // 加载图片
                result.ParseResult.Images = await this.GetImagesAsync(url!);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to read crawl result from database");
                return null;
            }
        }

        /// <summary>
        /// 异步根据URL获取提取的数据.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>提取的数据字典.</returns>
        private async Task<Dictionary<string, object>> GetExtractedDataAsync(string url)
        {
            var data = new Dictionary<string, object>();

            var command = new SQLiteCommand(
                "SELECT DataKey, DataValue FROM ExtractedData WHERE Url = @Url",
                this.connection);

            command.Parameters.AddWithValue("@Url", url);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var key = reader["DataKey"].ToString();
                if (key == null)
                {
                    continue;
                }

                var value = reader["DataValue"]?.ToString();

                data[key!] = value ?? string.Empty;
            }

            return data;
        }

        /// <summary>
        /// 异步根据URL获取链接.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>链接列表.</returns>
        private async Task<List<string>> GetLinksAsync(string url)
        {
            var links = new List<string>();

            var command = new SQLiteCommand(
                "SELECT TargetUrl FROM Links WHERE SourceUrl = @SourceUrl",
                this.connection);

            command.Parameters.AddWithValue("@SourceUrl", url);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var link = reader["TargetUrl"].ToString();
                if (link == null)
                {
                    continue;
                }

                links.Add(link);
            }

            return links;
        }

        /// <summary>
        /// 异步根据URL获取图片.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>图片列表.</returns>
        private async Task<List<string>> GetImagesAsync(string url)
        {
            var images = new List<string>();

            var command = new SQLiteCommand(
                "SELECT ImageUrl FROM Images WHERE Url = @Url",
                this.connection);

            command.Parameters.AddWithValue("@Url", url);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var imageUrl = reader["ImageUrl"].ToString();
                if (imageUrl == null)
                {
                    continue;
                }

                images.Add(imageUrl);
            }

            return images;
        }

        /// <summary>
        /// SQLite参数集合包装器，用于简化参数设置.
        /// </summary>
        private class SQLiteParameterCollectionWrapper
        {
            private readonly SQLiteParameterCollection parameters;

            /// <summary>
            /// 初始化 <see cref="SQLiteParameterCollectionWrapper"/> 类的新实例.
            /// </summary>
            /// <param name="parameters">SQLite参数集合.</param>
            public SQLiteParameterCollectionWrapper(SQLiteParameterCollection parameters)
            {
                this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters), "参数集合不能为空");
            }

            public SQLiteParameter this[string name]
            {
                get { return this.parameters[name]; }
            }
        }
    }
}