// <copyright file="ViewBase.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Unity;

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Curtain.Abstraction;
using Curtain.Core;
using UnityEngine;

/// <summary>
/// View 基类（Unity 桥接实现，实现核心的 <see cref="IView"/>）。
/// 框架不感知 ViewModel：VM 的创建与生命周期由 View 自己负责。
/// 框架不管理打开动画：如需入场动画，请在 <see cref="OnInitialize"/> 中自行启动；
/// 关闭前框架调用 <see cref="IView.HideAsync"/> 并等待 <see cref="OnHideAsync"/> 完成。
/// 本基类不依赖任何响应式库（如 R3），如需响应式绑定请自行引入并管理订阅。
/// </summary>
public abstract class ViewBase : MonoBehaviour, IView
{
    private readonly List<IDisposable> _disposables = new();
    private bool _initialized;

    /// <inheritdoc/>
    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    /// <inheritdoc/>
    void IView.Initialize()
    {
        if (_initialized)
        {
            throw new InvalidOperationException($"View '{GetType().Name}' 已经初始化过，禁止重复初始化。");
        }

        _initialized = true;
        OnInitialize();
    }

    /// <inheritdoc/>
    async UniTask IView.HideAsync()
    {
        await OnHideAsync();
    }

    /// <summary>注册一个跟随 View 实例生命周期释放的订阅。</summary>
    /// <param name="disposable">订阅对象，销毁时自动 Dispose。</param>
    protected void AddDisposable(IDisposable disposable)
    {
        _disposables.Add(disposable ?? throw new ArgumentNullException(nameof(disposable)));
    }

    /// <summary>子类可选：Initialize 时调用一次，适合缓存控件引用、启动入场动画等初始化。</summary>
    protected virtual void OnInitialize()
    {
    }

    /// <summary>子类可选：UI 关闭前的出场动画或清理，框架等待 UniTask 完成后再释放实例。</summary>
    /// <returns>表示异步操作的 UniTask。</returns>
    protected virtual UniTask OnHideAsync() => UniTask.CompletedTask;

    /// <summary>Unity 生命周期：销毁时释放所有已注册订阅。</summary>
    protected virtual void OnDestroy()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _disposables.Clear();
    }
}
