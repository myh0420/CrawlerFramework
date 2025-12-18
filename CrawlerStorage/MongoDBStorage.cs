// <copyright file="MongoDBStorage.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerStorage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CrawlerFramework.CrawlerEntity.Enums;
    using CrawlerFramework.CrawlerEntity.Models;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Conventions;
    using MongoDB.Driver;
    using Newtonsoft.Json;

    /// <summary>
    /// MongoDB 存储实现.
    /// </summary>
    public class MongoDBStorage : IStorageProvider, IMetadataStore, IDisposable
    {
        /// <summary>
        /// MongoDB 客户端.
        /// </summary>
        private readonly MongoClient client;

        /// <summary>
        /// 数据库.
        /// </summary>
        private readonly IMongoDatabase database;

        /// <summary>
        /// 日志记录器.
        /// </summary>
        private readonly ILogger<MongoDBStorage> logger;

        /// <summary>
        /// 连接字符串.
        /// </summary>
        private readonly string connectionString;

        /// <summary>
        /// 数据库名称.
        /// </summary>
        private readonly string databaseName;

        /// <summary>
        /// 是否已释放.
        /// </summary>
        private bool disposed = false;

        // 集合

        /// <summary>
        /// 爬取结果集合.
        /// </summary>
        private IMongoCollection<CrawlResult> crawlResultsCollection = null!;

        /// <summary>
        /// 爬取状态集合.
        /// </summary>
        private IMongoCollection<CrawlState> crawlStatesCollection = null!;

        /// <summary>
        /// URL 状态集合.
        /// </summary>
        private IMongoCollection<UrlState> urlStatesCollection = null!;

        /// <summary>
        /// 提取数据集合.
        /// </summary>
        private IMongoCollection<ExtractedData> extractedDataCollection = null!;

        /// <summary>
        /// 链接集合.
        /// </summary>
        private IMongoCollection<Link> linksCollection = null!;

        /// <summary>
        /// 图片集合.
        /// </summary>
        private IMongoCollection<Image> imagesCollection = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="MongoDBStorage"/> class.
        /// 构造函数.
        /// </summary>
        /// <param name="connectionString">数据库连接字符串.</param>
        /// <param name="logger">日志记录器.</param>
        public MongoDBStorage(string? connectionString, ILogger<MongoDBStorage>? logger)
        {
            this.connectionString = connectionString ?? "mongodb://localhost:27017";
            this.databaseName = "crawler";
            this.logger = logger ?? new Logger<MongoDBStorage>(new LoggerFactory());

            // 配置MongoDB序列化约定
            ConfigureSerialization();

            // 创建客户端和数据库
            this.client = new MongoClient(this.connectionString);
            this.database = this.client.GetDatabase(this.databaseName);
        }

        /// <summary>
        /// 初始化数据库.
        /// </summary>
        /// <returns>任务.</returns>
        public async Task InitializeAsync()
        {
            try
            {
                // 初始化集合
                this.crawlResultsCollection = this.database.GetCollection<CrawlResult>("crawlResults");
                this.crawlStatesCollection = this.database.GetCollection<CrawlState>("crawlStates");
                this.urlStatesCollection = this.database.GetCollection<UrlState>("urlStates");
                this.extractedDataCollection = this.database.GetCollection<ExtractedData>("extractedData");
                this.linksCollection = this.database.GetCollection<Link>("links");
                this.imagesCollection = this.database.GetCollection<Image>("images");

                // 创建索引
                await this.CreateIndexesAsync();

                this.logger.LogInformation("MongoDB storage initialized with connection string: {ConnectionString}", this.connectionString);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to initialize MongoDB storage");
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

            try
            {
                // 批量保存爬取结果
                var tasks = new List<Task>();

                foreach (var result in results)
                {
                    // 保存主结果
                    tasks.Add(this.crawlResultsCollection.ReplaceOneAsync(
                        Builders<CrawlResult>.Filter.Eq(cr => cr.Request.Url, result.Request.Url),
                        result,
                        new ReplaceOptions { IsUpsert = true }));

                    // 保存提取的数据
                    if (result.ParseResult?.ExtractedData != null)
                    {
                        // 删除旧数据
                        tasks.Add(this.extractedDataCollection.DeleteManyAsync(ed => ed.Url == result.Request.Url));

                        // 添加新数据
                        foreach (var data in result.ParseResult.ExtractedData)
                        {
                            var extractedData = new ExtractedData
                            {
                                Url = result.Request.Url,
                                DataKey = data.Key,
                                DataValue = data.Value?.ToString() ?? string.Empty,
                                DataType = data.Value?.GetType().Name ?? "String",
                            };
                            tasks.Add(this.extractedDataCollection.InsertOneAsync(extractedData));
                        }
                    }

                    // 保存链接
                    if (result.ParseResult?.Links != null)
                    {
                        // 删除旧链接
                        tasks.Add(this.linksCollection.DeleteManyAsync(l => l.SourceUrl == result.Request.Url));

                        // 添加新链接
                        foreach (var link in result.ParseResult.Links)
                        {
                            var linkDocument = new Link
                            {
                                SourceUrl = result.Request.Url,
                                TargetUrl = link,
                                LinkText = string.Empty,
                            };
                            tasks.Add(this.linksCollection.InsertOneAsync(linkDocument));
                        }
                    }

                    // 保存图片
                    if (result.ParseResult?.Images != null)
                    {
                        // 删除旧图片
                        tasks.Add(this.imagesCollection.DeleteManyAsync(i => i.Url == result.Request.Url));

                        // 添加新图片
                        foreach (var imageUrl in result.ParseResult.Images)
                        {
                            var imageDocument = new Image
                            {
                                Url = result.Request.Url,
                                ImageUrl = imageUrl,
                                AltText = string.Empty,
                            };
                            tasks.Add(this.imagesCollection.InsertOneAsync(imageDocument));
                        }
                    }

                    // 保存URL状态
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
                    tasks.Add(this.urlStatesCollection.ReplaceOneAsync(
                        Builders<UrlState>.Filter.Eq(us => us.Url, urlState.Url),
                        urlState,
                        new ReplaceOptions { IsUpsert = true }));
                }

                // 等待所有任务完成
                await Task.WhenAll(tasks);
                this.logger.LogDebug("Batch saved {Count} crawl results to MongoDB", results.Count);
            }
            catch (Exception ex)
            {
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
            var filter = Builders<CrawlResult>.Filter.Regex(cr => cr.Request.Url, new BsonRegularExpression(domain));
            var results = await this.crawlResultsCollection.Find(filter)
                .Limit(limit)
                .ToListAsync();

            return results;
        }

        /// <summary>
        /// 异步获取指定URL的爬取结果.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>爬取结果.</returns>
        public async Task<CrawlResult?> GetByUrlAsync(string url)
        {
            var filter = Builders<CrawlResult>.Filter.Eq(cr => cr.Request.Url, url);
            return await this.crawlResultsCollection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 异步获取总记录数.
        /// </summary>
        /// <returns>总记录数.</returns>
        public async Task<long> GetTotalCountAsync()
        {
            return await this.crawlResultsCollection.CountDocumentsAsync(FilterDefinition<CrawlResult>.Empty);
        }

        /// <summary>
        /// 异步删除指定URL的爬取结果.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>已完成任务.</returns>
        public async Task<bool> DeleteAsync(string url)
        {
            try
            {
                // 删除所有相关数据
                var tasks = new List<Task<DeleteResult>>
                {
                    this.crawlResultsCollection.DeleteOneAsync(cr => cr.Request.Url == url),
                    this.extractedDataCollection.DeleteManyAsync(ed => ed.Url == url),
                    this.linksCollection.DeleteManyAsync(l => l.SourceUrl == url || l.TargetUrl == url),
                    this.imagesCollection.DeleteManyAsync(i => i.Url == url),
                    this.urlStatesCollection.DeleteOneAsync(us => us.Url == url),
                };

                // 等待所有任务完成
                var results = await Task.WhenAll(tasks);

                // 检查是否有任何文档被删除
                foreach (var result in results)
                {
                    if (result.DeletedCount > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
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
            var statistics = new CrawlStatistics
            {
                // 获取总记录数
                TotalUrlsProcessed = Convert.ToInt32(await this.crawlResultsCollection.CountDocumentsAsync(FilterDefinition<CrawlResult>.Empty)),
            };

            // 获取成功记录数
            var successFilter = Builders<CrawlResult>.Filter.Eq(cr => cr.DownloadResult.IsSuccess, true);
            statistics.SuccessCount = Convert.ToInt32(await this.crawlResultsCollection.CountDocumentsAsync(successFilter));

            // 获取失败记录数
            var errorFilter = Builders<CrawlResult>.Filter.Eq(cr => cr.DownloadResult.IsSuccess, false);
            statistics.ErrorCount = Convert.ToInt32(await this.crawlResultsCollection.CountDocumentsAsync(errorFilter));

            // 获取平均下载时间
            var aggregateResult = await this.crawlResultsCollection.Aggregate()
                .Match(Builders<CrawlResult>.Filter.Gt(cr => cr.DownloadResult.DownloadTimeMs, 0))
                .Group(new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "avgDownloadTime", new BsonDocument("$avg", "$downloadResult.downloadTimeMs") },
                })
                .FirstOrDefaultAsync();

            if (aggregateResult != null && aggregateResult["avgDownloadTime"] != BsonNull.Value)
            {
                statistics.AverageDownloadTimeMs = aggregateResult["avgDownloadTime"].AsDouble;
            }

            return statistics;
        }

        /// <summary>
        /// 异步清除所有数据.
        /// </summary>
        /// <returns>已完成任务.</returns>
        public async Task ClearAllAsync()
        {
            try
            {
                // 删除所有集合数据
                var tasks = new List<Task<DeleteResult>>
                {
                    this.crawlResultsCollection.DeleteManyAsync(FilterDefinition<CrawlResult>.Empty),
                    this.crawlStatesCollection.DeleteManyAsync(FilterDefinition<CrawlState>.Empty),
                    this.urlStatesCollection.DeleteManyAsync(FilterDefinition<UrlState>.Empty),
                    this.extractedDataCollection.DeleteManyAsync(FilterDefinition<ExtractedData>.Empty),
                    this.linksCollection.DeleteManyAsync(FilterDefinition<Link>.Empty),
                    this.imagesCollection.DeleteManyAsync(FilterDefinition<Image>.Empty),
                };

                await Task.WhenAll(tasks);
                this.logger.LogInformation("Cleared all data from MongoDB storage");
            }
            catch (Exception ex)
            {
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
            this.logger.LogWarning("MongoDB backup functionality not implemented yet");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 异步保存爬取状态.
        /// </summary>
        /// <param name="state">爬取状态.</param>
        /// <returns>已完成任务.</returns>
        public async Task SaveCrawlStateAsync(CrawlState state)
        {
            await this.crawlStatesCollection.ReplaceOneAsync(
                Builders<CrawlState>.Filter.Eq(cs => cs.JobId, state.JobId),
                state,
                new ReplaceOptions { IsUpsert = true });
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

            var filter = Builders<CrawlState>.Filter.Eq(cs => cs.JobId, jobId);
            return await this.crawlStatesCollection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 异步保存URL状态.
        /// </summary>
        /// <param name="state">URL状态.</param>
        /// <returns>已完成任务.</returns>
        public async Task SaveUrlStateAsync(UrlState state)
        {
            await this.urlStatesCollection.ReplaceOneAsync(
                Builders<UrlState>.Filter.Eq(us => us.Url, state.Url),
                state,
                new ReplaceOptions { IsUpsert = true });
        }

        /// <summary>
        /// 异步获取指定URL的URL状态.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <returns>URL状态.</returns>
        public async Task<UrlState?> GetUrlStateAsync(string url)
        {
            var filter = Builders<UrlState>.Filter.Eq(us => us.Url, url);
            return await this.urlStatesCollection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 关闭数据库连接.
        /// </summary>
        /// <returns>已完成任务.</returns>
        public async Task ShutdownAsync()
        {
            try
            {
                // MongoDB客户端不需要显式关闭
                this.logger.LogInformation("MongoDB storage shutdown");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to shutdown MongoDB storage");
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
                    // MongoDB客户端不需要显式释放
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// 配置MongoDB序列化约定.
        /// </summary>
        private static void ConfigureSerialization()
        {
            // 使用camelCase命名约定
            var pack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("CamelCase", pack, t => true);

            // 注册自定义类型映射
            if (!BsonClassMap.IsClassMapRegistered(typeof(CrawlResult)))
            {
                BsonClassMap.RegisterClassMap<CrawlResult>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(cr => cr.Request.Url);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(CrawlState)))
            {
                BsonClassMap.RegisterClassMap<CrawlState>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(cs => cs.JobId);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(UrlState)))
            {
                BsonClassMap.RegisterClassMap<UrlState>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(us => us.Url);
                });
            }
        }

        /// <summary>
        /// 创建数据库索引.
        /// </summary>
        /// <returns>任务.</returns>
        private async Task CreateIndexesAsync()
        {
            // CrawlResults 索引
            await this.crawlResultsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<CrawlResult>(
                    Builders<CrawlResult>.IndexKeys.Ascending(cr => cr.Request.Depth),
                    new CreateIndexOptions { Background = true }));

            await this.crawlResultsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<CrawlResult>(
                    Builders<CrawlResult>.IndexKeys.Ascending(cr => cr.ProcessedAt),
                    new CreateIndexOptions { Background = true }));

            await this.crawlResultsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<CrawlResult>(
                    Builders<CrawlResult>.IndexKeys.Ascending(cr => cr.DownloadResult.StatusCode),
                    new CreateIndexOptions { Background = true }));

            // UrlStates 索引
            await this.urlStatesCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<UrlState>(
                    Builders<UrlState>.IndexKeys.Ascending(us => us.ProcessedAt),
                    new CreateIndexOptions { Background = true }));

            // Links 索引
            await this.linksCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<Link>(
                    Builders<Link>.IndexKeys.Ascending(l => l.SourceUrl),
                    new CreateIndexOptions { Background = true }));

            await this.linksCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<Link>(
                    Builders<Link>.IndexKeys.Ascending(l => l.TargetUrl),
                    new CreateIndexOptions { Background = true }));

            // ExtractedData 索引
            await this.extractedDataCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ExtractedData>(
                    Builders<ExtractedData>.IndexKeys.Ascending(ed => ed.Url),
                    new CreateIndexOptions { Background = true }));

            await this.extractedDataCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ExtractedData>(
                    Builders<ExtractedData>.IndexKeys.Ascending(ed => ed.DataKey),
                    new CreateIndexOptions { Background = true }));
        }
    }
}