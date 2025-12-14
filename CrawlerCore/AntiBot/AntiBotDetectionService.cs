// CrawlerCore/AntiBot/AntiBotDetectionService.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using CrawlerInterFaces.Interfaces;
using Microsoft.Extensions.Logging;

namespace CrawlerCore.AntiBot;

/// <summary>
/// 反爬虫策略增强
/// </summary>
public class AntiBotDetectionResult
{
    public bool IsBlocked { get; set; }
    public string BlockReason { get; set; } = string.Empty;
    public int RetryAfterSeconds { get; set; }
    public string SuggestedAction { get; set; } = string.Empty;
}

public class AntiBotDetectionService(ILogger<AntiBotDetectionService>? logger) : ICrawlerComponent
{
    private readonly ILogger<AntiBotDetectionService> _logger = logger ?? new Logger<AntiBotDetectionService>(new LoggerFactory());
    private readonly List<IAntiBotDetector> _detectors = [
            new CaptchaDetector(),
            new RateLimitDetector(),
            new IpBlockDetector(),
            new JsChallengeDetector()
        ];
    private bool _isInitialized = false;

    public async Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        foreach (var detector in _detectors)
        {
            var result = await detector.DetectAsync(response, htmlContent);
            if (result.IsBlocked)
            {
                _logger.LogWarning("Anti-bot detection triggered: {Reason}", result.BlockReason);
                return result;
            }
        }

        return new AntiBotDetectionResult { IsBlocked = false };
    }

    public void AddDetector(IAntiBotDetector detector)
    {
        _detectors.Add(detector);
    }
    
    /// <summary>
    /// 检查是否应该处理给定的URL
    /// </summary>
    public bool ShouldProcess(string url)
    {
        // 这里可以添加基于URL的过滤规则
        // 例如：检查是否是已知的反爬虫严格网站
        try
        {
            var uri = new Uri(url);
            var host = uri.Host;
            
            // 示例：添加一些简单的规则
            var blockedHosts = new List<string> { "example.com" };
            return !blockedHosts.Contains(host);
        }
        catch (UriFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid URL: {Url}", url);
            return false;
        }
    }

    public Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            _logger.LogDebug("AntiBotDetectionService initialized successfully with {DetectorCount} detectors", _detectors.Count);
        }
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        if (_isInitialized)
        {
            _isInitialized = false;
            // 清理检测器资源
            foreach (var detector in _detectors)
            {
                if (detector is IDisposable disposableDetector)
                {
                    disposableDetector.Dispose();
                }
            }
            _detectors.Clear();
            _logger.LogDebug("AntiBotDetectionService shutdown successfully");
        }
        return Task.CompletedTask;
    }
}

public interface IAntiBotDetector
{
    Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent);
}

public class CaptchaDetector : IAntiBotDetector
{
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();
        
        // 检测常见的验证码页面
        var captchaIndicators = new[]
        {
            "captcha", "recaptcha", "hcaptcha", "cloudflare",
            "验证码", "人机验证", "请输入验证码"
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

public class RateLimitDetector : IAntiBotDetector
{
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

public class IpBlockDetector : IAntiBotDetector
{
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();
        
        // 检测IP被封禁的迹象
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            var blockIndicators = new[]
            {
                "access denied", "ip blocked", "forbidden",
                "您的IP已被封禁", "禁止访问"
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

public class JsChallengeDetector : IAntiBotDetector
{
    public Task<AntiBotDetectionResult> DetectAsync(HttpResponseMessage response, string htmlContent)
    {
        var result = new AntiBotDetectionResult();
        
        // 检测JavaScript挑战（如Cloudflare）
        var jsChallengeIndicators = new[]
        {
            "challenge-form", "jschl-answer", "cloudflare",
            "Checking your browser", "DDoS protection"
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