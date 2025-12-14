// CrawlerParser/AdvancedParser.cs
using CrawlerCore.Extractors;
using CrawlerInterFaces.Interfaces;
using CrawlerEntity.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CrawlerParser;
/// <summary>
/// 高级解析器，支持多种内容提取
/// </summary>
public partial class AdvancedParser : IParser
{
    /// <summary>
    /// HTML文档对象，用于解析HTML内容
    /// </summary>
    private readonly HtmlDocument _htmlDocument;
    /// <summary>
    /// 提取器字典，键为提取器名称，值为提取器实例
    /// </summary>
    private readonly Dictionary<string, IContentExtractor> _extractors;
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<AdvancedParser> _logger;
    /// <summary>
    /// 构造函数，初始化HTML文档对象、提取器字典和日志记录器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public AdvancedParser(ILogger<AdvancedParser> logger)
    {
        _htmlDocument = new();
        _extractors = [];
        _logger = logger;
        
        InitializeDefaultExtractors();
    }
    /// <summary>
    /// 初始化默认提取器，包括链接提取器、元数据提取器和内容提取器
    /// </summary>
    private void InitializeDefaultExtractors()
    {
        AddExtractor("links", new LinkExtractor());
        AddExtractor("metadata", new MetadataExtractor());
        AddExtractor("content", new ContentExtractor());
    }
    /// <summary>
    /// 异步解析下载结果，提取链接、元数据和内容
    /// </summary>
    /// <param name="downloadResult">下载结果，包含URL、内容类型和内容</param>
    /// <returns>解析结果，包含提取的链接、元数据和内容</returns>
    public async Task<ParseResult> ParseAsync(DownloadResult downloadResult)
    {
        var result = new ParseResult();

        try
        {
            // 参数验证
            if (downloadResult == null)
            {
                _logger.LogWarning("ParseAsync called with null downloadResult");
                return result;
            }
            
            if (string.IsNullOrEmpty(downloadResult.Content))
            {
                _logger.LogWarning("ParseAsync called with empty content for {Url}", downloadResult.Url);
                return result;
            }
            
            if (downloadResult.ContentType?.StartsWith("text/html") == true)
            {
                _htmlDocument.LoadHtml(downloadResult.Content);
                
                // 并行执行所有提取器
                var extractorTasks = _extractors.Values
                    .Select(extractor => extractor.ExtractAsync(_htmlDocument, downloadResult))
                    .ToArray();

                var extractorResults = await Task.WhenAll(extractorTasks);

                foreach (var extractorResult in extractorResults)
                {
                    result.Links.AddRange(extractorResult.Links);
                    foreach (var data in extractorResult.Data)
                    {
                        result.ExtractedData[data.Key] = data.Value;
                    }
                }

                // 提取标题和文本内容
                result.Title = ExtractTitle();
                result.TextContent = ExtractTextContent();
            }
            else if (downloadResult.ContentType?.StartsWith("text/") == true)
            {
                // 处理纯文本内容
                result.TextContent = downloadResult.Content;
                result.ExtractedData["raw"] = downloadResult.Content;
                
                // 简单提取纯文本的前100个字符作为标题
                result.Title = downloadResult.Content.Length > 100 
                    ? string.Concat(downloadResult.Content.AsSpan(0, 100), "...")
                    : downloadResult.Content;
            }
            else if (downloadResult.ContentType?.Equals("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                // 处理JSON内容
                try
                {
                    result.TextContent = downloadResult.Content;
                    result.ExtractedData["json"] = downloadResult.Content;
                    result.Title = "JSON Data";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JSON content for {Url}", downloadResult.Url);
                    result.TextContent = downloadResult.Content;
                    result.ExtractedData["raw"] = downloadResult.Content;
                }
            }
            else
            {
                // 处理其他内容类型，作为原始数据保存
                result.ExtractedData["raw"] = downloadResult.Content;
                result.Title = $"{downloadResult.ContentType} Data";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parsing failed for {Url}", downloadResult.Url);
        }

        return result;
    }
    /// <summary>
    /// 提取HTML文档的标题
    /// </summary>
    /// <returns>标题文本</returns>
    private string ExtractTitle()
    {
        return _htmlDocument.DocumentNode
            .SelectSingleNode("//title")?
            .InnerText?
            .Trim() ?? string.Empty;
    }
    /// <summary>
    /// 提取HTML文档的文本内容，移除脚本和样式标签
    /// </summary>
    /// <returns>文本内容</returns>
    // 使用GeneratedRegexAttribute在编译时生成正则表达式
    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex WhitespaceRegex();

    private string ExtractTextContent()
    {
        // 创建文档副本，避免修改原始文档
        var documentCopy = new HtmlDocument();
        documentCopy.LoadHtml(_htmlDocument.DocumentNode.OuterHtml);
        
        // 移除脚本和样式
        documentCopy.DocumentNode.Descendants()
            .Where(n => n.Name == "script" || n.Name == "style")
            .ToList()
            .ForEach(n => n.Remove());

        // 提取文本并清理多余空白字符
        string text = documentCopy.DocumentNode.InnerText;
        // 使用编译时生成的正则表达式替换多个连续空白字符为单个空格
        text = WhitespaceRegex().Replace(text, " ").Trim();
        
        return text;
    }
    /// <summary>
    /// 添加自定义提取器
    /// </summary>
    /// <param name="name">提取器名称</param>
    /// <param name="extractor">提取器实例</param>
    public void AddExtractor(string name, IContentExtractor extractor)
    {
        _extractors[name] = extractor;
    }
    /// <summary>
    /// 异步初始化解析器，当前实现为空
    /// </summary>
    /// <returns>已完成任务</returns>
    public Task InitializeAsync() => Task.CompletedTask;
    /// <summary>
    /// 异步关闭解析器，当前实现为空
    /// </summary>
    /// <returns>已完成任务</returns>
    public Task ShutdownAsync() => Task.CompletedTask;
}