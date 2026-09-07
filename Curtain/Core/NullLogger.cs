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
    void ILogger.Trace(string message, params object[] args)
    {
    }

    /// <inheritdoc/>
    void ILogger.Debug(string message, params object[] args)
    {
    }

    /// <inheritdoc/>
    void ILogger.Info(string message, params object[] args)
    {
    }

    /// <inheritdoc/>
    void ILogger.Warn(string message, params object[] args)
    {
    }

    /// <inheritdoc/>
    void ILogger.Error(string message, params object[] args)
    {
    }

    /// <inheritdoc/>
    void ILogger.Error(Exception ex, string message)
    {
    }

    /// <inheritdoc/>
    void ILogger.Fatal(string message, params object[] args)
    {
    }

    /// <inheritdoc/>
    void ILogger.Fatal(Exception ex, string message)
    {
    }
}
