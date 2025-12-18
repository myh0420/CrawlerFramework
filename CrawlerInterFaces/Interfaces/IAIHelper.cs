// CrawlerInterFaces/Interfaces/IAIHelper.cs
using System.Threading.Tasks;

namespace CrawlerFramework.CrawlerInterFaces.Interfaces
{
    /// <summary>
    /// AI辅助功能接口
    /// </summary>
    public interface IAIHelper : ICrawlerComponent
    {
        /// <summary>
        /// 异步提取网页主要内容
        /// </summary>
        /// <param name="htmlContent">HTML内容</param>
        /// <param name="url">网页URL</param>
        /// <returns>提取的主要内容</returns>
        Task<string> ExtractMainContentAsync(string htmlContent, string url = "");

        /// <summary>
        /// 异步根据提示提取特定信息
        /// </summary>
        /// <param name="htmlContent">HTML内容</param>
        /// <param name="prompt">提取提示</param>
        /// <param name="url">网页URL（可选）</param>
        /// <returns>提取的信息</returns>
        Task<string> ExtractWithPromptAsync(string htmlContent, string prompt, string url = "");

        /// <summary>
        /// 异步分析网页结构
        /// </summary>
        /// <param name="htmlContent">HTML内容</param>
        /// <param name="url">网页URL（可选）</param>
        /// <returns>网页结构分析结果</returns>
        Task<string> AnalyzeStructureAsync(string htmlContent, string url = "");

        /// <summary>
        /// 检查AI服务是否可用
        /// </summary>
        /// <returns>是否可用</returns>
        Task<bool> IsAvailableAsync();
    }
}