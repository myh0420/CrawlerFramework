// CrawlerInterFaces/Interfaces/IContentExtractor.cs
using CrawlerFramework.CrawlerEntity.Models;
using HtmlAgilityPack;

namespace CrawlerFramework.CrawlerInterFaces.Interfaces
{
    /// <summary>
    /// 内容提取器接口
    /// </summary>
    public interface IContentExtractor : ICrawlerComponent
    {
        /// <summary>
        /// 提取器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 从HTML文档中提取内容
        /// </summary>
        /// <param name="htmlDocument">HTML文档</param>
        /// <param name="downloadResult">下载结果</param>
        /// <returns>提取结果</returns>
        Task<ExtractionResult> ExtractAsync(HtmlDocument htmlDocument, DownloadResult downloadResult);
    }

    /// <summary>
    /// 提取结果
    /// </summary>
    public class ExtractionResult
    {
        /// <summary>
        /// 发现的链接
        /// </summary>
        public List<string> Links { get; set; } = [];

        /// <summary>
        /// 提取的数据
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = [];
    }
}