using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrawlerEntity.Models;

/// <summary>
/// 解析结果
/// </summary>
public class ParseResult
{
    /// <summary>
    /// 解析的URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>
    /// 发现的链接
    /// </summary>
    public List<string> Links { get; set; } = [];
    /// <summary>
    /// 提取的数据
    /// </summary>
    public Dictionary<string, object> ExtractedData { get; set; } = [];
    /// <summary>
    /// 页面标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>
    /// 页面文本内容
    /// </summary>
    public string TextContent { get; set; } = string.Empty;
    /// <summary>
    /// 发现的图片URL
    /// </summary>
    public List<string> Images { get; set; } = [];
    /// <summary>
    /// 解析错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    /// <summary>
    /// 是否成功解析
    /// </summary>
    public bool IsSuccess { get; set; } = true;
    
    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
    
    /// <summary>
    /// 原始内容
    /// </summary>
    public string? Content { get; set; }
    
    /// <summary>
    /// 发现的URL（与Links相同，为了向后兼容）
    /// </summary>
    public List<string> DiscoveredUrls => Links;
}
