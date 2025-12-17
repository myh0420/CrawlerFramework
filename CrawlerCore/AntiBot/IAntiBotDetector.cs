// <copyright file="IAntiBotDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.AntiBot;

using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Models;

/// <summary>
/// 反爬虫策略增强检测器接口.
/// </summary>
/// <remarks>
/// 实现类应该根据响应内容和HTML内容来判断是否触发反爬虫策略.
/// </remarks>
public interface IAntiBotDetector
{
    /// <summary>
    /// 检测是否触发反爬虫策略.
    /// </summary>
    /// <param name="response">HTTP响应消息.</param>
    /// <param name="htmlContent">响应内容的HTML字符串.</param>
    /// <returns>一个表示异步操作的任务，任务结果为反爬虫策略增强检测结果.</returns>
    /// <remarks>
    /// 实现类应该根据响应内容和HTML内容来判断是否触发反爬虫策略.
    /// </remarks>
    Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent);
}