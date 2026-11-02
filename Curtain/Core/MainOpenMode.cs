// <copyright file="MainOpenMode.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

/// <summary>
/// 打开 Main 层界面时对已有 Main 的处理策略。
/// </summary>
public enum MainOpenMode
{
    /// <summary>
    /// 拒绝：已存在 Main 时抛出 <see cref="UIOpenException"/>，不做任何改动。
    /// </summary>
    Reject = 0,

    /// <summary>
    /// 替换：先打开新的 Main，成功后再关闭旧 Main。
    /// </summary>
    Replace = 1,
}
