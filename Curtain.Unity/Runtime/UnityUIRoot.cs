// <copyright file="UnityUIRoot.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Unity.Runtime;

using System;
using System.Collections.Generic;
using Curtain.Abstraction;
using Curtain.Core;
using UnityEngine;

/// <summary>
/// <see cref="IUIRoot"/> 的 Unity 实现，挂载到 UI 根节点上，
/// 为各层级提供序列化的 RectTransform 容器。
/// </summary>
public class UnityUIRoot : MonoBehaviour, IUIRoot
{
    private readonly Dictionary<UILayer, UnityViewContainer> _containers = new();

    [SerializeField]
    private RectTransform _main;

    [SerializeField]
    private RectTransform _stack;

    [SerializeField]
    private RectTransform _effect;

    [SerializeField]
    private RectTransform _top;

    /// <inheritdoc/>
    public IViewContainer GetLayerRoot(UILayer layer)
    {
        if (_containers.TryGetValue(layer, out var cached))
        {
            return cached;
        }

        var transform = layer switch
        {
            UILayer.Main => _main,
            UILayer.Stack => _stack,
            UILayer.Effect => _effect,
            UILayer.Top => _top,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null),
        };

        if (transform == null)
        {
            throw new InvalidOperationException($"层级 '{layer}' 未配置 RectTransform 根节点。");
        }

        var container = new UnityViewContainer(transform);
        _containers[layer] = container;
        return container;
    }
}
