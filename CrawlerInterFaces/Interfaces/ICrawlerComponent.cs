// CrawlerInterFaces/Interfaces/ICrawlerComponent.cs
using CrawlerEntity.Models;

namespace CrawlerInterFaces.Interfaces;
public interface ICrawlerComponent
{
    Task InitializeAsync();
    Task ShutdownAsync();
}

public interface IDownloader : ICrawlerComponent
{
    Task<DownloadResult> DownloadAsync(CrawlRequest request);
    int ConcurrentRequests { get; set; }
    TimeSpan Timeout { get; set; }
}

public interface IParser : ICrawlerComponent
{
    Task<ParseResult> ParseAsync(DownloadResult downloadResult);
    void AddExtractor(string name, IContentExtractor extractor);
}

public interface IScheduler : ICrawlerComponent
{
    Task<bool> AddUrlAsync(CrawlRequest request);
    Task<CrawlRequest?> GetNextAsync();
    int QueuedCount { get; }
    int ProcessedCount { get; }
}

//public interface IStorage : ICrawlerComponent
//{
//    Task SaveContentAsync(CrawlResult result);
//    Task SaveMetadataAsync(CrawlMetadata metadata);
//}