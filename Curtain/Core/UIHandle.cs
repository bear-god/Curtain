// <copyright file="UIHandle.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Core;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Curtain.Abstraction;

/// <summary>
/// UI 句柄。打开成功后由框架返回，用于查询状态、访问视图与关闭界面。
/// <see cref="IsOpen"/> 在界面从注册表摘除时变为 false；<see cref="View"/> 在关闭开始后不可再访问。
/// 关闭操作可重复调用，多次调用共享同一次关闭的完成。
/// </summary>
public class UIHandle : IDisposable
{
    private readonly int _id;
    private readonly object _stateLock = new object();
    private readonly TaskCompletionSource<byte> _closed =
        new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly UIManager _manager;
    private IView _view;
    private volatile bool _isOpen;
    private Task _closeAttempt;

    /// <summary>
    /// 初始化 <see cref="UIHandle"/> 的新实例。
    /// </summary>
    /// <param name="id">UI 实例唯一标识。</param>
    /// <param name="manager">所属的 UI 管理器。</param>
    internal UIHandle(int id, UIManager manager)
    {
        _id = id;
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>UI 实例唯一标识。</summary>
    public int Id => _id;

    /// <summary>界面是否仍处于打开状态；关闭开始（从注册表摘除）后为 false。</summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// 界面实例；关闭开始后访问会抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public IView View
    {
        get
        {
            if (!_isOpen || _view == null)
            {
                throw new InvalidOperationException($"界面 '{_id}' 已关闭，无法访问 View。");
            }

            return _view;
        }
    }

    /// <summary>以 <see cref="CloseReason.Normal"/> 关闭界面；多次调用共享同一次关闭的完成。</summary>
    /// <param name="cancellationToken">取消令牌，仅在界面摘除前生效。</param>
    /// <returns>关闭完成时结束的 ValueTask。</returns>
    public ValueTask CloseAsync(CancellationToken cancellationToken = default)
    {
        return RequestCloseAsync(CloseReason.Normal, cancellationToken);
    }

    /// <summary>尾部排队一次 <see cref="CloseReason.Normal"/> 关闭，不等待完成。</summary>
    public void Dispose()
    {
        try
        {
            _ = RequestCloseAsync(CloseReason.Normal, CancellationToken.None);
        }
        catch (Exception)
        {
            // 关闭失败只记录在框架日志中，Dispose 不向调用方抛出。
        }
    }

    /// <summary>框架内部：界面注册成功，绑定实例并把状态置为打开。</summary>
    /// <param name="view">界面实例。</param>
    internal void AttachView(IView view)
    {
        _view = view;
        _isOpen = true;
    }

    /// <summary>框架内部：界面已从注册表摘除，此后不可再访问 <see cref="View"/>。</summary>
    internal void MarkDetached()
    {
        _isOpen = false;
    }

    /// <summary>框架内部：界面已释放，结束所有等待关闭的调用。</summary>
    internal void MarkClosed()
    {
        _isOpen = false;
        OnClosed();
    }

    /// <summary>框架内部：请求关闭，返回共享的关闭任务。</summary>
    /// <param name="reason">关闭原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>关闭完成时结束的 ValueTask。</returns>
    internal ValueTask RequestCloseAsync(CloseReason reason, CancellationToken cancellationToken)
    {
        Task attempt;
        lock (_stateLock)
        {
            if (_closed.Task.IsCompleted)
            {
                return default;
            }

            if (_closeAttempt == null)
            {
                _closeAttempt = CloseCoreAsync(reason, cancellationToken);
            }

            attempt = _closeAttempt;
        }

        return new ValueTask(attempt);
    }

    /// <summary>子类钩子：界面释放后调用，用于收尾自身的等待状态。</summary>
    protected virtual void OnClosed()
    {
        _closed.TrySetResult(0);
    }

    /// <summary>把关闭请求交给 UIManager 的命令队列；取消或失败时允许后续重试。</summary>
    /// <param name="reason">关闭原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>关闭完成时结束的任务。</returns>
    private async Task CloseCoreAsync(CloseReason reason, CancellationToken cancellationToken)
    {
        try
        {
            await _manager.CloseHandleAsync(this, reason, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            lock (_stateLock)
            {
                _closeAttempt = null;
            }

            throw;
        }
    }
}

#pragma warning disable SA1402
/// <summary>
/// 带返回值的对话框句柄。
/// 通过 <see cref="GetResult()"/> 等待对话框返回结果；被关闭时若未设置结果，则返回未完成结果。
/// </summary>
/// <typeparam name="TResult">返回值类型。</typeparam>
public sealed class UIHandle<TResult> : UIHandle, IValueTaskSource<UIResult<TResult>>
{
    private ManualResetValueTaskSourceCore<UIResult<TResult>> _core;
    private bool _resultSet;

    /// <summary>
    /// 初始化 <see cref="UIHandle{TResult}"/> 的新实例。
    /// </summary>
    /// <param name="id">UI 实例唯一标识。</param>
    /// <param name="manager">所属的 UI 管理器。</param>
    internal UIHandle(int id, UIManager manager)
        : base(id, manager)
    {
    }

    /// <summary>等待对话框返回结果。</summary>
    /// <returns>包含完成状态和返回值的 <see cref="UIResult{T}"/>。</returns>
    public ValueTask<UIResult<TResult>> GetResult()
    {
        return new ValueTask<UIResult<TResult>>(this, _core.Version);
    }

    /// <summary>对话框正常完成，设置返回值（由 View 调用）。</summary>
    /// <param name="result">返回值。</param>
    public void Complete(TResult result)
    {
        if (_resultSet)
        {
            return;
        }

        _resultSet = true;
        _core.SetResult(new UIResult<TResult>(true, result));
    }

    /// <inheritdoc/>
    public UIResult<TResult> GetResult(short token)
    {
        return _core.GetResult(token);
    }

    /// <inheritdoc/>
    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _core.GetStatus(token);
    }

    /// <inheritdoc/>
    public void OnCompleted(
        Action<object> continuation,
        object state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }

    /// <inheritdoc/>
    protected override void OnClosed()
    {
        if (!_resultSet)
        {
            _resultSet = true;
            _core.SetResult(new UIResult<TResult>(false, default));
        }

        base.OnClosed();
    }
}
#pragma warning restore SA1402
