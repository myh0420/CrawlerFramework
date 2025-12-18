// CrawlerEntity/Models/CrawlModels.cs
using System;
using System.Collections.Generic;
using System.Threading;
using CrawlerFramework.CrawlerEntity.Enums;
using CrawlerFramework.CrawlerEntity.Events;

namespace  CrawlerFramework.CrawlerEntity.Models;


/// <summary>
/// 爬取请求
/// </summary>
public class CrawlRequest
{
    /// <summary>
    /// 请求URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>
    /// 爬取深度
    /// </summary>
    public int Depth { get; set; } = 0;
    /// <summary>
    /// 请求优先级
    /// </summary>
    public int Priority { get; set; } = (int)UrlPriority.Normal;
    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
    /// <summary>
    /// 引用URL
    /// </summary>
    public string Referrer { get; set; } = string.Empty;
    /// <summary>
    /// 爬取方法
    /// </summary>
    public CrawlMethod Method { get; set; } = CrawlMethod.GET;

    /// <summary>
    /// 额外请求头
    /// </summary>
    public Dictionary<string, string> AdditionalHeaders { get; set; } = [];

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken? CancellationToken { get; set; }

    /// <summary>
    /// 爬取配置
    /// </summary>
    public object? Configuration { get; set; }

    /// <summary>
    /// 任务ID
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    /// 加入队列时间
    /// </summary>
    public DateTime? QueuedAt { get; set; }

    /// <summary>
    /// 开始处理时间
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 请求ID
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
}