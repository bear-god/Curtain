// <copyright file="UIHandle.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

using System;
using Cysharp.Threading.Tasks;

namespace UIFramework.Core;

/// <summary>
/// UI 句柄，调用 <see cref="Dispose"/> 关闭该 UI。
/// 页面使用此类，对话框使用 <see cref="UIHandle{TResult}"/>。
/// </summary>
public class UIHandle : IDisposable
{
    private readonly int _id;
    private readonly Action _onClose;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="UIHandle"/> 的新实例。
    /// </summary>
    /// <param name="id">UI 实例唯一标识。</param>
    /// <param name="onClose">关闭回调。</param>
    internal UIHandle(int id, Action onClose)
    {
        _id = id;
        _onClose = onClose;
    }

    /// <summary>UI 实例唯一标识。</summary>
    public int Id => _id;

    /// <summary>句柄是否有效（可关闭）。</summary>
    public bool IsValid => _onClose != null && !_disposed;

    /// <summary>关闭该 UI 实例。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onClose?.Invoke();
    }

    /// <summary>框架内部：级联关闭（被父页面关闭时调用）。</summary>
    internal virtual void Dismiss()
    {
    }

    /// <summary>框架内部：对话框正常完成，设置返回值。</summary>
    /// <param name="result">返回值。</param>
    internal virtual void Complete(object result)
    {
    }
}

#pragma warning disable SA1402
/// <summary>
/// 带返回值的对话框句柄。
/// 通过 <see cref="GetResult"/> 等待对话框返回结果。
/// </summary>
/// <typeparam name="TResult">返回值类型。</typeparam>
public sealed class UIHandle<TResult> : UIHandle
{
    private readonly UniTaskCompletionSource<UIResult<TResult>> _tcs = new();

    /// <summary>
    /// 初始化 <see cref="UIHandle{TResult}"/> 的新实例。
    /// </summary>
    /// <param name="id">UI 实例唯一标识。</param>
    /// <param name="onClose">关闭回调。</param>
    internal UIHandle(int id, Action onClose)
        : base(id, onClose)
    {
    }

    /// <summary>等待对话框返回结果。</summary>
    /// <returns>包含完成状态和返回值的 <see cref="UIResult{T}"/>。</returns>
    public UniTask<UIResult<TResult>> GetResult()
    {
        return _tcs.Task;
    }

    /// <summary>对话框正常完成，设置返回值（由 ViewModel 调用）。</summary>
    /// <param name="result">返回值。</param>
    public void Complete(TResult result)
    {
        _tcs.TrySetResult(new UIResult<TResult>(true, result));
    }

    /// <summary>框架内部：级联关闭。</summary>
    internal override void Dismiss()
    {
        _tcs.TrySetResult(new UIResult<TResult>(false, default));
    }

    /// <summary>框架内部：对话框正常完成，设置返回值。</summary>
    /// <param name="result">返回值。</param>
    internal override void Complete(object result)
    {
        Complete((TResult)result);
    }
}
#pragma warning restore SA1402