// <copyright file="ILogger.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using System;

/// <summary>
/// 日志接口，提供分级日志输出能力。
/// 由宿主实现后注入框架；框架只依赖此抽象，不依赖任何具体日志框架。
/// </summary>
public interface ILogger
{
    /// <summary>输出调试级别日志，用于开发调试信息。</summary>
    /// <param name="message">日志内容。</param>
    void LogDebug(string message);

    /// <summary>输出信息级别日志，用于常规运行时信息。</summary>
    /// <param name="message">日志内容。</param>
    void LogInfo(string message);

    /// <summary>输出警告级别日志，用于潜在问题提示。</summary>
    /// <param name="message">日志内容。</param>
    /// <param name="exception">附带的异常，可为 null。</param>
    void LogWarning(string message, Exception exception = null);

    /// <summary>输出错误级别日志，用于可恢复的错误。</summary>
    /// <param name="message">日志内容。</param>
    /// <param name="exception">附带的异常，可为 null。</param>
    void LogError(string message, Exception exception = null);
}
