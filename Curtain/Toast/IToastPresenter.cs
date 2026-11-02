// <copyright file="IToastPresenter.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Toast;

using System;

/// <summary>
/// Toast 呈现接缝：宿主侧实现渲染、计时与间距；过期回调让队列出队。
/// </summary>
public interface IToastPresenter
{
    /// <summary>Toast 自动过期时触发，参数为 Toast 标识。</summary>
    event Action<int> Expired;

    /// <summary>显示一条 Toast。</summary>
    /// <param name="toastId">Toast 标识。</param>
    /// <param name="message">提示文案。</param>
    void Show(int toastId, string message);

    /// <summary>移除一条 Toast；实现需保证幂等。</summary>
    /// <param name="toastId">Toast 标识。</param>
    void Dismiss(int toastId);
}
