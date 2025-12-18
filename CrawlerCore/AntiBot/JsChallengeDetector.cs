// <copyright file="JsChallengeDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.AntiBot;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;
using CrawlerFramework.CrawlerInterFaces.Interfaces.AntiBot;

/// <summary>
/// JavaScript挑战检测器，用于检测网站是否设置了JavaScript挑战（如Cloudflare）.
/// </summary>
public class JsChallengeDetector : IAntiBotDetector
{
    /// <inheritdoc/>
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();

        // 检测JavaScript挑战（如Cloudflare）
        var jsChallengeIndicators = new[]
        {
            "challenge-form", "jschl-answer", "cloudflare",
            "Checking your browser", "DDoS protection",
        };

        foreach (var indicator in jsChallengeIndicators)
        {
            if (htmlContent.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockReason = "JavaScript challenge detected";
                result.SuggestedAction = "Use headless browser or specialized solver";
                break;
            }
        }

        return Task.FromResult(result);
    }
}