// <copyright file="CookieTrackingDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.AntiBot;

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerFramework.CrawlerInterFaces.Interfaces.AntiBot;
using CrawlerInterFaces.Models;

/// <summary>
/// Cookie跟踪检测器，用于检测网站的Cookie跟踪机制.
/// </summary>
public class CookieTrackingDetector : IAntiBotDetector
{
    /// <inheritdoc/>
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();

        // 检测是否有大量追踪Cookie
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var cookieList = cookies.ToList();
            if (cookieList.Count > 10) // 如果设置了超过10个Cookie，可能存在追踪机制
            {
                var trackingCookieCount = cookieList.Count(c =>
                    c.Contains("tracking") ||
                    c.Contains("session") ||
                    c.Contains("token") ||
                    c.Contains("visitor") ||
                    c.Contains("_ga") ||
                    c.Contains("_gid"));

                if (trackingCookieCount > 5)
                {
                    result.IsBlocked = true;
                    result.BlockReason = "Cookie tracking detected - too many tracking cookies";
                    result.SuggestedAction = "Use cookie management strategy, consider rotating cookies";
                }
            }
        }

        return Task.FromResult(result);
    }
}