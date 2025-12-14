// CrawlerCore/AI/AIAssistedHelper.cs
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CrawlerCore.AI;

/// <summary>
/// AI辅助解析器实现类
/// </summary>
public partial class AIAssistedHelper(ILogger<AIAssistedHelper> logger) : IAIHelper
{
    private readonly ILogger<AIAssistedHelper> _logger = logger;
    private bool _isInitialized = false;
    private readonly bool _isAvailable = true;

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+\n\s+")]
    private static partial Regex ExtraNewlineRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ExtraWhitespaceRegex();

    public Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            _logger.LogInformation("AIAssistedHelper initialized successfully");
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
            // 移除HTML标签
            var text = HtmlTagRegex().Replace(htmlContent, "");
            // 移除多余空格和换行
            text = ExtraNewlineRegex().Replace(text, "\n");
            text = ExtraWhitespaceRegex().Replace(text, " ");
            // 简单的内容提取逻辑，可以后续替换为真正的AI模型
            return Task.FromResult(text.Trim());
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
            // 移除HTML标签
            var text = HtmlTagRegex().Replace(htmlContent, "");
            // 简单的基于提示的提取，可以后续替换为真正的AI模型
            _logger.LogInformation("Extracting content with prompt: {Prompt}", prompt);
            return Task.FromResult(string.Concat(text.AsSpan(0, Math.Min(1000, text.Length)), "..."));
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
            // 简单的结构分析，可以后续替换为真正的AI模型
            var structure = "<!DOCTYPE html>\n";
            structure += "<html>\n";
            structure += "<head>\n";
            structure += "    <title>...</title>\n";
            structure += "</head>\n";
            structure += "<body>\n";
            structure += "    <header>...</header>\n";
            structure += "    <main>...</main>\n";
            structure += "    <footer>...</footer>\n";
            structure += "</body>\n";
            structure += "</html>";
            
            return Task.FromResult(structure);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze HTML structure");
            return Task.FromResult(string.Empty);
        }
    }

    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(_isAvailable);
    }
}