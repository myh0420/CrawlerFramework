// <copyright file="RequestDelayDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.AntiBot;

using System.Net.Http;
using System.Threading.Tasks;

/// <summary>
/// 请求延迟检测器，用于检测网站是否实现了基于时间的反爬措施.
/// </summary>
public class RequestDelayDetector : IAntiBotDetector
{
    /// <inheritdoc/>
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();

        // 检测页面中是否包含JavaScript延迟检测代码
        var delayIndicators = new[]
        {
            "setTimeout", "setInterval", "requestAnimationFrame",
            "performance.timing", "Date.now()",
            "slow down", "too fast", "request too quickly",
            "请稍候", "操作过于频繁", "检测到异常访问速度",
        };

        foreach (var indicator in delayIndicators)
        {
            if (htmlContent.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockReason = "Request delay detection detected";
                result.SuggestedAction = "Implement random delays between requests";
                result.RetryAfterSeconds = 5; // 建议默认延迟5秒
                break;
            }
        }

        // 检测是否有隐藏的时间戳参数
        if (htmlContent.Contains("timestamp") && htmlContent.Contains("window"))
        {
            result.IsBlocked = true;
            result.BlockReason = "Timestamp-based anti-bot detection detected";
            result.SuggestedAction = "Analyze and mimic timestamp generation logic";
        }

        return Task.FromResult(result);
    }
}