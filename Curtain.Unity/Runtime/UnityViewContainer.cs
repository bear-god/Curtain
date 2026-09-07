// <copyright file="UnityViewContainer.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Unity.Runtime;

using System;
using Curtain.Abstraction;
using UnityEngine;

/// <summary>
/// <see cref="IViewContainer"/> 的 Unity 实现，内部持有 RectTransform。
/// 宿主资源加载器通过 <see cref="Transform"/> 将实例挂到对应层级下。
/// </summary>
public sealed class UnityViewContainer : IViewContainer
{
    /// <summary>初始化 <see cref="UnityViewContainer"/> 的新实例。</summary>
    /// <param name="transform">层级根节点。</param>
    public UnityViewContainer(RectTransform transform)
    {
        Transform = transform ?? throw new ArgumentNullException(nameof(transform));
    }

    /// <summary>层级根节点。</summary>
    public RectTransform Transform { get; }
}
