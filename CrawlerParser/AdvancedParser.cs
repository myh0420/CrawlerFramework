// CrawlerParser/AdvancedParser.cs
using CrawlerCore.Extractors;
using CrawlerInterFaces.Interfaces;
using CrawlerEntity.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CrawlerParser;
public class AdvancedParser : IParser
{
    private readonly HtmlDocument _htmlDocument;
    private readonly Dictionary<string, IContentExtractor> _extractors;
    private readonly ILogger<AdvancedParser> _logger;

    public AdvancedParser(ILogger<AdvancedParser> logger)
    {
        _htmlDocument = new();
        _extractors = [];
        _logger = logger;
        
        InitializeDefaultExtractors();
    }

    private void InitializeDefaultExtractors()
    {
        AddExtractor("links", new LinkExtractor());
        AddExtractor("metadata", new MetadataExtractor());
        AddExtractor("content", new ContentExtractor());
    }

    public async Task<ParseResult> ParseAsync(DownloadResult downloadResult)
    {
        var result = new ParseResult();

        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parsing failed for {Url}", downloadResult.Url);
        }

        return result;
    }

    private string ExtractTitle()
    {
        return _htmlDocument.DocumentNode
            .SelectSingleNode("//title")?
            .InnerText?
            .Trim() ?? string.Empty;
    }

    private string ExtractTextContent()
    {
        // 移除脚本和样式
        _htmlDocument.DocumentNode.Descendants()
            .Where(n => n.Name == "script" || n.Name == "style")
            .ToList()
            .ForEach(n => n.Remove());

        return _htmlDocument.DocumentNode.InnerText;
    }

    public void AddExtractor(string name, IContentExtractor extractor)
    {
        _extractors[name] = extractor;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task ShutdownAsync() => Task.CompletedTask;
}