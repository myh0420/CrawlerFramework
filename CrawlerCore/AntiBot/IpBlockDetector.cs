// <copyright file="IpBlockDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerCore.AntiBot;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Models;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// IP封禁检测器，用于检测当前IP是否被目标网站封禁.
/// </summary>
public class IpBlockDetector : IAntiBotDetector
{
    /// <inheritdoc/>
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();

        // 检测IP被封禁的迹象
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            var blockIndicators = new[]
            {
                "access denied", "ip blocked", "forbidden",
                "您的IP已被封禁", "禁止访问",
            };

            foreach (var indicator in blockIndicators)
            {
                if (htmlContent.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsBlocked = true;
                    result.BlockReason = "IP address blocked";
                    result.SuggestedAction = "Change IP address or use proxy";
                    break;
                }
            }
        }

        return Task.FromResult(result);
    }
}
