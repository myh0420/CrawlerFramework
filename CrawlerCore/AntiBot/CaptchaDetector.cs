// <copyright file="CaptchaDetector.cs" company="PlaceholderCompany">
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
/// 验证码检测器，用于检测HTML内容中是否包含验证码相关的反爬虫措施.
/// </summary>
public class CaptchaDetector : IAntiBotDetector
{
    /// <inheritdoc/>
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();

        // 检测常见的验证码页面
        var captchaIndicators = new[]
        {
            "captcha", "recaptcha", "hcaptcha", "cloudflare",
            "验证码", "人机验证", "请输入验证码",
        };

        foreach (var indicator in captchaIndicators)
        {
            if (htmlContent.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                result.IsBlocked = true;
                result.BlockReason = $"Captcha detected: {indicator}";
                result.SuggestedAction = "Wait and retry or use captcha solving service";
                break;
            }
        }

        return Task.FromResult(result);
    }
}
