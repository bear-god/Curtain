// <copyright file="IViewContainer.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

/// <summary>
/// UI 层级父容器抽象，作为 <see cref="IUIRoot"/> 与 <see cref="IUIResourceLoader"/>
/// 之间传递的不透明句柄。由 Curtain.Unity 的 UnityViewContainer 实现（内部持有 RectTransform）。
/// </summary>
public interface IViewContainer
{
}
