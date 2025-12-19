// <copyright file="AIAssistedExtractor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Extractors;

using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

/// <summary>
/// AI辅助内容提取器.
/// </summary>
public class AIAssistedExtractor : IContentExtractor, IPlugin
{
    /// <summary>
    /// AI辅助帮助器实例，用于执行AI辅助提取操作.
    /// </summary>
    private readonly IAIHelper aiHelper;

    /// <summary>
    /// 日志记录器实例，用于记录提取操作的日志.
    /// </summary>
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIAssistedExtractor"/> class.
    /// 初始化 <see cref="AIAssistedExtractor"/> 类的新实例.
    /// </summary>
    /// <param name="aiHelper">AI辅助帮助器实例.</param>
    /// <param name="logger">日志记录器实例.</param>
    public AIAssistedExtractor(IAIHelper aiHelper, ILogger logger)
    {
        this.aiHelper = aiHelper;
        this.logger = logger;
    }

    /// <summary>
    /// Gets 提取器名称.
    /// </summary>
    public string Name => "ai-assistant";

    // IPlugin implementation

    /// <inheritdoc/>
    /// <summary>
    /// 获取插件名称.
    /// </summary>
    public string PluginName => "AIAssistedExtractor";

    /// <inheritdoc/>
    /// <summary>
    /// 获取插件版本.
    /// </summary>
    public string Version => "1.0.0";

    /// <inheritdoc/>
    /// <summary>
    /// 获取插件描述.
    /// </summary>
    public string Description => "AI辅助内容提取器插件，使用AI技术提取和分析网页内容";

    /// <inheritdoc/>
    /// <summary>
    /// 获取插件类型.
    /// </summary>
    public PluginType PluginType => PluginType.Extractor;

    /// <inheritdoc/>
    /// <summary>
    /// 获取插件作者.
    /// </summary>
    public string Author => "CrawlerFramework Team";

    /// <inheritdoc/>
    /// <summary>
    /// 获取插件入口点类型.
    /// </summary>
    public Type EntryPointType => typeof(AIAssistedExtractor);

    /// <inheritdoc/>
    /// <summary>
    /// 获取插件优先级.
    /// </summary>
    public int Priority => 5;

    // ICrawlerComponent implementation

    /// <inheritdoc/>
    /// <summary>
    /// 初始化组件.
    /// </summary>
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    /// <summary>
    /// 关闭组件.
    /// </summary>
    public Task ShutdownAsync() => Task.CompletedTask;

    /// <summary>
    /// 异步提取AI辅助内容.
    /// </summary>
    /// <param name="htmlDocument">HTML文档.</param>
    /// <param name="downloadResult">下载结果.</param>
    /// <returns>提取结果.</returns>
    public async Task<ExtractionResult> ExtractAsync(HtmlDocument htmlDocument, DownloadResult downloadResult)
    {
        try
        {
            var htmlContent = htmlDocument.DocumentNode.OuterHtml;
            var url = downloadResult.Url;

            // 使用AI辅助提取主要内容
            var mainContent = await this.aiHelper.ExtractMainContentAsync(htmlContent, url);

            // 使用AI辅助分析结构
            var structure = await this.aiHelper.AnalyzeStructureAsync(htmlContent);

            var result = new ExtractionResult
            {
                Links = [],
                Data = new ()
                {
                    ["mainContent"] = mainContent,
                    ["structure"] = structure,
                    ["isAIAssisted"] = true,
                },
            };

            this.logger.LogInformation("AI-assisted extraction completed for {Url}", url);
            return result;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "AI-assisted extraction failed for {Url}", downloadResult.Url);
            return new ExtractionResult();
        }
    }
}