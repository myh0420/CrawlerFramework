// <copyright file="AntiBotDetectionResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.AntiBot;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// 反爬虫检测结果类，用于存储对URL访问的反爬虫检测结果.
/// </summary>
public class AntiBotDetectionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether 获取或设置一个值，指示当前请求是否被封禁.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// Gets or sets 获取或设置封禁原因的详细描述.
    /// </summary>
    public string BlockReason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets 获取或设置建议的重试间隔时间（秒）.
    /// </summary>
    public int RetryAfterSeconds { get; set; }

    /// <summary>
    /// Gets or sets 获取或设置针对当前检测结果的建议操作.
    /// </summary>
    public string SuggestedAction { get; set; } = string.Empty;
}
