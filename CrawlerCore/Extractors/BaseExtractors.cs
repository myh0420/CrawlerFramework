// <copyright file="BaseExtractors.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Extractors;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CrawlerEntity.Models;
using CrawlerInterFaces.Interfaces;
using HtmlAgilityPack;

/// <summary>
/// 链接提取器.
/// </summary>
public class LinkExtractor : IContentExtractor, IPlugin
{
    /// <inheritdoc/>
    public string Name => "LinkExtractor";

    // IPlugin接口实现

    /// <inheritdoc/>
    public string PluginName => "LinkExtractor";

    /// <inheritdoc/>
    public string Version => "1.0.0";

    /// <inheritdoc/>
    public string Description => "链接提取器插件，用于从HTML文档中提取链接";

    /// <inheritdoc/>
    public PluginType PluginType => PluginType.Extractor;

    /// <inheritdoc/>
    public string Author => "CrawlerFramework Team";

    /// <inheritdoc/>
    public Type EntryPointType => typeof(LinkExtractor);

    /// <inheritdoc/>
    public int Priority => 5;

    // ICrawlerComponent接口实现

    /// <inheritdoc/>
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ShutdownAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ExtractionResult> ExtractAsync(HtmlDocument htmlDocument, DownloadResult downloadResult)
    {
        var result = new ExtractionResult();

        if (htmlDocument?.DocumentNode == null)
        {
            return Task.FromResult(result);
        }

        try
        {
            // 提取所有a标签的href
            var linkNodes = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            if (linkNodes != null)
            {
                foreach (var node in linkNodes)
                {
                    var href = node.GetAttributeValue("href", string.Empty).Trim();
                    if (!string.IsNullOrEmpty(href))
                    {
                        // 转换为绝对URL
                        var absoluteUrl = ToAbsoluteUrl(downloadResult.Url, href);
                        if (absoluteUrl != null)
                        {
                            result.Links.Add(absoluteUrl);
                        }
                    }
                }
            }

            // 提取图片链接
            var imgNodes = htmlDocument.DocumentNode.SelectNodes("//img[@src]");
            if (imgNodes != null)
            {
                foreach (var node in imgNodes)
                {
                    var src = node.GetAttributeValue("src", string.Empty).Trim();
                    if (!string.IsNullOrEmpty(src))
                    {
                        var absoluteUrl = ToAbsoluteUrl(downloadResult.Url, src);
                        if (absoluteUrl != null)
                        {
                            result.Data[$"Image_{result.Data.Count}"] = absoluteUrl;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 记录错误但继续处理
            result.Data["LinkExtractionError"] = ex.Message;
        }

        return Task.FromResult(result);
    }

    private static string? ToAbsoluteUrl(string baseUrl, string relativeUrl)
    {
        try
        {
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                if (Uri.TryCreate(baseUri, relativeUrl, out var absoluteUri))
                {
                    return absoluteUri.ToString();
                }
            }
        }
        catch
        {
            // 忽略URL转换错误
        }

        return null;
    }
}

/// <summary>
/// 元数据提取器.
/// </summary>
public class MetadataExtractor : IContentExtractor, IPlugin
{
    /// <inheritdoc/>
    public string Name => "MetadataExtractor";

    // IPlugin接口实现

    /// <inheritdoc/>
    public string PluginName => "MetadataExtractor";

    /// <inheritdoc/>
    public string Version => "1.0.0";

    /// <inheritdoc/>
    public string Description => "元数据提取器插件，用于从HTML文档中提取元数据";

    /// <inheritdoc/>
    public PluginType PluginType => PluginType.Extractor;

    /// <inheritdoc/>
    public string Author => "CrawlerFramework Team";

    /// <inheritdoc/>
    public Type EntryPointType => typeof(MetadataExtractor);

    /// <inheritdoc/>
    public int Priority => 5;

    // ICrawlerComponent接口实现

    /// <inheritdoc/>
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ShutdownAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ExtractionResult> ExtractAsync(HtmlDocument htmlDocument, DownloadResult downloadResult)
    {
        var result = new ExtractionResult();

        if (htmlDocument?.DocumentNode == null)
        {
            return Task.FromResult(result);
        }

        try
        {
            // 提取meta标签
            var metaNodes = htmlDocument.DocumentNode.SelectNodes("//meta");
            if (metaNodes != null)
            {
                foreach (var node in metaNodes)
                {
                    var name = node.GetAttributeValue("name", string.Empty) ??
                              node.GetAttributeValue("property", string.Empty);
                    var content = node.GetAttributeValue("content", string.Empty);

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(content))
                    {
                        result.Data[$"Meta_{name}"] = content;
                    }
                }
            }

            // 提取标题
            var titleNode = htmlDocument.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null)
            {
                result.Data["Title"] = titleNode.InnerText.Trim();
            }

            // 提取描述
            var descriptionNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@name='description']");
            if (descriptionNode != null)
            {
                result.Data["Description"] = descriptionNode.GetAttributeValue("content", string.Empty).Trim();
            }

            // 提取关键词
            var keywordsNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
            if (keywordsNode != null)
            {
                result.Data["Keywords"] = keywordsNode.GetAttributeValue("content", string.Empty).Trim();
            }
        }
        catch (Exception ex)
        {
            result.Data["MetadataExtractionError"] = ex.Message;
        }

        return Task.FromResult(result);
    }
}

/// <summary>
/// 内容提取器.
/// </summary>
public partial class ContentExtractor : IContentExtractor, IPlugin
{
    /// <inheritdoc/>
    public string Name => "ContentExtractor";

    // IPlugin接口实现

    /// <inheritdoc/>
    public string PluginName => "ContentExtractor";

    /// <inheritdoc/>
    public string Version => "1.0.0";

    /// <inheritdoc/>
    public string Description => "内容提取器插件，用于从HTML文档中提取正文内容";

    /// <inheritdoc/>
    public PluginType PluginType => PluginType.Extractor;

    /// <inheritdoc/>
    public string Author => "CrawlerFramework Team";

    /// <inheritdoc/>
    public Type EntryPointType => typeof(ContentExtractor);

    /// <inheritdoc/>
    public int Priority => 5;

    // ICrawlerComponent接口实现

    /// <inheritdoc/>
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ShutdownAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ExtractionResult> ExtractAsync(HtmlDocument htmlDocument, DownloadResult downloadResult)
    {
        var result = new ExtractionResult();

        if (htmlDocument?.DocumentNode == null)
        {
            return Task.FromResult(result);
        }

        try
        {
            // 移除脚本和样式
            var nodesToRemove = htmlDocument.DocumentNode
                .SelectNodes("//script | //style | //noscript | //comment()");

            nodesToRemove?.ToList().ForEach(n => n.Remove());

            // 提取正文文本
            var bodyNode = htmlDocument.DocumentNode.SelectSingleNode("//body");
            if (bodyNode != null)
            {
                var text = bodyNode.InnerText;
                text = SpaceRegex().Replace(text, " ").Trim();
                result.Data["BodyText"] = text;

                // 计算文本长度
                result.Data["TextLength"] = text.Length;

                // 提取前200个字符作为摘要
                result.Data["Summary"] = text.Length > 200 ? string.Concat(text.AsSpan(0, 200), "...") : text;
            }

            // 提取H1-H6标题
            for (int i = 1; i <= 6; i++)
            {
                var headingNodes = htmlDocument.DocumentNode.SelectNodes($"//h{i}");
                if (headingNodes != null)
                {
                    var headings = headingNodes.Select(h => h.InnerText.Trim()).ToList();
                    if (headings.Count != 0)
                    {
                        result.Data[$"H{i}Headings"] = headings;
                    }
                }
            }

            // 提取段落数量
            var paragraphNodes = htmlDocument.DocumentNode.SelectNodes("//p");
            if (paragraphNodes != null)
            {
                result.Data["ParagraphCount"] = paragraphNodes.Count;
            }
        }
        catch (Exception ex)
        {
            result.Data["ContentExtractionError"] = ex.Message;
        }

        return Task.FromResult(result);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();
}