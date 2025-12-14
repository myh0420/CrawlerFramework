// CrawlerCore/Extractors/AIAssistedExtractor.cs
using CrawlerInterFaces.Interfaces;
using CrawlerEntity.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CrawlerCore.Extractors;

/// <summary>
/// AI辅助内容提取器
/// </summary>
public class AIAssistedExtractor(IAIHelper aiHelper, ILogger logger) : IContentExtractor
{
    /// <summary>
    /// 提取AI辅助内容
    /// </summary>
    private readonly IAIHelper _aiHelper = aiHelper;
    /// <summary>
    /// 提取AI辅助内容
    /// </summary>
    private readonly ILogger _logger = logger;
    /// <summary>
    /// 提取器名称
    /// </summary>
    public string Name => "ai-assistant";
    /// <summary>
    /// 异步提取AI辅助内容
    /// </summary>
    /// <param name="htmlDocument">HTML文档</param>
    /// <param name="downloadResult">下载结果</param>
    /// <returns>提取结果</returns>
    public async Task<ExtractionResult> ExtractAsync(HtmlDocument htmlDocument, DownloadResult downloadResult)
    {
        try
        {
            var htmlContent = htmlDocument.DocumentNode.OuterHtml;
            var url = downloadResult.Url;
            
            // 使用AI辅助提取主要内容
            var mainContent = await _aiHelper.ExtractMainContentAsync(htmlContent, url);
            
            // 使用AI辅助分析结构
            var structure = await _aiHelper.AnalyzeStructureAsync(htmlContent);
            
            var result = new ExtractionResult
            {
                Links = [],
                Data = new()
                {
                    ["mainContent"] = mainContent,
                    ["structure"] = structure,
                    ["isAIAssisted"] = true
                }
            };
            
            _logger.LogInformation("AI-assisted extraction completed for {Url}", url);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI-assisted extraction failed for {Url}", downloadResult.Url);
            return new ExtractionResult();
        }
    }
}