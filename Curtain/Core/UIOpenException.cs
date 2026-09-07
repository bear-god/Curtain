// <copyright file="UIOpenException.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

using System;

/// <summary>
/// 打开页面或对话框失败时抛出的异常。
/// </summary>
public sealed class UIOpenException : Exception
{
    /// <summary>
    /// 初始化 <see cref="UIOpenException"/> 的新实例。
    /// </summary>
    /// <param name="message">错误消息。</param>
    public UIOpenException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化 <see cref="UIOpenException"/> 的新实例，包含内部异常。
    /// </summary>
    /// <param name="message">错误消息。</param>
    /// <param name="innerException">内部异常。</param>
    public UIOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}