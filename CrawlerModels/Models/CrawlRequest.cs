// CrawlerEntity/Models/CrawlModels.cs
using System;
using System.Collections.Generic;
using CrawlerEntity.Enums;
using CrawlerEntity.Events;

namespace CrawlerEntity.Models;


/// <summary>
/// ��ȡ����
/// </summary>
public class CrawlRequest
{
    public string Url { get; set; } = string.Empty;
    public int Depth { get; set; }
    public int Priority { get; set; } = (int)UrlPriority.Normal;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string Referrer { get; set; } = string.Empty;
    public CrawlMethod Method { get; set; } = CrawlMethod.GET;

    /// <summary>
    /// ���Դ���
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// ������Դ���
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}