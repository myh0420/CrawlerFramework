// <copyright file="AIAssistedHelper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
namespace CrawlerFramework.CrawlerCore.AI;

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CrawlerFramework.CrawlerInterFaces.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

/// <summary>
/// AI辅助解析器实现类.
/// </summary>
public partial class AIAssistedHelper : IAIHelper, IDisposable
{
    /// <summary>
    /// 缓存过期时间（毫秒）.
    /// </summary>
    private const int CacheExpirationMs = 300000; // 5分钟

    /// <summary>
    /// 主要内容选择器.
    /// </summary>
    private static readonly string[] MainContentSelectors = [
        "//main",
        "//article",
        "//div[contains(@class, 'main') or contains(@class, 'content') or contains(@class, 'article') or contains(@class, 'post')]",
        "//section[contains(@class, 'main') or contains(@class, 'content') or contains(@class, 'article') or contains(@class, 'post')]"
    ];

    /// <summary>
    /// 日志记录器实例.
    /// </summary>
    private readonly ILogger<AIAssistedHelper> logger;

    /// <summary>
    /// 是否可用.
    /// </summary>
    private readonly bool isAvailable = true;

    /// <summary>
    /// HTML文档缓存.
    /// </summary>
    private readonly ConcurrentDictionary<string, CacheItem<HtmlDocument>> htmlDocumentCache = new ();

    /// <summary>
    /// 提取结果缓存.
    /// </summary>
    private readonly ConcurrentDictionary<string, CacheItem<object>> extractionResultCache = new ();

    /// <summary>
    /// 是否已释放资源.
    /// </summary>
    private bool disposed;

    /// <summary>
    /// 是否已初始化.
    /// </summary>
    private bool isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIAssistedHelper"/> class.
    /// 初始化 <see cref="AIAssistedHelper"/> 类的新实例.
    /// </summary>
    /// <param name="logger">日志记录器实例.</param>
    public AIAssistedHelper(ILogger<AIAssistedHelper> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger), "日志记录器参数不能为空");
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="AIAssistedHelper"/> class.
    /// 终结器，确保资源被释放.
    /// </summary>
    ~AIAssistedHelper()
    {
        Dispose(false);
    }

    /// <summary>
    /// 初始化AI辅助解析器.
    /// </summary>
    /// <returns>异步任务.</returns>
    public Task InitializeAsync()
    {
        if (!this.isInitialized)
        {
            this.isInitialized = true;
            this.logger.LogInformation("AIAssistedHelper initialized successfully");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 关闭AI辅助解析器.
    /// </summary>
    /// <returns>异步任务.</returns>
    public Task ShutdownAsync()
    {
        if (this.isInitialized)
        {
            this.isInitialized = false;
            this.logger.LogInformation("AIAssistedHelper shutdown successfully");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 从HTML内容中提取主要内容.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>主要内容文本.</returns>
    public async Task<string> ExtractMainContentAsync(string htmlContent, string url = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                this.logger.LogWarning("Empty HTML content provided for extraction. URL: {Url}", url);
                return string.Empty;
            }

            // 尝试从缓存获取结果
            var cachedResult = this.GetCachedExtractionResult(htmlContent, "main_content", url);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            this.logger.LogDebug("Extracting main content from HTML with length: {Length}. URL: {Url}", htmlContent.Length, url);

            // 使用Task.Run在后台线程执行耗时的HTML处理
            var text = await Task.Run(() =>
            {
                // 获取或创建HTML文档
                var doc = this.GetOrCreateHtmlDocument(htmlContent, url);

                // 创建文档副本以避免修改原始缓存文档
                var docCopy = new HtmlDocument();
                docCopy.LoadHtml(doc.DocumentNode.OuterHtml);

                // 1. 移除脚本、样式和iframe等不需要的内容
                RemoveUnnecessaryContent(docCopy);

                // 2. 尝试提取主要内容块
                string mainContent = string.Empty;

                foreach (var selector in MainContentSelectors)
                {
                    var mainContentNode = doc.DocumentNode.SelectSingleNode(selector);
                    if (mainContentNode != null && !string.IsNullOrWhiteSpace(mainContentNode.InnerText))
                    {
                        mainContent = mainContentNode.InnerText;
                        this.logger.LogDebug("Found main content using selector: {Selector}. URL: {Url}", selector, url);
                        break;
                    }
                }

                // 如果没有找到主要内容块，使用整个文档内容
                if (string.IsNullOrWhiteSpace(mainContent))
                {
                    mainContent = doc.DocumentNode.InnerText;
                    this.logger.LogDebug("Using entire document content as main content. URL: {Url}", url);
                }

                // 3. 清理文本内容
                var cleanedText = CleanTextContent(mainContent);

                // 4. 提取并保留标题
                var title = this.ExtractTitle(htmlContent, url);
                if (!string.IsNullOrWhiteSpace(title) && !cleanedText.StartsWith(title))
                {
                    cleanedText = $"{title}\n\n{cleanedText}";
                }

                return cleanedText;
            });

            // 存入缓存
            this.CacheExtractionResult(htmlContent, "main_content", text, url);

            this.logger.LogInformation("Successfully extracted main content with length: {Length}. URL: {Url}", text.Length, url);
            return text;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to extract main content from HTML. Content length: {Length}, URL: {Url}", htmlContent?.Length ?? 0, url);
            return string.Empty;
        }
    }

    /// <summary>
    /// 从HTML内容中提取内容根据提示.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="prompt">提取提示.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取的内容文本.</returns>
    public async Task<string> ExtractWithPromptAsync(string htmlContent, string prompt, string url = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                this.logger.LogWarning("Empty HTML content provided for prompt extraction");
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                this.logger.LogWarning("Empty prompt provided for extraction");
                return string.Empty;
            }

            this.logger.LogInformation("Extracting content with prompt: {Prompt}", prompt);

            // 根据提示类型执行不同的提取策略
            string extractedContent;
            if (prompt.Contains("标题", StringComparison.OrdinalIgnoreCase) ||
                prompt.Contains("title", StringComparison.OrdinalIgnoreCase))
            {
                // 使用Task.Run在后台线程执行CPU绑定操作
                extractedContent = await Task.Run(() =>
                {
                    var doc = this.GetOrCreateHtmlDocument(htmlContent, url);
                    var docCopy = new HtmlDocument();
                    docCopy.LoadHtml(doc.DocumentNode.OuterHtml);
                    RemoveUnnecessaryContent(docCopy);
                    return this.ExtractTitle(docCopy.DocumentNode.OuterHtml, url);
                });
            }
            else if (prompt.Contains("元数据", StringComparison.OrdinalIgnoreCase) ||
                     prompt.Contains("meta", StringComparison.OrdinalIgnoreCase))
            {
                // 使用Task.Run在后台线程执行CPU绑定操作
                extractedContent = await Task.Run(() =>
                {
                    var doc = this.GetOrCreateHtmlDocument(htmlContent, url);
                    var docCopy = new HtmlDocument();
                    docCopy.LoadHtml(doc.DocumentNode.OuterHtml);
                    RemoveUnnecessaryContent(docCopy);
                    return this.ExtractMetaInformation(docCopy.DocumentNode.OuterHtml, url);
                });
            }
            else if (prompt.Contains("链接", StringComparison.OrdinalIgnoreCase) ||
                     prompt.Contains("link", StringComparison.OrdinalIgnoreCase))
            {
                // 使用Task.Run在后台线程执行CPU绑定操作
                extractedContent = await Task.Run(() =>
                {
                    var doc = this.GetOrCreateHtmlDocument(htmlContent, url);
                    var docCopy = new HtmlDocument();
                    docCopy.LoadHtml(doc.DocumentNode.OuterHtml);
                    RemoveUnnecessaryContent(docCopy);
                    return this.ExtractLinks(docCopy.DocumentNode.OuterHtml, url);
                });
            }
            else if (prompt.Contains("结构", StringComparison.OrdinalIgnoreCase) ||
                     prompt.Contains("structure", StringComparison.OrdinalIgnoreCase))
            {
                // 结构分析已经是异步方法
                extractedContent = await this.AnalyzeStructureAsync(htmlContent, url);
            }
            else
            {
                // 默认提取主要内容
                extractedContent = await this.ExtractMainContentAsync(htmlContent, url);

                // 限制结果长度
                if (extractedContent.Length > 2000)
                {
                    extractedContent = string.Concat(extractedContent.AsSpan(0, 2000), "...[内容过长，已截断]");
                }
            }

            this.logger.LogInformation("Successfully extracted content with prompt, result length: {Length}", extractedContent.Length);
            return extractedContent;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to extract content with prompt: {Prompt}", prompt);
            return string.Empty;
        }
    }

    /// <summary>
    /// 分析HTML结构.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>HTML结构分析报告.</returns>
    public async Task<string> AnalyzeStructureAsync(string htmlContent, string url = "")
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
        {
            this.logger.LogWarning("Empty HTML content provided for structure analysis");
            return string.Empty;
        }

        try
        {
            this.logger.LogDebug("Analyzing HTML structure");

            // 第一步：在后台线程执行CPU密集型的HTML处理
            var structureBuilder = await Task.Run(() =>
            {
                // 获取或创建HTML文档
                var doc = this.GetOrCreateHtmlDocument(htmlContent, url);

                // 创建文档副本以避免修改原始缓存文档
                var docCopy = new HtmlDocument();
                docCopy.LoadHtml(doc.DocumentNode.OuterHtml);

                // 移除脚本、样式和iframe等不需要的内容
                RemoveUnnecessaryContent(docCopy);

                var cleanedHtml = docCopy.DocumentNode.OuterHtml;

                // 分析HTML结构
                var builder = new StringBuilder();
                builder.AppendLine("# HTML结构分析报告");
                builder.AppendLine();

                // 1. 提取标题
                var title = this.ExtractTitle(cleanedHtml, url);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    builder.AppendLine($"## 页面标题");
                    builder.AppendLine($"- {title}");
                    builder.AppendLine();
                }

                // 2. 分析元数据
                var metaInfo = this.ExtractMetaInformation(cleanedHtml, url);
                if (!string.IsNullOrWhiteSpace(metaInfo))
                {
                    builder.AppendLine($"## 元数据");
                    builder.AppendLine(metaInfo);
                    builder.AppendLine();
                }

                // 3. 分析内容结构
                builder.AppendLine($"## 内容结构");

                // 统计段落
                var paragraphs = this.ExtractParagraphs(cleanedHtml, url);
                builder.AppendLine($"- 段落数量: {paragraphs.Count}");

                // 统计标题层级
                var headings = this.ExtractHeadings(cleanedHtml, url);
                foreach (var heading in headings)
                {
                    builder.AppendLine($"- {heading.Key}标题: {heading.Value}个");
                }

                // 统计链接数量
                var links = this.ExtractLinksCount(cleanedHtml, url);
                builder.AppendLine($"- 链接数量: {links}");

                return builder;
            });

            // 第二步：执行异步的内容摘要提取
            var summary = await this.ExtractMainContentAsync(htmlContent, url);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                structureBuilder.AppendLine();
                structureBuilder.AppendLine($"## 内容摘要");
                structureBuilder.AppendLine(summary.Length > 500 ? string.Concat(summary.AsSpan(0, 500), "...") : summary);
            }

            var result = structureBuilder.ToString();
            this.logger.LogInformation("Successfully analyzed HTML structure");
            return result;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to analyze HTML structure");
            return string.Empty;
        }
    }

    /// <summary>
    /// 检查AI服务是否可用.
    /// </summary>
    /// <returns>如果AI服务可用则为true，否则为false.</returns>
    public Task<bool> IsAvailableAsync()
    {
        // 模拟AI服务可用性检查
        var available = this.isAvailable && this.isInitialized;
        this.logger.LogDebug("AI service availability: {Available}", available);
        return Task.FromResult(available);
    }

    /// <summary>
    /// 额外换行符正则表达式
    /// </summary>
    /// <returns>匹配额外换行符的正则表达式</returns>
    [GeneratedRegex(@"\s+\n\s+")]
    private static partial Regex ExtraNewlineRegex();

    /// <summary>
    /// 额外空格正则表达式
    /// </summary>
    /// <returns>匹配额外空格的正则表达式</returns>
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ExtraWhitespaceRegex();

    /// <summary>
    /// 换行符正则表达式
    /// </summary>
    /// <returns>匹配换行符的正则表达式</returns>
    [GeneratedRegex(@"(\r\n|\r|\n)")]
    private static partial Regex NewlineRegex();

    /// <summary>
    /// 移除HTML文档中不需要的内容.
    /// </summary>
    /// <param name="doc">HTML文档.</param>
    private static void RemoveUnnecessaryContent(HtmlDocument doc)
    {
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script | //style | //iframe | //noscript | //svg");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }
    }

    /// <summary>
    /// 生成缓存键.
    /// </summary>
    /// <param name="prefix">缓存键前缀.</param>
    /// <param name="content">内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>唯一的缓存键.</returns>
    private static string GenerateCacheKey(string prefix, string content, string url = "")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.MD5.HashData(bytes);
        var hashString = Convert.ToHexString(hash).ToLower();
        return string.IsNullOrEmpty(url) ? $"{prefix}:{hashString}" : $"{prefix}:{url}:{hashString}";
    }

    /// <summary>
    /// 清理文本内容，移除多余的换行符和空格.
    /// </summary>
    /// <param name="text">原始文本内容.</param>
    /// <returns>清理后的文本内容.</returns>
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

    /// <summary>
    /// 获取或创建HTML文档.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>HTML文档.</returns>
    private HtmlDocument GetOrCreateHtmlDocument(string htmlContent, string url = "")
    {
        // 首先尝试从缓存获取
        var cachedDoc = this.GetCachedHtmlDocument(htmlContent, url);
        if (cachedDoc != null)
        {
            return cachedDoc;
        }

        // 如果缓存中没有，创建新的HTML文档
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        // 存入缓存
        this.CacheHtmlDocument(htmlContent, doc, url);

        return doc;
    }

    /// <summary>
    /// 从缓存获取HTML文档.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>HTML文档或null.</returns>
    private HtmlDocument? GetCachedHtmlDocument(string htmlContent, string url = "")
    {
        var key = GenerateCacheKey("html_doc", htmlContent, url);
        if (this.htmlDocumentCache.TryGetValue(key, out var cacheItem))
        {
            if (cacheItem.IsExpired)
            {
                // 如果缓存过期，移除它
                this.htmlDocumentCache.TryRemove(key, out _);
                this.logger.LogDebug("HTML document cache expired, removed from cache");
                return null;
            }

            this.logger.LogDebug("Retrieved HTML document from cache");
            return cacheItem.Value;
        }

        return null;
    }

    /// <summary>
    /// 将HTML文档存入缓存.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="doc">HTML文档.</param>
    /// <param name="url">URL（可选）.</param>
    private void CacheHtmlDocument(string htmlContent, HtmlDocument doc, string url = "")
    {
        var key = GenerateCacheKey("html_doc", htmlContent, url);
        var cacheItem = new CacheItem<HtmlDocument>(doc, DateTime.UtcNow.AddMilliseconds(CacheExpirationMs));
        this.htmlDocumentCache.TryAdd(key, cacheItem);
        this.logger.LogDebug("Cached HTML document");
    }

    /// <summary>
    /// 从缓存获取提取结果.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="type">提取类型.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取结果或null.</returns>
    private string? GetCachedExtractionResult(string htmlContent, string type, string url = "")
    {
        return GetCachedExtractionResult<string>(htmlContent, type, url);
    }

    /// <summary>
    /// 从缓存获取提取结果（泛型版本）.
    /// </summary>
    /// <typeparam name="T">结果类型.</typeparam>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="type">提取类型.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取结果或null.</returns>
    private T? GetCachedExtractionResult<T>(string htmlContent, string type, string url = "")
    {
        var key = GenerateCacheKey($"extraction_{type}", htmlContent, url);
        if (this.extractionResultCache.TryGetValue(key, out var cacheItem))
        {
            if (cacheItem.IsExpired)
            {
                // 如果缓存过期，移除它
                this.extractionResultCache.TryRemove(key, out _);
                this.logger.LogDebug("Extraction result cache expired, removed from cache for type: {Type}", type);
                return default;
            }

            try
            {
                // 统一进行JSON反序列化
                var jsonString = cacheItem.Value as string ?? string.Empty;
                var result = JsonSerializer.Deserialize<T>(jsonString);
                this.logger.LogDebug("Retrieved and deserialized extraction result from cache for type: {Type}", type);
                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to deserialize cached extraction result for type: {Type}", type);
                return default;
            }
        }

        return default;
    }

    /// <summary>
    /// 将提取结果存入缓存.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="type">提取类型.</param>
    /// <param name="result">提取结果.</param>
    /// <param name="url">URL（可选）.</param>
    private void CacheExtractionResult(string htmlContent, string type, string result, string url = "")
    {
        CacheExtractionResult(htmlContent, type, (object)result, url);
    }

    /// <summary>
    /// 将提取结果存入缓存（泛型版本）.
    /// </summary>
    /// <typeparam name="T">结果类型.</typeparam>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="type">提取类型.</param>
    /// <param name="result">提取结果.</param>
    /// <param name="url">URL（可选）.</param>
    private void CacheExtractionResult<T>(string htmlContent, string type, T result, string url = "")
    {
        try
        {
            var key = GenerateCacheKey($"extraction_{type}", htmlContent, url);
            object cacheValue;

            // 统一序列化为JSON
            var jsonString = JsonSerializer.Serialize(result);
            cacheValue = jsonString;

            var cacheItem = new CacheItem<object>(cacheValue, DateTime.UtcNow.AddMilliseconds(CacheExpirationMs));
            this.extractionResultCache.TryAdd(key, cacheItem);
            this.logger.LogDebug("Cached extraction result for type: {Type}", type);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to cache extraction result for type: {Type}", type);
        }
    }

    /// <summary>
    /// 释放资源.
    /// </summary>
    /// <param name="disposing">表示是否释放托管资源.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                // 清理缓存
                this.htmlDocumentCache.Clear();
                this.extractionResultCache.Clear();
                this.logger.LogInformation("AIAssistedHelper resources disposed successfully");
            }

            this.disposed = true;
        }
    }

    /// <summary>
    /// 释放资源.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 提取HTML标题.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取到的标题文本.</returns>
    private string ExtractTitle(string htmlContent, string url = "")
    {
        // 尝试从缓存获取结果
        var cachedResult = this.GetCachedExtractionResult(htmlContent, "title", url);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // 获取或创建HTML文档
        var doc = this.GetOrCreateHtmlDocument(htmlContent, url);

        // 优先提取<title>标签内容
        var titleElement = doc.DocumentNode.SelectSingleNode("//title");
        if (titleElement != null && !string.IsNullOrWhiteSpace(titleElement.InnerText))
        {
            var title = titleElement.InnerText.Trim();
            this.CacheExtractionResult(htmlContent, "title", title, url);
            return title;
        }

        // 如果没有<title>标签，尝试提取<h1>标签内容
        var h1Element = doc.DocumentNode.SelectSingleNode("//h1");
        if (h1Element != null && !string.IsNullOrWhiteSpace(h1Element.InnerText))
        {
            var title = h1Element.InnerText.Trim();
            this.CacheExtractionResult(htmlContent, "title", title, url);
            return title;
        }

        this.CacheExtractionResult(htmlContent, "title", string.Empty, url);
        return string.Empty;
    }

    /// <summary>
    /// 提取HTML元数据信息.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取到的元数据信息文本.</returns>
    private string ExtractMetaInformation(string htmlContent, string url = "")
    {
        // 尝试从缓存获取结果
        var cachedResult = this.GetCachedExtractionResult(htmlContent, "meta_info", url);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // 获取或创建HTML文档
        var doc = this.GetOrCreateHtmlDocument(htmlContent, url);

        var metaInfo = new StringBuilder();

        // 提取描述元数据
        var descriptionNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description' or @property='og:description']");
        if (descriptionNode != null)
        {
            var content = descriptionNode.GetAttributeValue("content", string.Empty);
            if (!string.IsNullOrWhiteSpace(content))
            {
                metaInfo.AppendLine($"- 描述: {content}");
            }
        }

        // 提取关键词元数据
        var keywordsNode = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
        if (keywordsNode != null)
        {
            var content = keywordsNode.GetAttributeValue("content", string.Empty);
            if (!string.IsNullOrWhiteSpace(content))
            {
                metaInfo.AppendLine($"- 关键词: {content}");
            }
        }

        // 提取标题元数据（Open Graph）
        var ogTitleNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
        if (ogTitleNode != null)
        {
            var content = ogTitleNode.GetAttributeValue("content", string.Empty);
            if (!string.IsNullOrWhiteSpace(content))
            {
                metaInfo.AppendLine($"- 标题: {content}");
            }
        }

        var result = metaInfo.ToString().Trim();
        this.CacheExtractionResult(htmlContent, "meta_info", result, url);
        return result;
    }

    /// <summary>
    /// 提取HTML段落.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取到的段落列表.</returns>
    private List<string> ExtractParagraphs(string htmlContent, string url = "")
    {
        // 尝试从缓存获取结果（泛型版本，自动处理JSON反序列化）
        var cachedResult = this.GetCachedExtractionResult<List<string>>(htmlContent, "paragraphs", url);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // 获取或创建HTML文档
        var doc = this.GetOrCreateHtmlDocument(htmlContent, url);

        // 提取所有段落标签
        var pNodes = doc.DocumentNode.SelectNodes("//p");
        if (pNodes == null)
        {
            return [];
        }

        var paragraphs = pNodes.Select(p => p.InnerText.Trim())
            .Where(p => p.Length > 50) // 过滤太短的段落
            .ToList();

        // 使用泛型缓存方法，自动处理JSON序列化
        this.CacheExtractionResult(htmlContent, "paragraphs", paragraphs, url);

        return paragraphs;
    }

    /// <summary>
    /// 提取HTML标题等级.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>标题等级统计字典，键为标题等级（H1-H6），值为出现次数.</returns>
    private Dictionary<string, int> ExtractHeadings(string htmlContent, string url = "")
    {
        // 尝试从缓存获取结果（泛型版本，自动处理JSON反序列化）
        var cachedResult = this.GetCachedExtractionResult<Dictionary<string, int>>(htmlContent, "headings", url);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // 获取或创建HTML文档
        var doc = this.GetOrCreateHtmlDocument(htmlContent, url);

        var headings = new Dictionary<string, int> { { "H1", 0 }, { "H2", 0 }, { "H3", 0 }, { "H4", 0 }, { "H5", 0 }, { "H6", 0 } };

        // 提取所有标题标签（h1-h6）
        for (int i = 1; i <= 6; i++)
        {
            var headingNodes = doc.DocumentNode.SelectNodes($"//h{i}");
            if (headingNodes != null)
            {
                headings[$"H{i}"] = headingNodes.Count;
            }
        }

        // 使用泛型缓存方法，自动处理JSON序列化
        this.CacheExtractionResult(htmlContent, "headings", headings, url);

        return headings;
    }

    /// <summary>
    /// 提取HTML链接数量.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取到的链接数量.</returns>
    private int ExtractLinksCount(string htmlContent, string url = "")
    {
        // 尝试从缓存获取结果（泛型版本，自动处理JSON反序列化）
        var cachedResult = this.GetCachedExtractionResult<int>(htmlContent, "links_count", url);
        if (cachedResult != default)
        {
            return cachedResult;
        }

        // 获取或创建HTML文档
        var doc = this.GetOrCreateHtmlDocument(htmlContent, url);

        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        var result = links?.Count ?? 0;

        // 使用泛型缓存方法，自动处理JSON序列化
        this.CacheExtractionResult(htmlContent, "links_count", result, url);

        return result;
    }

    /// <summary>
    /// 提取HTML链接.
    /// </summary>
    /// <param name="htmlContent">HTML内容.</param>
    /// <param name="url">URL（可选）.</param>
    /// <returns>提取到的链接列表文本.</returns>
    private string ExtractLinks(string htmlContent, string url = "")
    {
        // 尝试从缓存获取结果
        var cachedResult = this.GetCachedExtractionResult(htmlContent, "links", url);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // 尝试从缓存获取HTML文档
        var doc = this.GetCachedHtmlDocument(htmlContent, url);
        if (doc == null)
        {
            doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            this.CacheHtmlDocument(htmlContent, doc, url);
        }

        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links == null || links.Count == 0)
        {
            this.CacheExtractionResult(htmlContent, "links", string.Empty, url);
            return string.Empty;
        }

        var linksText = new StringBuilder();
        var processedUrls = new HashSet<string>(); // 用于去重

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", string.Empty);
            var text = link.InnerText.Trim();

            if (string.IsNullOrWhiteSpace(href) || processedUrls.Contains(href))
            {
                continue;
            }

            processedUrls.Add(href);

            // 限制每个链接的文本长度
            if (text.Length > 50)
            {
                text = string.Concat(text.AsSpan(0, 50), "...");
            }

            linksText.AppendLine($"- [{text}]({href})");
        }

        var result = linksText.ToString().Trim();
        this.CacheExtractionResult(htmlContent, "links", result, url);

        return result;
    }

    /// <summary>
    /// 缓存项结构体，用于存储缓存值和过期时间.
    /// </summary>
    /// <typeparam name="T">缓存值的类型.</typeparam>
    /// <param name="value">缓存的值.</param>
    /// <param name="expirationTime">缓存项的过期时间.</param>
    private readonly struct CacheItem<T>(T value, DateTime expirationTime)
    {
        /// <summary>
        /// Gets 获取缓存的值.
        /// </summary>
        public T Value { get; } = value;

        /// <summary>
        /// Gets a value indicating whether 获取一个值，指示缓存项是否已过期.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > this.ExpirationTime;

        /// <summary>
        /// Gets 获取缓存项的过期时间.
        /// </summary>
        private DateTime ExpirationTime { get; } = expirationTime;
    }
}