// <copyright file="ViewModelBase.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

using System;
using Cysharp.Threading.Tasks;
using R3;

namespace UIFramework.Core;

/// <summary>
/// MVVM ViewModel 基类。
/// 子类持有 <see cref="ReactiveProperty{T}"/> / Subject 等响应式对象，
/// 订阅时统一使用 <c>AddTo(disposables)</c> 管理生命周期。
/// </summary>
public abstract class ViewModelBase : IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly CompositeDisposable _clearWhenHide = new();
    private bool _disposed;

    /// <summary>
    /// 返回键按下时调用。
    /// 返回 true 表示处理返回键（框架关闭当前界面）。
    /// 返回 false 表示拦截返回键（如强制公告不允许返回）。
    /// </summary>
    /// <returns>是否处理返回键。</returns>
    public virtual bool OnBack() => true;

    /// <summary>释放 ViewModel 及其所有订阅。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            OnDispose();
        }
        finally
        {
            _clearWhenHide.Dispose();
            _disposables.Dispose();
        }
    }

    /// <summary>初始化</summary>
    internal void Initialize()
    {
        OnInitialize(_disposables);
    }

    /// <summary>框架内部：通知 VM 显示。</summary>
    /// <returns>表示异步操作的 UniTask。</returns>
    internal async UniTask NotifyShowAsync()
    {
        _clearWhenHide.Clear();
        await OnShowAsync(_clearWhenHide);
    }

    /// <summary>框架内部：通知 VM 隐藏。</summary>
    /// <returns>表示异步操作的 UniTask。</returns>
    internal async UniTask NotifyHideAsync()
    {
        await OnHideAsync();
        _clearWhenHide.Clear();
    }

    /// <summary>View 实例化完成、Bind 之前调用一次。参数已注入。</summary>
    /// <param name="disposables">生命周期为 VM 实例的订阅回收容器。</param>
    protected virtual void OnInitialize(CompositeDisposable disposables)
    {
    }

    /// <summary>View 显示时调用，框架等待 UniTask 完成。</summary>
    /// <param name="clearWhenHide">显示期订阅回收容器，OnHide 时自动清空。</param>
    /// <returns>表示异步操作的 UniTask。</returns>
    protected virtual UniTask OnShowAsync(CompositeDisposable clearWhenHide) => UniTask.CompletedTask;

    /// <summary>View 隐藏时调用，框架等待 UniTask 完成。</summary>
    /// <returns>表示异步操作的 UniTask。</returns>
    protected virtual UniTask OnHideAsync() => UniTask.CompletedTask;

    /// <summary>子类清理业务资源的钩子（在 Disposables 释放前调用）</summary>
    protected virtual void OnDispose()
    {
    }
}