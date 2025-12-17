// CrawlerStorage/MongoDBStorage.cs
using CrawlerEntity.Enums;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CrawlerStorage
{
    /// <summary>
    /// MongoDB 存储实现
    /// </summary>
    public class MongoDBStorage : IStorageProvider, IMetadataStore, IDisposable
    {
        /// <summary>
        /// MongoDB 客户端
        /// </summary>
        private readonly IMongoClient _client;
        /// <summary>
        /// 数据库
        /// </summary>
        private readonly IMongoDatabase _database;
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger<MongoDBStorage> _logger;
        /// <summary>
        /// 连接字符串
        /// </summary>
        private readonly string _connectionString;
        /// <summary>
        /// 数据库名称
        /// </summary>
        private readonly string _databaseName;
        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed = false;

        // 集合
        private IMongoCollection<CrawlResult> _crawlResultsCollection = null!;
        private IMongoCollection<CrawlState> _crawlStatesCollection = null!;
        private IMongoCollection<UrlState> _urlStatesCollection = null!;
        private IMongoCollection<ExtractedData> _extractedDataCollection = null!;
        private IMongoCollection<Link> _linksCollection = null!;
        private IMongoCollection<Image> _imagesCollection = null!;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connectionString">数据库连接字符串</param>
        /// <param name="logger">日志记录器</param>
        public MongoDBStorage(string? connectionString, ILogger<MongoDBStorage>? logger)
        {
            _connectionString = connectionString ?? "mongodb://localhost:27017";
            _databaseName = "crawler";
            _logger = logger ?? new Logger<MongoDBStorage>(new LoggerFactory());

            // 配置MongoDB序列化约定
            ConfigureSerialization();

            // 创建客户端和数据库
            _client = new MongoClient(_connectionString);
            _database = _client.GetDatabase(_databaseName);
        }

        /// <summary>
        /// 配置MongoDB序列化
        /// </summary>
        private void ConfigureSerialization()
        {
            // 使用camelCase命名约定
            var pack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register("CamelCase", pack, t => true);

            // 注册自定义类型映射
            if (!BsonClassMap.IsClassMapRegistered(typeof(CrawlResult))) {
                BsonClassMap.RegisterClassMap<CrawlResult>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(cr => cr.Request.Url);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(CrawlState))) {
                BsonClassMap.RegisterClassMap<CrawlState>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(cs => cs.JobId);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(UrlState))) {
                BsonClassMap.RegisterClassMap<UrlState>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(us => us.Url);
                });
            }
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        /// <returns>任务</returns>
        public async Task InitializeAsync()
        {
            try
            {
                // 初始化集合
                _crawlResultsCollection = _database.GetCollection<CrawlResult>("crawlResults");
                _crawlStatesCollection = _database.GetCollection<CrawlState>("crawlStates");
                _urlStatesCollection = _database.GetCollection<UrlState>("urlStates");
                _extractedDataCollection = _database.GetCollection<ExtractedData>("extractedData");
                _linksCollection = _database.GetCollection<Link>("links");
                _imagesCollection = _database.GetCollection<Image>("images");

                // 创建索引
                await CreateIndexesAsync();

                _logger.LogInformation("MongoDB storage initialized with connection string: {ConnectionString}", _connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MongoDB storage");
                throw;
            }
        }

        /// <summary>
        /// 创建数据库索引
        /// </summary>
        /// <returns>任务</returns>
        private async Task CreateIndexesAsync()
        {
            // CrawlResults 索引
            await _crawlResultsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<CrawlResult>(
                    Builders<CrawlResult>.IndexKeys.Ascending(cr => cr.Request.Depth),
                    new CreateIndexOptions { Background = true }));

            await _crawlResultsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<CrawlResult>(
                    Builders<CrawlResult>.IndexKeys.Ascending(cr => cr.ProcessedAt),
                    new CreateIndexOptions { Background = true }));

            await _crawlResultsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<CrawlResult>(
                    Builders<CrawlResult>.IndexKeys.Ascending(cr => cr.DownloadResult.StatusCode),
                    new CreateIndexOptions { Background = true }));

            // UrlStates 索引
            await _urlStatesCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<UrlState>(
                    Builders<UrlState>.IndexKeys.Ascending(us => us.ProcessedAt),
                    new CreateIndexOptions { Background = true }));

            // Links 索引
            await _linksCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<Link>(
                    Builders<Link>.IndexKeys.Ascending(l => l.SourceUrl),
                    new CreateIndexOptions { Background = true }));

            await _linksCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<Link>(
                    Builders<Link>.IndexKeys.Ascending(l => l.TargetUrl),
                    new CreateIndexOptions { Background = true }));

            // ExtractedData 索引
            await _extractedDataCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ExtractedData>(
                    Builders<ExtractedData>.IndexKeys.Ascending(ed => ed.Url),
                    new CreateIndexOptions { Background = true }));

            await _extractedDataCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<ExtractedData>(
                    Builders<ExtractedData>.IndexKeys.Ascending(ed => ed.DataKey),
                    new CreateIndexOptions { Background = true }));
        }

        /// <summary>
        /// 异步保存爬取结果
        /// </summary>
        /// <param name="result">爬取结果</param>
        /// <returns>任务</returns>
        public async Task SaveAsync(CrawlResult result)
        {
            await BatchSaveAsync([result]);
        }

        /// <summary>
        /// 异步批量保存爬取结果
        /// </summary>
        /// <param name="results">爬取结果列表</param>
        /// <returns>任务</returns>
        public async Task BatchSaveAsync(List<CrawlResult> results)
        {
            if (results == null || results.Count == 0)
                return;

            try
            {
                // 批量保存爬取结果
                var tasks = new List<Task>();

                foreach (var result in results)
                {
                    // 保存主结果
                    tasks.Add(_crawlResultsCollection.ReplaceOneAsync(
                        Builders<CrawlResult>.Filter.Eq(cr => cr.Request.Url, result.Request.Url),
                        result,
                        new ReplaceOptions { IsUpsert = true }));

                    // 保存提取的数据
                    if (result.ParseResult?.ExtractedData != null)
                    {
                        // 删除旧数据
                        tasks.Add(_extractedDataCollection.DeleteManyAsync(ed => ed.Url == result.Request.Url));

                        // 添加新数据
                        foreach (var data in result.ParseResult.ExtractedData)
                        {
                            var extractedData = new ExtractedData
                            {
                                Url = result.Request.Url,
                                DataKey = data.Key,
                                DataValue = data.Value?.ToString() ?? string.Empty,
                                DataType = data.Value?.GetType().Name ?? "String"
                            };
                            tasks.Add(_extractedDataCollection.InsertOneAsync(extractedData));
                        }
                    }

                    // 保存链接
                    if (result.ParseResult?.Links != null)
                    {
                        // 删除旧链接
                        tasks.Add(_linksCollection.DeleteManyAsync(l => l.SourceUrl == result.Request.Url));

                        // 添加新链接
                        foreach (var link in result.ParseResult.Links)
                        {
                            var linkDocument = new Link
                            {
                                SourceUrl = result.Request.Url,
                                TargetUrl = link,
                                LinkText = string.Empty
                            };
                            tasks.Add(_linksCollection.InsertOneAsync(linkDocument));
                        }
                    }

                    // 保存图片
                    if (result.ParseResult?.Images != null)
                    {
                        // 删除旧图片
                        tasks.Add(_imagesCollection.DeleteManyAsync(i => i.Url == result.Request.Url));

                        // 添加新图片
                        foreach (var imageUrl in result.ParseResult.Images)
                        {
                            var imageDocument = new Image
                            {
                                Url = result.Request.Url,
                                ImageUrl = imageUrl,
                                AltText = string.Empty
                            };
                            tasks.Add(_imagesCollection.InsertOneAsync(imageDocument));
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
                        ErrorMessage = result.DownloadResult.ErrorMessage ?? string.Empty
                    };
                    tasks.Add(_urlStatesCollection.ReplaceOneAsync(
                        Builders<UrlState>.Filter.Eq(us => us.Url, urlState.Url),
                        urlState,
                        new ReplaceOptions { IsUpsert = true }));
                }

                // 等待所有任务完成
                await Task.WhenAll(tasks);
                _logger.LogDebug("Batch saved {Count} crawl results to MongoDB", results.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to batch save crawl results");
                throw;
            }
        }

        /// <summary>
        /// 异步获取指定域名的爬取结果
        /// </summary>
        /// <param name="domain">域名</param>
        /// <param name="limit">最大返回数量，默认值为100</param>
        /// <returns>爬取结果列表</returns>
        public async Task<IEnumerable<CrawlResult>> GetByDomainAsync(string domain, int limit = 100)
        {
            var filter = Builders<CrawlResult>.Filter.Regex(cr => cr.Request.Url, new BsonRegularExpression(domain));
            var results = await _crawlResultsCollection.Find(filter)
                .Limit(limit)
                .ToListAsync();

            return results;
        }

        /// <summary>
        /// 异步获取指定URL的爬取结果
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>爬取结果</returns>
        public async Task<CrawlResult?> GetByUrlAsync(string url)
        {
            var filter = Builders<CrawlResult>.Filter.Eq(cr => cr.Request.Url, url);
            return await _crawlResultsCollection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 异步获取总记录数
        /// </summary>
        /// <returns>总记录数</returns>
        public async Task<long> GetTotalCountAsync()
        {
            return await _crawlResultsCollection.CountDocumentsAsync(FilterDefinition<CrawlResult>.Empty);
        }

        /// <summary>
        /// 异步删除指定URL的爬取结果
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>已完成任务</returns>
        public async Task<bool> DeleteAsync(string url)
        {
            try
            {
                // 删除所有相关数据
                var tasks = new List<Task<DeleteResult>>
                {
                    _crawlResultsCollection.DeleteOneAsync(cr => cr.Request.Url == url),
                    _extractedDataCollection.DeleteManyAsync(ed => ed.Url == url),
                    _linksCollection.DeleteManyAsync(l => l.SourceUrl == url || l.TargetUrl == url),
                    _imagesCollection.DeleteManyAsync(i => i.Url == url),
                    _urlStatesCollection.DeleteOneAsync(us => us.Url == url)
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
                _logger.LogError(ex, "Failed to delete crawl result for URL: {Url}", url);
                return false;
            }
        }

        /// <summary>
        /// 异步获取爬取统计信息
        /// </summary>
        /// <returns>爬取统计信息</returns>
        public async Task<CrawlStatistics> GetStatisticsAsync()
        {
            var statistics = new CrawlStatistics
            {
                // 获取总记录数
                TotalUrlsProcessed = Convert.ToInt32(await _crawlResultsCollection.CountDocumentsAsync(FilterDefinition<CrawlResult>.Empty))
            };

            // 获取成功记录数
            var successFilter = Builders<CrawlResult>.Filter.Eq(cr => cr.DownloadResult.IsSuccess, true);
            statistics.SuccessCount = Convert.ToInt32(await _crawlResultsCollection.CountDocumentsAsync(successFilter));

            // 获取失败记录数
            var errorFilter = Builders<CrawlResult>.Filter.Eq(cr => cr.DownloadResult.IsSuccess, false);
            statistics.ErrorCount = Convert.ToInt32(await _crawlResultsCollection.CountDocumentsAsync(errorFilter));

            // 获取平均下载时间
            var aggregateResult = await _crawlResultsCollection.Aggregate()
                .Match(Builders<CrawlResult>.Filter.Gt(cr => cr.DownloadResult.DownloadTimeMs, 0))
                .Group(new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "avgDownloadTime", new BsonDocument("$avg", "$downloadResult.downloadTimeMs") }
                })
                .FirstOrDefaultAsync();

            if (aggregateResult != null && aggregateResult["avgDownloadTime"] != BsonNull.Value)
            {
                statistics.AverageDownloadTimeMs = aggregateResult["avgDownloadTime"].AsDouble;
            }

            return statistics;
        }

        /// <summary>
        /// 异步清除所有数据
        /// </summary>
        /// <returns>已完成任务</returns>
        public async Task ClearAllAsync()
        {
            try
            {
                // 删除所有集合数据
                var tasks = new List<Task<DeleteResult>>
                {
                    _crawlResultsCollection.DeleteManyAsync(FilterDefinition<CrawlResult>.Empty),
                    _crawlStatesCollection.DeleteManyAsync(FilterDefinition<CrawlState>.Empty),
                    _urlStatesCollection.DeleteManyAsync(FilterDefinition<UrlState>.Empty),
                    _extractedDataCollection.DeleteManyAsync(FilterDefinition<ExtractedData>.Empty),
                    _linksCollection.DeleteManyAsync(FilterDefinition<Link>.Empty),
                    _imagesCollection.DeleteManyAsync(FilterDefinition<Image>.Empty)
                };

                await Task.WhenAll(tasks);
                _logger.LogInformation("Cleared all data from MongoDB storage");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear all data");
                throw;
            }
        }

        /// <summary>
        /// 异步备份数据
        /// </summary>
        /// <param name="backupPath">备份路径</param>
        /// <returns>已完成任务</returns>
        public async Task BackupAsync(string backupPath)
        {
            _logger.LogWarning("MongoDB backup functionality not implemented yet");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 异步保存爬取状态
        /// </summary>
        /// <param name="state">爬取状态</param>
        /// <returns>已完成任务</returns>
        public async Task SaveCrawlStateAsync(CrawlState state)
        {
            await _crawlStatesCollection.ReplaceOneAsync(
                Builders<CrawlState>.Filter.Eq(cs => cs.JobId, state.JobId),
                state,
                new ReplaceOptions { IsUpsert = true });
        }

        /// <summary>
        /// 异步获取指定作业ID的爬取状态
        /// </summary>
        /// <param name="jobId">作业ID</param>
        /// <returns>爬取状态</returns>
        public async Task<CrawlState?> GetCrawlStateAsync(string? jobId)
        {
            if (string.IsNullOrEmpty(jobId))
                return null;

            var filter = Builders<CrawlState>.Filter.Eq(cs => cs.JobId, jobId);
            return await _crawlStatesCollection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 异步保存URL状态
        /// </summary>
        /// <param name="state">URL状态</param>
        /// <returns>已完成任务</returns>
        public async Task SaveUrlStateAsync(UrlState state)
        {
            await _urlStatesCollection.ReplaceOneAsync(
                Builders<UrlState>.Filter.Eq(us => us.Url, state.Url),
                state,
                new ReplaceOptions { IsUpsert = true });
        }

        /// <summary>
        /// 异步获取指定URL的URL状态
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>URL状态</returns>
        public async Task<UrlState?> GetUrlStateAsync(string url)
        {
            var filter = Builders<UrlState>.Filter.Eq(us => us.Url, url);
            return await _urlStatesCollection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        /// <returns>任务</returns>
        public async Task ShutdownAsync()
        {
            try
            {
                // MongoDB客户端不需要显式关闭
                _logger.LogInformation("MongoDB storage shutdown");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to shutdown MongoDB storage");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // MongoDB客户端不需要显式释放
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    // MongoDB 文档模型
    internal class ExtractedData
    {
        public ObjectId Id { get; set; }
        public required string Url { get; set; }
        public required string DataKey { get; set; }
        public required string DataValue { get; set; }
        public required string DataType { get; set; }
    }

    internal class Link
    {
        public ObjectId Id { get; set; }
        public required string SourceUrl { get; set; }
        public required string TargetUrl { get; set; }
        public required string LinkText { get; set; }
    }

    internal class Image
    {
        public ObjectId Id { get; set; }
        public required string Url { get; set; }
        public required string ImageUrl { get; set; }
        public required string AltText { get; set; }
    }
}