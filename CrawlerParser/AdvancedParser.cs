// <copyright file="AdvancedParser.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerParser;

using System.Diagnostics;
using CrawlerCore.Extractors;
using CrawlerCore.Metrics;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

/// <summary>
/// 高级解析器，支持多种内容提取器和AI辅助解析.
/// </summary>
public partial class AdvancedParser : IParser, IPlugin
{
    /// <summary>
    /// HTML文档对象，用于解析HTML内容.
    /// </summary>
    private readonly HtmlDocument htmlDocument;

    /// <summary>
    /// 提取器字典，键为提取器名称，值为提取器实例.
    /// </summary>
    private readonly Dictionary<string, IContentExtractor> extractors;

    /// <summary>
    /// 日志记录器.
    /// </summary>
    private readonly ILogger<AdvancedParser> logger;

    /// <summary>
    /// AI助手.
    /// </summary>
    private readonly IAIHelper? aiHelper;

    /// <summary>
    /// 指标服务.
    /// </summary>
    private readonly CrawlerMetrics? metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvancedParser"/> class.
    /// 构造函数，初始化HTML文档对象、提取器字典和日志记录器、AI助手和指标服务.
    /// </summary>
    /// <param name="logger">日志记录器.</param>
    /// <param name="aiHelper">AI助手.</param>
    /// <param name="metrics">指标服务.</param>
    public AdvancedParser(ILogger<AdvancedParser> logger, IAIHelper? aiHelper = null, CrawlerMetrics? metrics = null)
    {
        this.htmlDocument = new();
        this.extractors = [];
        this.logger = logger;
        this.aiHelper = aiHelper;
        this.metrics = metrics;

        this.InitializeDefaultExtractors();
    }

    // IPlugin接口实现

    /// <summary>
    /// Gets 插件名称.
    /// </summary>
    public string PluginName => "AdvancedParser";

    /// <summary>
    /// Gets 插件版本.
    /// </summary>
    public string Version => "1.0.0";

    /// <summary>
    /// Gets 插件描述.
    /// </summary>
    public string Description => "高级解析器插件，支持多种内容提取器和AI辅助解析";

    /// <summary>
    /// Gets 插件类型.
    /// </summary>
    public PluginType PluginType => PluginType.Parser;

    /// <summary>
    /// Gets 插件作者.
    /// </summary>
    public string Author => "CrawlerFramework Team";

    /// <summary>
    /// Gets 插件入口点类型.
    /// </summary>
    public Type EntryPointType => typeof(AdvancedParser);

    /// <summary>
    /// 异步解析下载结果，提取链接、元数据和内容.
    /// </summary>
    /// <param name="downloadResult">下载结果，包含URL、内容类型和内容.</param>
    /// <returns>解析结果，包含提取的链接、元数据和内容.</returns>
    public async Task<ParseResult> ParseAsync(DownloadResult downloadResult)
    {
        var result = new ParseResult();
        var stopwatch = Stopwatch.StartNew();

        // 创建OpenTelemetry追踪
        var tracer = TracerProvider.Default.GetTracer("CrawlerParser");
        using var span = tracer.StartActiveSpan("ParseAsync", SpanKind.Internal);

        if (downloadResult != null)
        {
            span.SetAttribute("url", downloadResult.Url);
            span.SetAttribute("content_type", downloadResult.ContentType ?? "unknown");
            span.SetAttribute("request_id", downloadResult.RequestId ?? "unknown");
        }

        try
        {
            // 参数验证
            if (downloadResult == null)
            {
                this.logger.LogWarning("ParseAsync called with null downloadResult");
                span.SetAttribute("error", true);
                span.SetAttribute("error.message", "Null downloadResult");
                return result;
            }

            if (string.IsNullOrEmpty(downloadResult.Content))
            {
                this.logger.LogWarning("ParseAsync called with empty content for {Url}", downloadResult.Url);
                span.SetAttribute("error", true);
                span.SetAttribute("error.message", "Empty content");
                return result;
            }

            if (downloadResult.ContentType?.StartsWith("text/html") == true)
            {
                span.SetAttribute("parse.mode", "html");
                this.htmlDocument.LoadHtml(downloadResult.Content);

                // 并行执行所有提取器
                var extractorTasks = this.extractors.Values
                    .Select(extractor => extractor.ExtractAsync(this.htmlDocument, downloadResult))
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
                result.Title = this.ExtractTitle();
                result.TextContent = this.ExtractTextContent();
            }
            else if (downloadResult.ContentType?.StartsWith("text/") == true)
            {
                span.SetAttribute("parse.mode", "text");

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
                span.SetAttribute("parse.mode", "json");

                // 处理JSON内容
                try
                {
                    result.TextContent = downloadResult.Content;
                    result.ExtractedData["json"] = downloadResult.Content;
                    result.Title = "JSON Data";
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to parse JSON content for {Url}", downloadResult.Url);
                    span.SetAttribute("error", true);
                    span.SetAttribute("error.message", "JSON parsing failed");
                    span.SetAttribute("error.type", "JSONParseException");
                    result.TextContent = downloadResult.Content;
                    result.ExtractedData["raw"] = downloadResult.Content;
                }
            }
            else
            {
                span.SetAttribute("parse.mode", "raw");

                // 处理其他内容类型，作为原始数据保存
                result.ExtractedData["raw"] = downloadResult.Content;
                result.Title = $"{downloadResult.ContentType} Data";
            }

            result.IsSuccess = true;
            span.SetAttribute("success", true);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Parsing failed for {Url}", downloadResult?.Url);
            span.SetAttribute("error", true);
            span.SetAttribute("error.message", ex.Message);
            span.SetAttribute("error.type", ex.GetType().Name);
            span.RecordException(ex);
        }
        finally
        {
            stopwatch.Stop();
            result.ParseTimeMs = stopwatch.ElapsedMilliseconds;
            span.SetAttribute("duration_ms", stopwatch.Elapsed.TotalMilliseconds);

            // 记录解析时间指标
            if (downloadResult != null && this.metrics != null)
            {
                this.metrics.RecordParseDuration(downloadResult.Url, stopwatch.ElapsedMilliseconds);
            }
        }

        return result;
    }

    /// <summary>
    /// 添加自定义提取器.
    /// </summary>
    /// <param name="name">提取器名称.</param>
    /// <param name="extractor">提取器实例.</param>
    public void AddExtractor(string name, IContentExtractor extractor)
    {
        this.extractors[name] = extractor;
    }

    /// <summary>
    /// 异步初始化解析器，当前实现为空.
    /// </summary>
    /// <returns>已完成任务.</returns>
    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// 异步关闭解析器，当前实现为空.
    /// </summary>
    /// <returns>已完成任务.</returns>
    public Task ShutdownAsync() => Task.CompletedTask;

    /// <summary>
    /// 提取HTML文档的文本内容，移除脚本和样式标签
    /// </summary>
    /// <returns>文本内容</returns>
    // 使用GeneratedRegexAttribute在编译时生成正则表达式
    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex WhitespaceRegex();

    /// <summary>
    /// 初始化默认提取器，包括链接提取器、元数据提取器和内容提取器.
    /// </summary>
    private void InitializeDefaultExtractors()
    {
        this.AddExtractor("links", new LinkExtractor());
        this.AddExtractor("metadata", new MetadataExtractor());
        this.AddExtractor("content", new ContentExtractor());

        // 如果AI助手可用，添加AI辅助提取器
        if (this.aiHelper != null)
        {
            this.AddExtractor("ai-assistant", new AIAssistedExtractor(this.aiHelper, this.logger));
        }
    }

    /// <summary>
    /// 提取HTML文档的标题.
    /// </summary>
    /// <returns>标题文本.</returns>
    private string ExtractTitle()
    {
        return this.htmlDocument.DocumentNode
            .SelectSingleNode("//title")?
            .InnerText?
            .Trim() ?? string.Empty;
    }

    /// <summary>
    /// 提取HTML文档的文本内容，移除脚本和样式标签.
    /// </summary>
    /// <returns>文本内容.</returns>
    private string ExtractTextContent()
    {
        // 创建文档副本，避免修改原始文档
        var documentCopy = new HtmlDocument();
        documentCopy.LoadHtml(this.htmlDocument.DocumentNode.OuterHtml);

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
}