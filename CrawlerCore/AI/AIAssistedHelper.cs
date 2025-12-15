// CrawlerCore/AI/AIAssistedHelper.cs
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Text;
using System.Xml.Linq;

namespace CrawlerCore.AI;

/// <summary>
/// AI辅助解析器实现类
/// </summary>
public partial class AIAssistedHelper(ILogger<AIAssistedHelper> logger) : IAIHelper
{
    private readonly ILogger<AIAssistedHelper> _logger = logger;
    private bool _isInitialized = false;
    private readonly bool _isAvailable = true;
    private readonly Dictionary<string, string> _contentPatterns = new()
    {
        { "article", "main|article|content|post" },
        { "title", "h1|h2|title" },
        { "meta", "meta|description|keywords" },
        { "navigation", "nav|menu|sidebar|aside" },
        { "footer", "footer" }
    };

    [GeneratedRegex(@"<(script|style|iframe|noscript|svg)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+\n\s+")]
    private static partial Regex ExtraNewlineRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ExtraWhitespaceRegex();

    [GeneratedRegex(@"(\r\n|\r|\n)")]
    private static partial Regex NewlineRegex();

    [GeneratedRegex(@"(?i)<(h[1-6]|title)[^>]*>(.*?)</\1>")]
    private static partial Regex TitleTagRegex();

    [GeneratedRegex(@"(?i)<(meta|link)[^>]*>")]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(@"(?i)<(main|article|div|section)[^>]*class=[""]?.*?(content|article|post|main).*?[""]?[^>]*>(.*?)</\1>", RegexOptions.Singleline)]
    private static partial Regex MainContentRegex();

    [GeneratedRegex(@"content=[""'](.*?)[""']")]
    private static partial Regex ContentRegex();

    [GeneratedRegex(@"(?i)<a[^>]*href=[""'].*?[""'][^>]*>")]
    private static partial Regex LinkTagRegex();

    [GeneratedRegex(@"(?i)<a[^>]*href=[""'](.*?)[""'][^>]*>(.*?)</a>")]
    private static partial Regex LinkWithTextRegex();

    [GeneratedRegex(@"(?i)<h([1-6])[^>]*>.*?</h\1>")]
    private static partial Regex HeadingRegex();

    public Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            _logger.LogInformation("AIAssistedHelper initialized successfully");
            _logger.LogDebug("Content patterns: {Patterns}", string.Join(", ", _contentPatterns.Select(kv => $"{kv.Key}: {kv.Value}")));
        }
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        if (_isInitialized)
        {
            _isInitialized = false;
            _logger.LogInformation("AIAssistedHelper shutdown successfully");
        }
        return Task.CompletedTask;
    }

    public Task<string> ExtractMainContentAsync(string htmlContent, string url = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                _logger.LogWarning("Empty HTML content provided for extraction");
                return Task.FromResult(string.Empty);
            }

            _logger.LogDebug("Extracting main content from HTML with length: {Length}", htmlContent.Length);

            // 1. 移除脚本、样式和iframe等不需要的内容
            var cleanedHtml = ScriptStyleRegex().Replace(htmlContent, string.Empty);
            
            // 2. 尝试提取主要内容块
            var mainContentMatch = MainContentRegex().Match(cleanedHtml);
            if (mainContentMatch.Success)
            {
                cleanedHtml = mainContentMatch.Groups[3].Value;
                _logger.LogDebug("Found main content block, extracting from it");
            }

            // 3. 移除所有HTML标签
            var text = HtmlTagRegex().Replace(cleanedHtml, string.Empty);
            
            // 4. 清理文本内容
            text = CleanTextContent(text);
            
            // 5. 提取并保留标题
            var title = ExtractTitle(htmlContent);
            if (!string.IsNullOrWhiteSpace(title) && !text.StartsWith(title))
            {
                text = $"{title}\n\n{text}";
            }

            _logger.LogInformation("Successfully extracted main content with length: {Length}", text.Length);
            return Task.FromResult(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract main content from HTML");
            return Task.FromResult(string.Empty);
        }
    }

    public Task<string> ExtractWithPromptAsync(string htmlContent, string prompt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                _logger.LogWarning("Empty HTML content provided for prompt extraction");
                return Task.FromResult(string.Empty);
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                _logger.LogWarning("Empty prompt provided for extraction");
                return Task.FromResult(string.Empty);
            }

            _logger.LogInformation("Extracting content with prompt: {Prompt}", prompt);

            // 移除脚本和样式
            var cleanedHtml = ScriptStyleRegex().Replace(htmlContent, string.Empty);
            
            // 根据提示类型执行不同的提取策略
            string extractedContent;
            if (prompt.Contains("标题", StringComparison.OrdinalIgnoreCase) || 
                prompt.Contains("title", StringComparison.OrdinalIgnoreCase))
            {
                extractedContent = ExtractTitle(cleanedHtml);
            }
            else if (prompt.Contains("元数据", StringComparison.OrdinalIgnoreCase) || 
                     prompt.Contains("meta", StringComparison.OrdinalIgnoreCase))
            {
                extractedContent = ExtractMetaInformation(cleanedHtml);
            }
            else if (prompt.Contains("链接", StringComparison.OrdinalIgnoreCase) || 
                     prompt.Contains("link", StringComparison.OrdinalIgnoreCase))
            {
                extractedContent = ExtractLinks(cleanedHtml);
            }
            else if (prompt.Contains("结构", StringComparison.OrdinalIgnoreCase) || 
                     prompt.Contains("structure", StringComparison.OrdinalIgnoreCase))
            {
                extractedContent = AnalyzeStructureAsync(cleanedHtml).Result;
            }
            else
            {
                // 默认提取主要内容
                extractedContent = ExtractMainContentAsync(cleanedHtml).Result;
                // 限制结果长度
                if (extractedContent.Length > 2000)
                {
                    extractedContent = string.Concat(extractedContent.AsSpan(0, 2000), "...[内容过长，已截断]");
                }
            }

            _logger.LogInformation("Successfully extracted content with prompt, result length: {Length}", extractedContent.Length);
            return Task.FromResult(extractedContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract content with prompt: {Prompt}", prompt);
            return Task.FromResult(string.Empty);
        }
    }

    public Task<string> AnalyzeStructureAsync(string htmlContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                _logger.LogWarning("Empty HTML content provided for structure analysis");
                return Task.FromResult(string.Empty);
            }

            _logger.LogDebug("Analyzing HTML structure");

            // 移除脚本和样式
            var cleanedHtml = ScriptStyleRegex().Replace(htmlContent, string.Empty);

            // 分析HTML结构
            var structureBuilder = new StringBuilder();
            structureBuilder.AppendLine("# HTML结构分析报告");
            structureBuilder.AppendLine();

            // 1. 提取标题
            var title = ExtractTitle(cleanedHtml);
            if (!string.IsNullOrWhiteSpace(title))
            {
                structureBuilder.AppendLine($"## 页面标题");
                structureBuilder.AppendLine($"- {title}");
                structureBuilder.AppendLine();
            }

            // 2. 分析元数据
            var metaInfo = ExtractMetaInformation(cleanedHtml);
            if (!string.IsNullOrWhiteSpace(metaInfo))
            {
                structureBuilder.AppendLine($"## 元数据");
                structureBuilder.AppendLine(metaInfo);
                structureBuilder.AppendLine();
            }

            // 3. 分析内容结构
            structureBuilder.AppendLine($"## 内容结构");
            
            // 统计段落
            var paragraphs = ExtractParagraphs(cleanedHtml);
            structureBuilder.AppendLine($"- 段落数量: {paragraphs.Count}");
            
            // 统计标题层级
            var headings = ExtractHeadings(cleanedHtml);
            foreach (var heading in headings)
            {
                structureBuilder.AppendLine($"- {heading.Key}标题: {heading.Value}个");
            }
            
            // 统计链接数量
            var links = ExtractLinksCount(cleanedHtml);
            structureBuilder.AppendLine($"- 链接数量: {links}");
            
            // 4. 内容摘要
            var summary = ExtractMainContentAsync(cleanedHtml).Result;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                structureBuilder.AppendLine();
                structureBuilder.AppendLine($"## 内容摘要");
                structureBuilder.AppendLine(summary.Length > 500 ? string.Concat(summary.AsSpan(0, 500), "...") : summary);
            }

            var result = structureBuilder.ToString();
            _logger.LogInformation("Successfully analyzed HTML structure");
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze HTML structure");
            return Task.FromResult(string.Empty);
        }
    }

    public Task<bool> IsAvailableAsync()
    {
        // 模拟AI服务可用性检查
        var available = _isAvailable && _isInitialized;
        _logger.LogDebug("AI service availability: {Available}", available);
        return Task.FromResult(available);
    }

    #region Private Helper Methods

    private static string CleanTextContent(string text)
    {
        // 标准化换行符
        text = NewlineRegex().Replace(text, "\n");
        
        // 移除多余的换行符
        text = ExtraNewlineRegex().Replace(text, "\n\n");
        
        // 移除多余的空格
        text = ExtraWhitespaceRegex().Replace(text, " ");
        
        // 移除行首行尾空格
        var lines = text.Split(['\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0);
        
        return string.Join("\n", lines);
    }

    private static string ExtractTitle(string htmlContent)
    {
        var match = TitleTagRegex().Match(htmlContent);
        if (match.Success)
        {
            return HtmlTagRegex().Replace(match.Groups[2].Value, string.Empty).Trim();
        }
        return string.Empty;
    }

    private static string ExtractMetaInformation(string htmlContent)
    {
        var matches = MetaTagRegex().Matches(htmlContent);
        var metaInfo = new StringBuilder();
        
        foreach (Match match in matches)
        {
            var tagContent = match.Value;
            if (tagContent.Contains("description", StringComparison.OrdinalIgnoreCase))
            {
                var contentMatch = ContentRegex().Match(tagContent);
                if (contentMatch.Success)
                {
                    metaInfo.AppendLine($"- 描述: {contentMatch.Groups[1].Value}");
                }
            }
            else if (tagContent.Contains("keywords", StringComparison.OrdinalIgnoreCase))
            {
                var contentMatch = ContentRegex().Match(tagContent);
                if (contentMatch.Success)
                {
                    metaInfo.AppendLine($"- 关键词: {contentMatch.Groups[1].Value}");
                }
            }
            else if (tagContent.Contains("title", StringComparison.OrdinalIgnoreCase))
            {
                var contentMatch = ContentRegex().Match(tagContent);
                if (contentMatch.Success)
                {
                    metaInfo.AppendLine($"- 标题: {contentMatch.Groups[1].Value}");
                }
            }
        }
        
        return metaInfo.ToString().Trim();
    }

    private static List<string> ExtractParagraphs(string htmlContent)
    {
        var text = HtmlTagRegex().Replace(htmlContent, string.Empty);
        var paragraphs = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 50) // 过滤太短的段落
            .ToList();
        
        return paragraphs;
    }

    private static Dictionary<string, int> ExtractHeadings(string htmlContent)
    {
        var headings = new Dictionary<string, int> { { "H1", 0 }, { "H2", 0 }, { "H3", 0 }, { "H4", 0 }, { "H5", 0 }, { "H6", 0 } };
        
        var matches = HeadingRegex().Matches(htmlContent);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1 && int.TryParse(match.Groups[1].Value, out int level))
            {
                if (level >= 1 && level <= 6)
                {
                    headings[$"H{level}"]++;
                }
            }
        }
        
        return headings;
    }

    private static int ExtractLinksCount(string htmlContent)
    {
        return LinkTagRegex().Matches(htmlContent).Count;
    }

    private static string ExtractLinks(string htmlContent)
    {
        var matches = LinkWithTextRegex().Matches(htmlContent);
        
        var linksBuilder = new StringBuilder();
        int count = 0;
        
        foreach (Match match in matches.Take(20)) // 限制最多20个链接
        {
            var url = match.Groups[1].Value;
            var text = HtmlTagRegex().Replace(match.Groups[2].Value, string.Empty).Trim();
            
            if (!string.IsNullOrWhiteSpace(url))
            {
                linksBuilder.AppendLine($"- [{text}]({url})");
                count++;
            }
        }
        
        if (matches.Count > 20)
        {
            linksBuilder.AppendLine($"- ... 还有 {matches.Count - 20} 个链接未显示");
        }
        
        return linksBuilder.ToString().Trim();
    }

    #endregion
}