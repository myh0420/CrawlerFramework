// <copyright file="ISecurityService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace  CrawlerFramework.CrawlerCore.Security
{
    using System.Threading.Tasks;

    /// <summary>
    /// 安全服务接口，提供加密解密功能。.
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// 异步加密字符串。.
        /// </summary>
        /// <param name="plainText">要加密的明文。.</param>
        /// <returns>加密后的字符串。.</returns>
        Task<string> EncryptAsync(string plainText);

        /// <summary>
        /// 异步解密字符串。.
        /// </summary>
        /// <param name="encryptedText">要解密的密文。.</param>
        /// <returns>解密后的明文。.</returns>
        Task<string> DecryptAsync(string encryptedText);

        /// <summary>
        /// 异步加密字符串数组。.
        /// </summary>
        /// <param name="plainTexts">要加密的明文数组。.</param>
        /// <returns>加密后的字符串数组。.</returns>
        Task<string[]> EncryptArrayAsync(string[] plainTexts);

        /// <summary>
        /// 异步解密字符串数组。.
        /// </summary>
        /// <param name="encryptedTexts">要解密的密文数组。.</param>
        /// <returns>解密后的明文数组。.</returns>
        Task<string[]> DecryptArrayAsync(string[] encryptedTexts);
    }
}