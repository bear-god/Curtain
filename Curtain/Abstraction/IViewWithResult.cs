// <copyright file="IViewWithResult.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using Curtain.Core;

/// <summary>
/// 可接收对话框结果句柄的 View 接口。
/// UIManager 的对话框打开链要求 View 实现此接口，并在 <see cref="IView.Initialize"/> 之前注入句柄。
/// </summary>
public interface IViewWithResult
{
    /// <summary>接收对话框结果句柄，视图通过句柄返回结果。</summary>
    /// <param name="handle">对话框句柄。</param>
    void SetResultHandle(UIHandle handle);
}
