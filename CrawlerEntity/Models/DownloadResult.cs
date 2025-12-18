using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace  CrawlerFramework.CrawlerEntity.Models;

/// <summary>
/// 下载结果
/// </summary>
public class DownloadResult
{
    /// <summary>
    /// 下载URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>
    /// 下载内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>
    /// 原始数据
    /// </summary>
    public byte[] RawData { get; set; } = [];
    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
    /// <summary>
    /// HTTP状态码
    /// </summary>
    public int StatusCode { get; set; }
    /// <summary>
    /// 下载时间（毫秒）
    /// </summary>
    public long DownloadTimeMs { get; set; }
    /// <summary>
    /// 响应头
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = [];
    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// 请求ID
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
}
