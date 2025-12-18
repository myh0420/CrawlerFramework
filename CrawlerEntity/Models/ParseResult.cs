// <copyright file="ParseResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerEntity.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 解析结果.
/// </summary>
public class ParseResult
{
    /// <summary>
    /// Gets or sets 解析的URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets 发现的链接.
    /// </summary>
    public List<string> Links { get; set; } = [];

    /// <summary>
    /// Gets or sets 提取的数据.
    /// </summary>
    public Dictionary<string, object> ExtractedData { get; set; } = [];

    /// <summary>
    /// Gets or sets 页面标题.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets 页面文本内容.
    /// </summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets 发现的图片URL.
    /// </summary>
    public List<string> Images { get; set; } = [];

    /// <summary>
    /// Gets or sets 解析错误消息.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets 是否成功解析.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Gets or sets 解析时间（毫秒）.
    /// </summary>
    public long ParseTimeMs { get; set; } = 0;

    /// <summary>
    /// Gets or sets 内容类型.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets 原始内容.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets 发现的URL（与Links相同，为了向后兼容）.
    /// </summary>
    public List<string> DiscoveredUrls => this.Links;

    /// <summary>
    /// Gets or sets 错误类型.
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;
}
