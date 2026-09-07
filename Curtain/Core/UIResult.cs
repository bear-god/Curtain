// <copyright file="UIResult.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

/// <summary>
/// 对话框的返回结果。
/// 调用方必须先检查 <see cref="IsCompleted"/>，
/// 只有为 true 时 <see cref="Value"/> 才有效。
/// </summary>
/// <typeparam name="T">返回值类型。</typeparam>
public readonly struct UIResult<T>
{
    /// <summary>初始化 <see cref="UIResult{T}"/> 的新实例。</summary>
    /// <param name="isCompleted">是否正常完成。</param>
    /// <param name="value">返回值。</param>
    internal UIResult(bool isCompleted, T value)
    {
        IsCompleted = isCompleted;
        Value = value;
    }

    /// <summary>对话框是否正常完成（用户确认），false 表示被取消或外部关闭。</summary>
    public bool IsCompleted { get; }

    /// <summary>用户返回的值，仅在 <see cref="IsCompleted"/> 为 true 时有效。</summary>
    public T Value { get; }

    /// <summary>解构，方便模式匹配风格使用。</summary>
    /// <param name="isCompleted">是否正常完成。</param>
    /// <param name="value">返回值。</param>
    public void Deconstruct(out bool isCompleted, out T value)
    {
        isCompleted = IsCompleted;
        value = Value;
    }
}