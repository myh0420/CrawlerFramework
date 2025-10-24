// CrawlerInterFaces/Interfaces/IStorageProvider.cs

using CrawlerEntity.Models;

namespace CrawlerInterFaces.Interfaces;
public interface IStorageProvider : ICrawlerComponent
{
    Task SaveAsync(CrawlResult result);
    Task<IEnumerable<CrawlResult>> GetByDomainAsync(string domain, int limit = 100);
    Task<CrawlResult?> GetByUrlAsync(string url);
    Task<long> GetTotalCountAsync();
    Task<bool> DeleteAsync(string url);

    // 新增方法
    Task<CrawlStatistics> GetStatisticsAsync();
    Task ClearAllAsync();
    Task BackupAsync(string backupPath);
}

public interface IMetadataStore
{
    Task SaveCrawlStateAsync(CrawlState state);
    Task<CrawlState?> GetCrawlStateAsync(string? jobId);
    Task SaveUrlStateAsync(UrlState state);
    Task<UrlState?> GetUrlStateAsync(string url);
}
