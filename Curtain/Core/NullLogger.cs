// <copyright file="NullLogger.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

using System;
using Curtain.Abstraction;

/// <summary>
/// 空日志记录器，所有日志操作均为空实现，用于禁用日志输出的场景。
/// </summary>
public sealed class NullLogger : ILogger
{
    private NullLogger()
    {
    }

    /// <summary>获取 NullLogger 的单例实例。</summary>
    public static NullLogger Instance { get; } = new();

    /// <inheritdoc/>
    void ILogger.LogDebug(string message)
    {
    }

    /// <inheritdoc/>
    void ILogger.LogInfo(string message)
    {
    }

    /// <inheritdoc/>
    void ILogger.LogWarning(string message, Exception exception)
    {
    }

    /// <inheritdoc/>
    void ILogger.LogError(string message, Exception exception)
    {
    }
}
