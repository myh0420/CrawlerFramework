// <copyright file="UserAgentDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.AntiBot;

using System.Net.Http;
using System.Threading.Tasks;
using CrawlerFramework.CrawlerInterFaces.Interfaces.AntiBot;
using CrawlerInterFaces.Models;

/// <summary>
/// User-Agent检测器，用于检测网站是否验证User-Agent.
/// </summary>
public class UserAgentDetector : IAntiBotDetector
{
    /// <inheritdoc/>
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();

        // 检测是否有User-Agent相关的错误信息
        var userAgentIndicators = new[]
        {
            "invalid user agent", "user agent not supported",
            "浏览器版本过低", "请使用现代浏览器",
            "User-Agent不能为空", "检测到异常User-Agent",
        };

        foreach (var indicator in userAgentIndicators)
        {
            if (htmlContent.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockReason = "User-Agent validation detected";
                result.SuggestedAction = "Use rotating, realistic User-Agent strings";
                break;
            }
        }

        // 检测是否返回特定的403错误代码
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            // 检查是否是因为User-Agent问题
            if (response.Content.Headers.ContentType?.MediaType == "text/html")
            {
                // 简单检查响应内容是否包含User-Agent相关信息
                if (htmlContent.Contains("User-Agent", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsBlocked = true;
                    result.BlockReason = "Forbidden - User-Agent validation failure";
                    result.SuggestedAction = "Use valid User-Agent string";
                }
            }
        }

        return Task.FromResult(result);
    }
}