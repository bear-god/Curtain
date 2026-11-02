// <copyright file="CloseReason.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

/// <summary>
/// 视图关闭的原因，在关闭生命周期钩子中传给 <see cref="Curtain.Abstraction.IView"/>。
/// </summary>
public enum CloseReason
{
    /// <summary>调用方主动关闭。</summary>
    Normal = 0,

    /// <summary>父界面关闭导致的级联关闭。</summary>
    ParentClosed = 1,

    /// <summary>宿主关闭整个 UI。</summary>
    Shutdown = 2,

    /// <summary>打开流程失败后的清理关闭。</summary>
    OpenFailed = 3,
}
