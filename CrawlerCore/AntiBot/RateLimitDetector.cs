// <copyright file="RateLimitDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.AntiBot;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// 速率限制检测器，用于检测请求是否超过了网站的速率限制.
/// </summary>
public class RateLimitDetector : IAntiBotDetector
{
    /// <inheritdoc/>
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();

        // 检测速率限制
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            result.IsBlocked = true;
            result.BlockReason = "Rate limit exceeded";

            // 尝试从 Retry-After 头获取等待时间
            if (response.Headers.TryGetValues("Retry-After", out var values))
            {
                if (int.TryParse(values.First(), out var seconds))
                {
                    result.RetryAfterSeconds = seconds;
                }
            }

            result.SuggestedAction = $"Wait {result.RetryAfterSeconds} seconds before retrying";
        }

        return Task.FromResult(result);
    }
}
