// <copyright file="Link.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace CrawlerFramework.CrawlerStorage
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CrawlerFramework.CrawlerEntity.Enums;
    using CrawlerFramework.CrawlerEntity.Models;
    using CrawlerFramework.CrawlerInterFaces.Interfaces;
    using Microsoft.Extensions.Logging;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Conventions;
    using MongoDB.Driver;
    using Newtonsoft.Json;

    /// <summary>
    /// 链接文档.
    /// </summary>
    internal class Link
    {
        /// <summary>
        /// Gets or sets 文档ID.
        /// </summary>
        public ObjectId Id { get; set; }

        /// <summary>
        /// Gets or sets 源URL.
        /// </summary>
        public required string SourceUrl { get; set; }

        /// <summary>
        /// Gets or sets 目标URL.
        /// </summary>
        public required string TargetUrl { get; set; }

        /// <summary>
        /// Gets or sets 链接文本.
        /// </summary>
        public required string LinkText { get; set; }
    }
}