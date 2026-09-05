// <copyright file="UIAttribute.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

using System;

namespace UIFramework.Core;

/// <summary>
/// 标记 View 子类对应的 Prefab 路径。
/// 层级在打开时通过参数指定，不由此 Attribute 标注。
/// <example>
/// <code>
/// [UI("UI/Page/ShopPage")]
/// public class ShopPageView : ViewBase&lt;ShopPageVM&gt; { ... }
/// </code>
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class UIAttribute : Attribute
{
    /// <summary>初始化 UI 标注，指定 Prefab 路径。</summary>
    /// <param name="prefabPath">UI Prefab 资源路径。</param>
    public UIAttribute(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath))
        {
            throw new ArgumentException("Prefab 路径不能为空", nameof(prefabPath));
        }

        PrefabPath = prefabPath;
    }

    /// <summary>UI Prefab 资源路径（资源系统使用的地址）</summary>
    public string PrefabPath { get; }
}