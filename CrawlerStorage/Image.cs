// <copyright file="Image.cs" company="PlaceholderCompany">
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
    /// 图片文档.
    /// </summary>
    internal class Image
    {
        /// <summary>
        /// Gets or sets 文档ID.
        /// </summary>
        public ObjectId Id { get; set; }

        /// <summary>
        /// Gets or sets uRL.
        /// </summary>
        public required string Url { get; set; }

        /// <summary>
        /// Gets or sets 图片URL.
        /// </summary>
        public required string ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets 图片替代文本.
        /// </summary>
        public required string AltText { get; set; }
    }
}