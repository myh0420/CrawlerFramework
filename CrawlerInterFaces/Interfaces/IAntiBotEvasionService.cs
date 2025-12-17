// <copyright file="IAntiBotEvasionService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerInterFaces.Interfaces;

using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Models;

/// <summary>
/// 反爬规避服务接口，用于管理反爬应对策略.
/// </summary>
public interface IAntiBotEvasionService : ICrawlerComponent
{
    /// <summary>
    /// 为HTTP请求配置反爬规避策略.
    /// </summary>
    /// <param name="request">要配置的HTTP请求.</param>
    /// <param name="url">请求的URL.</param>
    void ConfigureRequest(HttpRequestMessage request, string url);

    /// <summary>
    /// 获取随机的User-Agent字符串.
    /// </summary>
    /// <returns>随机的User-Agent字符串.</returns>
    string GetRandomUserAgent();

    /// <summary>
    /// 执行随机延迟，模拟人类用户的行为.
    /// </summary>
    /// <param name="minDelayMs">最小延迟时间（毫秒）.</param>
    /// <param name="maxDelayMs">最大延迟时间（毫秒）.</param>
    /// <returns>表示异步操作的任务.</returns>
    Task RandomDelayAsync(int minDelayMs = 1000, int maxDelayMs = 5000);

    /// <summary>
    /// 根据检测结果应用相应的反爬策略.
    /// </summary>
    /// <param name="result">反爬检测结果.</param>
    /// <returns>表示异步操作的任务.</returns>
    Task ApplyStrategyAsync(AntiBotDetectionResult result);

    /// <summary>
    /// 处理响应中的Cookie，将它们保存到Cookie容器中.
    /// </summary>
    /// <param name="response">HTTP响应消息.</param>
    /// <param name="url">请求的URL.</param>
    void ProcessResponseCookies(HttpResponseMessage response, string url);
}