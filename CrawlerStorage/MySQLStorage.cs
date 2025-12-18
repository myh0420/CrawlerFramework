// <copyright file="MySQLStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerStorage
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;
    using CrawlerFramework.CrawlerEntity.Enums;
    using CrawlerFramework.CrawlerEntity.Models;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;
    using MySql.Data.MySqlClient;
    using Newtonsoft.Json;

    /// <summary>
    /// MySQL 存储实现，用于存储爬取结果和元数据.
    /// </summary>
    public class MySQLStorage : IStorageProvider, IMetadataStore, IDisposable
    {
        /// <summary>
        /// MySQL 数据库连接.
        /// </summary>
        private readonly MySqlConnection connection;

        /// <summary>
        /// 日志记录器.
        /// </summary>
        private readonly ILogger<MySQLStorage> logger;

        /// <summary>
        /// 连接字符串.
        /// </summary>
        private readonly string connectionString;

        /// <summary>
        /// 是否已释放.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MySQLStorage"/> class.
        /// 构造函数.
        /// </summary>
        /// <param name="connectionString">数据库连接字符串. 默认为 "Server=localhost;Database=crawler;Uid=root;Pwd=root;".</param>
        /// <param name="logger">日志记录器. 默认为新的 Logger&lt;MySQLStorage&gt; 实例.</param>
        public MySQLStorage(string? connectionString, ILogger<MySQLStorage>? logger)
        {
            this.connectionString = connectionString ?? "Server=localhost;Database=crawler;Uid=root;Pwd=root;";
            this.logger = logger ?? new Logger<MySQLStorage>(new LoggerFactory());
            this.connection = new MySqlConnection(this.connectionString);
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
                this.logger.LogInformation("MySQL storage initialized with connection string: {ConnectionString}", this.connectionString);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize MySQL storage");
                throw;
            }
        }

        /// <summary>
        /// 异步保存爬取结果.
        /// </summary>
        /// <param name="result">爬取结果.</param>
        /// <returns>任务. 完成时表示爬取结果已保存.</returns>
        public async Task SaveAsync(CrawlResult result)
        {
            await this.BatchSaveAsync([result]);
        }

        /// <summary>
        /// 异步批量保存爬取结果.
        /// </summary>
        /// <param name="results">爬取结果列表.</param>
        /// <returns>任务. 完成时表示爬取结果列表已保存.</returns>
        public async Task BatchSaveAsync(List<CrawlResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return;
            }

            using var transaction = await this.connection.BeginTransactionAsync();

            try
            {
                // 批量插入CrawlResults
                var crawlResultsCommand = new MySqlCommand(
                    @"INSERT INTO CrawlResults 
                      (Url, Content, RawData, ContentType, StatusCode, DownloadTimeMs, Depth, Referrer, Title, TextContent, ErrorMessage, IsSuccess)
                      VALUES (@Url, @Content, @RawData, @ContentType, @StatusCode, @DownloadTimeMs, @Depth, @Referrer, @Title, @TextContent, @ErrorMessage, @IsSuccess)
                      ON DUPLICATE KEY UPDATE
                      Content = VALUES(Content),
                      RawData = VALUES(RawData),
                      ContentType = VALUES(ContentType),
                      StatusCode = VALUES(StatusCode),
                      DownloadTimeMs = VALUES(DownloadTimeMs),
                      Depth = VALUES(Depth),
                      Referrer = VALUES(Referrer),
                      Title = VALUES(Title),
                      TextContent = VALUES(TextContent),
                      ErrorMessage = VALUES(ErrorMessage),
                      IsSuccess = VALUES(IsSuccess)",
                    this.connection,
                    transaction as MySqlTransaction);

                // 批量插入ExtractedData
                var extractedDataCommand = new MySqlCommand(
                    @"INSERT INTO ExtractedData (Url, DataKey, DataValue, DataType)
                      VALUES (@Url, @Key, @Value, @Type)",
                    this.connection,
                    transaction as MySqlTransaction);

                // 批量插入Links
                var linksCommand = new MySqlCommand(
                    @"INSERT IGNORE INTO Links (SourceUrl, TargetUrl)
                      VALUES (@SourceUrl, @TargetUrl)",
                    this.connection,
                    transaction as MySqlTransaction);

                // 批量插入Images
                var imagesCommand = new MySqlCommand(
                    @"INSERT INTO Images (Url, ImageUrl)
                      VALUES (@Url, @ImageUrl)",
                    this.connection,
                    transaction as MySqlTransaction);

                // 批量插入UrlStates
                var urlStatesCommand = new MySqlCommand(
                    @"INSERT INTO UrlStates 
                      (Url, DiscoveredAt, ProcessedAt, StatusCode, ContentLength, ContentType, DownloadTimeMs, ErrorMessage, RetryCount)
                      VALUES (@Url, @DiscoveredAt, @ProcessedAt, @StatusCode, @ContentLength, @ContentType, @DownloadTimeMs, @ErrorMessage, @RetryCount)
                      ON DUPLICATE KEY UPDATE
                      DiscoveredAt = VALUES(DiscoveredAt),
                      ProcessedAt = VALUES(ProcessedAt),
                      StatusCode = VALUES(StatusCode),
                      ContentLength = VALUES(ContentLength),
                      ContentType = VALUES(ContentType),
                      DownloadTimeMs = VALUES(DownloadTimeMs),
                      ErrorMessage = VALUES(ErrorMessage),
                      RetryCount = VALUES(RetryCount)",
                    this.connection,
                    transaction as MySqlTransaction);

                foreach (var result in results)
                {
                    // 插入CrawlResults
                    crawlResultsCommand.Parameters.Clear();
                    crawlResultsCommand.Parameters.AddWithValue("@Url", result.Request.Url);
                    crawlResultsCommand.Parameters.AddWithValue("@Content", result.DownloadResult.Content);
                    crawlResultsCommand.Parameters.AddWithValue("@RawData", result.DownloadResult.RawData);
                    crawlResultsCommand.Parameters.AddWithValue("@ContentType", result.DownloadResult.ContentType);
                    crawlResultsCommand.Parameters.AddWithValue("@StatusCode", result.DownloadResult.StatusCode);
                    crawlResultsCommand.Parameters.AddWithValue("@DownloadTimeMs", result.DownloadResult.DownloadTimeMs);
                    crawlResultsCommand.Parameters.AddWithValue("@Depth", result.Request.Depth);
                    crawlResultsCommand.Parameters.AddWithValue("@Referrer", string.IsNullOrEmpty(result.Request.Referrer) ? DBNull.Value : (object)result.Request.Referrer);
                    crawlResultsCommand.Parameters.AddWithValue("@Title", result.ParseResult?.Title == null ? DBNull.Value : (object)result.ParseResult.Title);
                    crawlResultsCommand.Parameters.AddWithValue("@TextContent", result.ParseResult?.TextContent == null ? DBNull.Value : (object)result.ParseResult.TextContent);
                    crawlResultsCommand.Parameters.AddWithValue("@ErrorMessage", string.IsNullOrEmpty(result.DownloadResult.ErrorMessage) ? DBNull.Value : (object)result.DownloadResult.ErrorMessage);
                    crawlResultsCommand.Parameters.AddWithValue("@IsSuccess", result.DownloadResult.IsSuccess);
                    await crawlResultsCommand.ExecuteNonQueryAsync();

                    // 插入ExtractedData
                    if (result.ParseResult?.ExtractedData != null)
                    {
                        // 先删除旧数据
                        var deleteExtractedDataCommand = new MySqlCommand("DELETE FROM ExtractedData WHERE Url = @Url", this.connection, transaction as MySqlTransaction);
                        deleteExtractedDataCommand.Parameters.AddWithValue("@Url", result.Request.Url);
                        await deleteExtractedDataCommand.ExecuteNonQueryAsync();

                        foreach (var data in result.ParseResult.ExtractedData)
                        {
                            extractedDataCommand.Parameters.Clear();
                            extractedDataCommand.Parameters.AddWithValue("@Url", result.Request.Url);
                            extractedDataCommand.Parameters.AddWithValue("@Key", data.Key);
                            extractedDataCommand.Parameters.AddWithValue("@Value", data.Value?.ToString() ?? string.Empty);
                            extractedDataCommand.Parameters.AddWithValue("@Type", data.Value?.GetType().Name ?? "String");
                            await extractedDataCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // 插入Links
                    if (result.ParseResult?.Links != null)
                    {
                        // 先删除旧数据
                        var deleteLinksCommand = new MySqlCommand("DELETE FROM Links WHERE SourceUrl = @SourceUrl", this.connection, transaction as MySqlTransaction);
                        deleteLinksCommand.Parameters.AddWithValue("@SourceUrl", result.Request.Url);
                        await deleteLinksCommand.ExecuteNonQueryAsync();

                        foreach (var link in result.ParseResult.Links)
                        {
                            linksCommand.Parameters.Clear();
                            linksCommand.Parameters.AddWithValue("@SourceUrl", result.Request.Url);
                            linksCommand.Parameters.AddWithValue("@TargetUrl", link);
                            await linksCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // 插入Images
                    if (result.ParseResult?.Images != null)
                    {
                        // 先删除旧数据
                        var deleteImagesCommand = new MySqlCommand("DELETE FROM Images WHERE Url = @Url", this.connection, transaction as MySqlTransaction);
                        deleteImagesCommand.Parameters.AddWithValue("@Url", result.Request.Url);
                        await deleteImagesCommand.ExecuteNonQueryAsync();

                        foreach (var imageUrl in result.ParseResult.Images)
                        {
                            imagesCommand.Parameters.Clear();
                            imagesCommand.Parameters.AddWithValue("@Url", result.Request.Url);
                            imagesCommand.Parameters.AddWithValue("@ImageUrl", imageUrl);
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

                    urlStatesCommand.Parameters.Clear();
                    urlStatesCommand.Parameters.AddWithValue("@Url", urlState.Url);
                    urlStatesCommand.Parameters.AddWithValue("@DiscoveredAt", urlState.DiscoveredAt);
                    urlStatesCommand.Parameters.AddWithValue("@ProcessedAt", urlState.ProcessedAt.HasValue ? (object)urlState.ProcessedAt.Value : DBNull.Value);
                    urlStatesCommand.Parameters.AddWithValue("@StatusCode", urlState.StatusCode);
                    urlStatesCommand.Parameters.AddWithValue("@ContentLength", urlState.ContentLength);
                    urlStatesCommand.Parameters.AddWithValue("@ContentType", string.IsNullOrEmpty(urlState.ContentType) ? DBNull.Value : (object)urlState.ContentType);
                    urlStatesCommand.Parameters.AddWithValue("@DownloadTimeMs", urlState.DownloadTime.TotalMilliseconds);
                    urlStatesCommand.Parameters.AddWithValue("@ErrorMessage", string.IsNullOrEmpty(urlState.ErrorMessage) ? DBNull.Value : (object)urlState.ErrorMessage);
                    urlStatesCommand.Parameters.AddWithValue("@RetryCount", urlState.RetryCount);
                    await urlStatesCommand.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                this.logger.LogDebug("Batch saved {Count} crawl results to MySQL", results.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                this.logger.LogError(ex, "Failed to batch save crawl results");
                throw;
            }
        }

        /// <summary>
        /// 异步获取指定域名的爬取结果.
        /// </summary>
        /// <param name="domain">域名.</param>
        /// <param name="limit">最大返回数量，默认值为100.</param>
        /// <returns>爬取结果列表.</returns>
        public async Task<IEnumerable<CrawlResult>> GetByDomainAsync(string domain, int limit = 100)
        {
            var results = new List<CrawlResult>();
            var command = new MySqlCommand(
                "SELECT * FROM CrawlResults WHERE Url LIKE @Domain LIMIT @Limit",
                this.connection);
            command.Parameters.AddWithValue("@Domain", $"%{domain}%");
            command.Parameters.AddWithValue("@Limit", limit);

            using var reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var result = MapCrawlResult(reader);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// 异步获取指定URL的爬取结果.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>爬取结果.</returns>
        public async Task<CrawlResult?> GetByUrlAsync(string url)
        {
            var command = new MySqlCommand("SELECT * FROM CrawlResults WHERE Url = @Url", this.connection);
            command.Parameters.AddWithValue("@Url", url);

            using var reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapCrawlResult(reader);
            }

            return null;
        }

        /// <summary>
        /// 异步获取总记录数.
        /// </summary>
        /// <returns>总记录数.</returns>
        public async Task<long> GetTotalCountAsync()
        {
            var command = new MySqlCommand(
                "SELECT COUNT(*) FROM CrawlResults",
                this.connection);

            return (long)(await command.ExecuteScalarAsync() ?? 0);
        }

        /// <summary>
        /// 异步删除指定URL的爬取结果.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>已完成任务.</returns>
        public async Task<bool> DeleteAsync(string url)
        {
            using var transaction = await this.connection.BeginTransactionAsync();

            try
            {
                // 删除关联数据
                var deleteLinksCommand = new MySqlCommand("DELETE FROM Links WHERE SourceUrl = @Url OR TargetUrl = @Url", this.connection, transaction as MySqlTransaction);
                deleteLinksCommand.Parameters.AddWithValue("@Url", url);
                await deleteLinksCommand.ExecuteNonQueryAsync();

                var deleteExtractedDataCommand = new MySqlCommand("DELETE FROM ExtractedData WHERE Url = @Url", this.connection, transaction as MySqlTransaction);
                deleteExtractedDataCommand.Parameters.AddWithValue("@Url", url);
                await deleteExtractedDataCommand.ExecuteNonQueryAsync();

                var deleteImagesCommand = new MySqlCommand("DELETE FROM Images WHERE Url = @Url", this.connection, transaction as MySqlTransaction);
                deleteImagesCommand.Parameters.AddWithValue("@Url", url);
                await deleteImagesCommand.ExecuteNonQueryAsync();

                var deleteUrlStatesCommand = new MySqlCommand("DELETE FROM UrlStates WHERE Url = @Url", this.connection, transaction as MySqlTransaction);
                deleteUrlStatesCommand.Parameters.AddWithValue("@Url", url);
                await deleteUrlStatesCommand.ExecuteNonQueryAsync();

                // 删除主数据
                var deleteCrawlResultCommand = new MySqlCommand("DELETE FROM CrawlResults WHERE Url = @Url", this.connection, transaction as MySqlTransaction);
                deleteCrawlResultCommand.Parameters.AddWithValue("@Url", url);
                var rowsAffected = await deleteCrawlResultCommand.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                this.logger.LogError(ex, "Failed to delete crawl result for URL: {Url}", url);
                return false;
            }
        }

        /// <summary>
        /// 异步获取爬取统计信息.
        /// </summary>
        /// <returns>爬取统计信息.</returns>
        public async Task<CrawlStatistics> GetStatisticsAsync()
        {
            var statistics = new CrawlStatistics();

            // 获取总记录数
            var totalCountCommand = new MySqlCommand("SELECT COUNT(*) FROM CrawlResults", this.connection);
            statistics.TotalUrlsProcessed = Convert.ToInt32(await totalCountCommand.ExecuteScalarAsync());

            // 获取成功记录数
            var successCountCommand = new MySqlCommand("SELECT COUNT(*) FROM CrawlResults WHERE IsSuccess = 1", this.connection);
            statistics.SuccessCount = Convert.ToInt32(await successCountCommand.ExecuteScalarAsync());

            // 获取失败记录数
            var errorCountCommand = new MySqlCommand("SELECT COUNT(*) FROM CrawlResults WHERE IsSuccess = 0", this.connection);
            statistics.ErrorCount = Convert.ToInt32(await errorCountCommand.ExecuteScalarAsync());

            // 获取平均下载时间
            var avgDownloadTimeCommand = new MySqlCommand("SELECT AVG(DownloadTimeMs) FROM CrawlResults WHERE DownloadTimeMs > 0", this.connection);
            var avgDownloadTime = await avgDownloadTimeCommand.ExecuteScalarAsync();
            if (avgDownloadTime != DBNull.Value)
            {
                statistics.AverageDownloadTimeMs = Convert.ToDouble(avgDownloadTime);
            }

            return statistics;
        }

        /// <summary>
        /// 异步清除所有数据.
        /// </summary>
        /// <returns>已完成任务.</returns>
        public async Task ClearAllAsync()
        {
            using var transaction = await this.connection.BeginTransactionAsync();

            try
            {
                // 删除所有数据（按照外键关系顺序）
                var tables = new[] { "Links", "ExtractedData", "Images", "UrlStates", "CrawlResults", "CrawlStates" };
                foreach (var table in tables)
                {
                    var command = new MySqlCommand($"TRUNCATE TABLE {table}", this.connection, transaction as MySqlTransaction);
                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                this.logger.LogInformation("Cleared all data from MySQL storage");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                this.logger.LogError(ex, "Failed to clear all data");
                throw;
            }
        }

        /// <summary>
        /// 异步备份数据.
        /// </summary>
        /// <param name="backupPath">备份路径.</param>
        /// <returns>已完成任务.</returns>
        public async Task BackupAsync(string backupPath)
        {
            this.logger.LogWarning("MySQL backup functionality not implemented yet");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 异步保存爬取状态.
        /// </summary>
        /// <param name="state">爬取状态.</param>
        /// <returns>已完成任务.</returns>
        public async Task SaveCrawlStateAsync(CrawlState state)
        {
            var command = new MySqlCommand(
                @"INSERT INTO CrawlStates 
                  (JobId, StartTime, EndTime, TotalUrlsDiscovered, TotalUrlsProcessed, TotalErrors, Statistics, Status, Configuration)
                  VALUES (@JobId, @StartTime, @EndTime, @TotalUrlsDiscovered, @TotalUrlsProcessed, @TotalErrors, @Statistics, @Status, @Configuration)
                  ON DUPLICATE KEY UPDATE
                  StartTime = VALUES(StartTime),
                  EndTime = VALUES(EndTime),
                  TotalUrlsDiscovered = VALUES(TotalUrlsDiscovered),
                  TotalUrlsProcessed = VALUES(TotalUrlsProcessed),
                  TotalErrors = VALUES(TotalErrors),
                  Statistics = VALUES(Statistics),
                  Status = VALUES(Status),
                  Configuration = VALUES(Configuration)",
                this.connection);

            command.Parameters.AddWithValue("@JobId", state.JobId);
            command.Parameters.AddWithValue("@StartTime", state.StartTime);
            command.Parameters.AddWithValue("@EndTime", state.EndTime.HasValue ? (object)state.EndTime.Value : DBNull.Value);
            command.Parameters.AddWithValue("@TotalUrlsDiscovered", state.TotalUrlsDiscovered);
            command.Parameters.AddWithValue("@TotalUrlsProcessed", state.TotalUrlsProcessed);
            command.Parameters.AddWithValue("@TotalErrors", state.TotalErrors);
            command.Parameters.AddWithValue("@Statistics", JsonConvert.SerializeObject(state.Statistics));
            command.Parameters.AddWithValue("@Status", (int)state.Status);
            command.Parameters.AddWithValue("@Configuration", JsonConvert.SerializeObject(state.Configuration));

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 异步获取指定作业ID的爬取状态.
        /// </summary>
        /// <param name="jobId">作业ID.</param>
        /// <returns>爬取状态.</returns>
        public async Task<CrawlState?> GetCrawlStateAsync(string? jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                return null;
            }

            var command = new MySqlCommand(
                "SELECT * FROM CrawlStates WHERE JobId = @JobId",
                this.connection);
            command.Parameters.AddWithValue("@JobId", jobId);

            using var reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new CrawlState
                {
                    JobId = reader.GetString("JobId"),
                    StartTime = reader.GetDateTime("StartTime"),
                    EndTime = reader.IsDBNull("EndTime") ? null : reader.GetDateTime("EndTime"),
                    TotalUrlsDiscovered = reader.GetInt32("TotalUrlsDiscovered"),
                    TotalUrlsProcessed = reader.GetInt32("TotalUrlsProcessed"),
                    TotalErrors = reader.GetInt32("TotalErrors"),
                    Statistics = JsonConvert.DeserializeObject<CrawlStatistics>(reader.GetString("Statistics")),
                    Status = (CrawlerStatus)reader.GetInt32("Status"),
                    Configuration = JsonConvert.DeserializeObject<object>(reader.GetString("Configuration")) ?? new(),
                };
            }

            return null;
        }

        /// <summary>
        /// 异步保存URL状态.
        /// </summary>
        /// <param name="state">URL状态.</param>
        /// <returns>已完成任务.</returns>
        public async Task SaveUrlStateAsync(UrlState state)
        {
            var command = new MySqlCommand(
                @"INSERT INTO UrlStates 
                  (Url, DiscoveredAt, ProcessedAt, StatusCode, ContentLength, ContentType, DownloadTimeMs, ErrorMessage, RetryCount)
                  VALUES (@Url, @DiscoveredAt, @ProcessedAt, @StatusCode, @ContentLength, @ContentType, @DownloadTimeMs, @ErrorMessage, @RetryCount)
                  ON DUPLICATE KEY UPDATE
                  DiscoveredAt = VALUES(DiscoveredAt),
                  ProcessedAt = VALUES(ProcessedAt),
                  StatusCode = VALUES(StatusCode),
                  ContentLength = VALUES(ContentLength),
                  ContentType = VALUES(ContentType),
                  DownloadTimeMs = VALUES(DownloadTimeMs),
                  ErrorMessage = VALUES(ErrorMessage),
                  RetryCount = VALUES(RetryCount)",
                this.connection);

            command.Parameters.AddWithValue("@Url", state.Url);
            command.Parameters.AddWithValue("@DiscoveredAt", state.DiscoveredAt);
            command.Parameters.AddWithValue("@ProcessedAt", state.ProcessedAt.HasValue ? (object)state.ProcessedAt.Value : DBNull.Value);
            command.Parameters.AddWithValue("@StatusCode", state.StatusCode);
            command.Parameters.AddWithValue("@ContentLength", state.ContentLength);
            command.Parameters.AddWithValue("@ContentType", string.IsNullOrEmpty(state.ContentType) ? DBNull.Value : (object)state.ContentType);
            command.Parameters.AddWithValue("@DownloadTimeMs", state.DownloadTime.TotalMilliseconds);
            command.Parameters.AddWithValue("@ErrorMessage", string.IsNullOrEmpty(state.ErrorMessage) ? DBNull.Value : (object)state.ErrorMessage);
            command.Parameters.AddWithValue("@RetryCount", state.RetryCount);

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 异步获取指定URL的URL状态.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>URL状态.</returns>
        public async Task<UrlState?> GetUrlStateAsync(string url)
        {
            var command = new MySqlCommand(
                "SELECT * FROM UrlStates WHERE Url = @Url",
                this.connection);
            command.Parameters.AddWithValue("@Url", url);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new UrlState
                {
                    Url = reader.GetString("Url"),
                    DiscoveredAt = reader.GetDateTime("DiscoveredAt"),
                    ProcessedAt = reader.IsDBNull("ProcessedAt") ? null : reader.GetDateTime("ProcessedAt"),
                    StatusCode = reader.GetInt32("StatusCode"),
                    ContentLength = reader.GetInt64("ContentLength"),
                    ContentType = reader.IsDBNull("ContentType") ? string.Empty : reader.GetString("ContentType"),
                    DownloadTime = TimeSpan.FromMilliseconds(reader.GetInt32("DownloadTimeMs")),
                    ErrorMessage = reader.IsDBNull("ErrorMessage") ? string.Empty : reader.GetString("ErrorMessage"),
                    RetryCount = reader.GetInt32("RetryCount"),
                };
            }

            return null;
        }

        /// <summary>
        /// 关闭数据库连接.
        /// </summary>
        /// <returns>任务.</returns>
        public async Task ShutdownAsync()
        {
            try
            {
                if (this.connection.State == ConnectionState.Open)
                {
                    await this.connection.CloseAsync();
                    this.logger.LogInformation("MySQL storage shutdown");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to shutdown MySQL storage");
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
        /// 释放资源.
        /// </summary>
        /// <param name="disposing">是否正在释放.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.connection.Dispose();
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// 将DataReader映射到CrawlResult对象.
        /// </summary>
        /// <param name="reader">DataReader.</param>
        /// <returns>CrawlResult对象.</returns>
        private static CrawlResult MapCrawlResult(MySqlDataReader reader)
        {
            return new CrawlResult
            {
                Request = new CrawlRequest
                {
                    Url = reader.GetString("Url"),
                    Depth = reader.GetInt32("Depth"),
                    Referrer = reader.IsDBNull("Referrer") ? string.Empty : reader.GetString("Referrer"),
                },
                DownloadResult = new DownloadResult
                {
                    Content = reader.IsDBNull("Content") ? string.Empty : reader.GetString("Content"),
                    RawData = reader.IsDBNull("RawData") ? [] : (byte[])reader["RawData"],
                    ContentType = reader.IsDBNull("ContentType") ? string.Empty : reader.GetString("ContentType"),
                    StatusCode = reader.GetInt32("StatusCode"),
                    DownloadTimeMs = reader.GetInt32("DownloadTimeMs"),
                    ErrorMessage = reader.IsDBNull("ErrorMessage") ? string.Empty : reader.GetString("ErrorMessage"),
                    IsSuccess = reader.GetBoolean("IsSuccess"),
                },
                ProcessedAt = reader.GetDateTime("ProcessedAt"),
            };
        }

        /// <summary>
        /// 创建数据库表.
        /// </summary>
        /// <returns>任务. 完成时表示数据库表已创建.</returns>
        private async Task CreateTablesAsync()
        {
            var commands = new[]
            {
                // CrawlResults 表 - 存储爬取结果
                @"CREATE TABLE IF NOT EXISTS CrawlResults (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    Url VARCHAR(2048) UNIQUE NOT NULL,
                    Content LONGTEXT,
                    RawData LONGBLOB,
                    ContentType VARCHAR(255),
                    StatusCode INT,
                    DownloadTimeMs INT,
                    ProcessedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    Depth INT DEFAULT 0,
                    Referrer VARCHAR(2048),
                    Title VARCHAR(1024),
                    TextContent LONGTEXT,
                    ErrorMessage TEXT,
                    IsSuccess BOOLEAN DEFAULT TRUE
                )",

                // CrawlStates 表 - 存储爬虫状态
                @"CREATE TABLE IF NOT EXISTS CrawlStates (
                    JobId VARCHAR(100) PRIMARY KEY,
                    StartTime DATETIME NOT NULL,
                    EndTime DATETIME,
                    TotalUrlsDiscovered INT DEFAULT 0,
                    TotalUrlsProcessed INT DEFAULT 0,
                    TotalErrors INT DEFAULT 0,
                    Statistics TEXT,
                    Status INT NOT NULL,
                    Configuration TEXT
                )",

                // UrlStates 表 - 存储URL状态
                @"CREATE TABLE IF NOT EXISTS UrlStates (
                    Url VARCHAR(2048) PRIMARY KEY,
                    DiscoveredAt DATETIME NOT NULL,
                    ProcessedAt DATETIME,
                    StatusCode INT,
                    ContentLength BIGINT,
                    ContentType VARCHAR(255),
                    DownloadTimeMs INT,
                    ErrorMessage TEXT,
                    RetryCount INT DEFAULT 0
                )",

                // ExtractedData 表 - 存储提取的数据
                @"CREATE TABLE IF NOT EXISTS ExtractedData (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    Url VARCHAR(2048) NOT NULL,
                    DataKey VARCHAR(255) NOT NULL,
                    DataValue TEXT,
                    DataType VARCHAR(100),
                    FOREIGN KEY (Url) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",

                // Links 表 - 存储链接关系
                @"CREATE TABLE IF NOT EXISTS Links (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    SourceUrl VARCHAR(2048) NOT NULL,
                    TargetUrl VARCHAR(2048) NOT NULL,
                    LinkText TEXT,
                    FOREIGN KEY (SourceUrl) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",

                // Images 表 - 存储图片信息
                @"CREATE TABLE IF NOT EXISTS Images (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    Url VARCHAR(2048) NOT NULL,
                    ImageUrl VARCHAR(2048) NOT NULL,
                    AltText TEXT,
                    FOREIGN KEY (Url) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",

                // Metadata 表 - 存储自定义元数据
                @"CREATE TABLE IF NOT EXISTS Metadata (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    Url VARCHAR(2048) NOT NULL,
                    MetaKey VARCHAR(255) NOT NULL,
                    MetaValue TEXT,
                    FOREIGN KEY (Url) REFERENCES CrawlResults (Url) ON DELETE CASCADE
                )",
            };

            foreach (var commandText in commands)
            {
                try
                {
                    using var command = new MySqlCommand(commandText, this.connection);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to execute SQL command: {Command}", commandText);
                }
            }

            await this.CreateIndexesAsync();
        }

        /// <summary>
        /// 创建数据库索引.
        /// </summary>
        /// <returns>任务. 完成时表示数据库索引已创建.</returns>
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
                    using var command = new MySqlCommand(indexCommand, this.connection);
                    await command.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to create index: {Index}", indexCommand);
                }
            }
        }
    }
}