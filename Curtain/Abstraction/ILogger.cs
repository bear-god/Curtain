// <copyright file="ILogger.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using System;

/// <summary>
/// 日志记录器接口，提供分级日志输出能力。
/// 由宿主（Unity 项目）通过 DI 注入实现，框架仅依赖此抽象。
/// </summary>
public interface ILogger
{
    /// <summary>输出追踪级别日志，用于最详细的调试信息。</summary>
    /// <param name="message">日志消息格式字符串。</param>
    /// <param name="args">格式化参数。</param>
    void Trace(string message, params object[] args);

    /// <summary>输出调试级别日志，用于开发调试信息。</summary>
    /// <param name="message">日志消息格式字符串。</param>
    /// <param name="args">格式化参数。</param>
    void Debug(string message, params object[] args);

    /// <summary>输出信息级别日志，用于常规运行时信息。</summary>
    /// <param name="message">日志消息格式字符串。</param>
    /// <param name="args">格式化参数。</param>
    void Info(string message, params object[] args);

    /// <summary>输出警告级别日志，用于潜在问题提示。</summary>
    /// <param name="message">日志消息格式字符串。</param>
    /// <param name="args">格式化参数。</param>
    void Warn(string message, params object[] args);

    /// <summary>输出错误级别日志，用于可恢复的错误。</summary>
    /// <param name="message">日志消息格式字符串。</param>
    /// <param name="args">格式化参数。</param>
    void Error(string message, params object[] args);

    /// <summary>输出错误级别日志，附带异常信息。</summary>
    /// <param name="ex">异常对象。</param>
    /// <param name="message">可选的附加消息。</param>
    void Error(Exception ex, string message = null);

    /// <summary>输出致命级别日志，用于不可恢复的严重错误。</summary>
    /// <param name="message">日志消息格式字符串。</param>
    /// <param name="args">格式化参数。</param>
    void Fatal(string message, params object[] args);

    /// <summary>输出致命级别日志，附带异常信息。</summary>
    /// <param name="ex">异常对象。</param>
    /// <param name="message">可选的附加消息。</param>
    void Fatal(Exception ex, string message = null);
}
