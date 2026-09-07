// <copyright file="UIManager.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain;

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Curtain.Abstraction;
using Curtain.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// UI 管理器，负责 UI 的打开、关闭、层级管理。
/// 不感知 ViewModel：打开参数通过 <see cref="IViewWithParameter{TParam}"/> 注入 View，
/// 对话框结果句柄通过 <see cref="IViewWithResult"/> 注入 View，VM 的创建与生命周期由 View 自持。
/// 不管理打开/关闭动画：入场动画由 View 自行启动，框架关闭前通过 <see cref="IView.HideAsync"/> 等待出场动画完成。
/// 通过 <see cref="IUIResourceLoader"/> 加载 Prefab，通过 <see cref="IUIRoot"/> 获取各层级父节点。
/// 本类型不依赖任何 Unity 类型，Unity 桥接由 Curtain.Unity 实现。
/// </summary>
public sealed class UIManager
{
    private readonly ILogger _logger;
    private readonly IUIResourceLoader _loader;
    private readonly IUIRoot _root;

    // 多开管理
    private readonly Dictionary<int, Entry> _openById = new();
    private readonly Dictionary<Type, List<int>> _openByType = new();

    // ID 自增计数器
    private int _nextId = 1;

    // Main 层当前页面（-1 表示无）
    private int _currentMainId = -1;

    /// <summary>初始化 UIManager。</summary>
    /// <param name="loader">UI 资源加载器。</param>
    /// <param name="root">UI 根节点。</param>
    /// <param name="logger">可选的日志记录器，默认使用 NullLogger。</param>
    public UIManager(IUIResourceLoader loader, IUIRoot root, ILogger logger = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>打开 Main 层页面（自动关闭旧 Main 页面），无参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>页面句柄。</returns>
    public async UniTask<UIHandle> OpenMainAsync<TView>(CancellationToken cancellationToken = default)
        where TView : IView
    {
        // 关闭旧 Main 页面
        if (_currentMainId >= 0)
        {
            await CloseInternalAsync(_currentMainId);
        }

        return await OpenPageInternalAsync<TView>(UILayer.Main, cancellationToken);
    }

    /// <summary>打开 Main 层页面（自动关闭旧 Main 页面），带参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TParam">参数类型。</typeparam>
    /// <param name="parameter">页面参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>页面句柄。</returns>
    public async UniTask<UIHandle> OpenMainAsync<TView, TParam>(
        TParam parameter,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithParameter<TParam>
    {
        // 关闭旧 Main 页面
        if (_currentMainId >= 0)
        {
            await CloseInternalAsync(_currentMainId);
        }

        return await OpenPageInternalAsync<TView, TParam>(parameter, UILayer.Main, cancellationToken);
    }

    /// <summary>打开 Stack 或 Top 层页面，无参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <param name="layer">UI 层级（Stack 或 Top）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>页面句柄。</returns>
    public async UniTask<UIHandle> OpenPageAsync<TView>(
        UILayer layer,
        CancellationToken cancellationToken = default)
        where TView : IView
    {
        ValidatePageLayer(layer);
        return await OpenPageInternalAsync<TView>(layer, cancellationToken);
    }

    /// <summary>打开 Stack 或 Top 层页面，带参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TParam">参数类型。</typeparam>
    /// <param name="parameter">页面参数。</param>
    /// <param name="layer">UI 层级（Stack 或 Top）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>页面句柄。</returns>
    public async UniTask<UIHandle> OpenPageAsync<TView, TParam>(
        TParam parameter,
        UILayer layer,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithParameter<TParam>
    {
        ValidatePageLayer(layer);
        return await OpenPageInternalAsync<TView, TParam>(parameter, layer, cancellationToken);
    }

    /// <summary>打开对话框（无参数），返回带结果的句柄。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="parent">父界面句柄。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>带返回值的对话框句柄。</returns>
    public async UniTask<UIHandle<TResult>> OpenDialogAsync<TView, TResult>(
        UIHandle parent,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithResult
    {
        var parentEntry = ValidateDialogParent(parent);
        var id = _nextId++;
        var dialogHandle = new UIHandle<TResult>(id, () => CloseById(id));
        await OpenInternalAsync<TView>(
            UILayer.Stack,
            parent.Id,
            dialogHandle,
            cancellationToken,
            view => view.SetResultHandle(dialogHandle));

        parentEntry.ChildIds.Add(id);

        _logger.LogInformation("Dialog opened: {ViewType} (Parent: {ParentId})", typeof(TView).Name, parent.Id);
        return dialogHandle;
    }

    /// <summary>打开对话框（带参数），返回带结果的句柄。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TParam">参数类型。</typeparam>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="parameter">对话框参数。</param>
    /// <param name="parent">父界面句柄。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>带返回值的对话框句柄。</returns>
    public async UniTask<UIHandle<TResult>> OpenDialogAsync<TView, TParam, TResult>(
        TParam parameter,
        UIHandle parent,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithParameter<TParam>, IViewWithResult
    {
        var parentEntry = ValidateDialogParent(parent);
        var id = _nextId++;
        var dialogHandle = new UIHandle<TResult>(id, () => CloseById(id));
        await OpenInternalAsync<TView, TParam>(
            parameter,
            UILayer.Stack,
            parent.Id,
            dialogHandle,
            cancellationToken,
            view => view.SetResultHandle(dialogHandle));

        parentEntry.ChildIds.Add(id);

        _logger.LogInformation("Dialog opened: {ViewType} (Parent: {ParentId})", typeof(TView).Name, parent.Id);
        return dialogHandle;
    }

    /// <summary>关闭 Stack 层所有页面（级联关闭对话框）。</summary>
    public void CloseAll()
    {
        // 收集 Stack 层所有页面（不包含 Main/Top）
        var toClose = new List<int>();
        foreach (var kvp in _openById)
        {
            if (kvp.Value.Layer == UILayer.Stack && kvp.Value.ParentId < 0)
            {
                toClose.Add(kvp.Key);
            }
        }

        foreach (var id in toClose)
        {
            CloseById(id);
        }
    }

    /// <summary>查询指定类型是否有实例打开。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <returns>如果有至少一个实例打开则返回 true。</returns>
    public bool IsOpen<TView>()
        where TView : IView
    {
        return GetOpenCount<TView>() > 0;
    }

    /// <summary>获取指定类型的打开实例数量。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <returns>打开实例数量。</returns>
    public int GetOpenCount<TView>()
        where TView : IView
    {
        var viewType = typeof(TView);
        return _openByType.TryGetValue(viewType, out var ids) ? ids.Count : 0;
    }

    /// <summary>在 Effect 层播放特效。</summary>
    /// <typeparam name="TEffect">特效类型。</typeparam>
    /// <param name="effect">特效参数。</param>
    public void PlayEffect<TEffect>(TEffect effect)
        where TEffect : IUIEffect
    {
        // TODO: 特效系统实现（对象池 + DOTween）
        _logger.LogDebug("PlayEffect: {EffectType}", typeof(TEffect).Name);
    }

    private static void ValidatePageLayer(UILayer layer)
    {
        if (layer != UILayer.Stack && layer != UILayer.Top)
        {
            throw new ArgumentException($"OpenPageAsync 仅支持 Stack 或 Top 层级，当前传入: {layer}", nameof(layer));
        }
    }

    private async UniTask<UIHandle> OpenPageInternalAsync<TView>(
        UILayer layer,
        CancellationToken cancellationToken)
        where TView : IView
    {
        var id = _nextId++;
        var handle = new UIHandle(id, () => CloseById(id));
        await OpenInternalAsync<TView>(
            layer,
            -1,
            handle,
            cancellationToken,
            _ =>
            {
            });

        if (layer == UILayer.Main)
        {
            _currentMainId = id;
        }

        return handle;
    }

    private async UniTask<UIHandle> OpenPageInternalAsync<TView, TParam>(
        TParam parameter,
        UILayer layer,
        CancellationToken cancellationToken)
        where TView : IView, IViewWithParameter<TParam>
    {
        var id = _nextId++;
        var handle = new UIHandle(id, () => CloseById(id));
        await OpenInternalAsync<TView, TParam>(
            parameter,
            layer,
            -1,
            handle,
            cancellationToken,
            _ =>
            {
            });

        if (layer == UILayer.Main)
        {
            _currentMainId = id;
        }

        return handle;
    }

    /// <summary>无参数打开内部链。</summary>
    private async UniTask<Entry> OpenInternalAsync<TView>(
        UILayer layer,
        int parentId,
        UIHandle handle,
        CancellationToken cancellationToken,
        Action<TView> configureView)
        where TView : IView
    {
        return await OpenInternalCoreAsync<TView>(layer, parentId, handle, cancellationToken, configureView);
    }

    /// <summary>
    /// 带参数打开内部链：编译期强制 <typeparamref name="TView"/> 实现 <see cref="IViewWithParameter{TParam}"/>，
    /// 在 View 初始化前注入参数。
    /// </summary>
    private async UniTask<Entry> OpenInternalAsync<TView, TParam>(
        TParam parameter,
        UILayer layer,
        int parentId,
        UIHandle handle,
        CancellationToken cancellationToken,
        Action<TView> configureView)
        where TView : IView, IViewWithParameter<TParam>
    {
        return await OpenInternalCoreAsync<TView>(
            layer,
            parentId,
            handle,
            cancellationToken,
            view =>
            {
                view.SetParameter(parameter);
                configureView(view);
            });
    }

    private async UniTask<Entry> OpenInternalCoreAsync<TView>(
        UILayer layer,
        int parentId,
        UIHandle handle,
        CancellationToken cancellationToken,
        Action<TView> configureView)
        where TView : IView
    {
        var viewType = typeof(TView);
        _logger.LogDebug("Opening UI: {ViewType} (Layer: {Layer})", viewType.Name, layer);

        try
        {
            // 1. 读取 UIAttribute
            var attr = (UIAttribute)Attribute.GetCustomAttribute(viewType, typeof(UIAttribute));
            if (attr == null)
            {
                throw new UIOpenException($"View '{viewType.Name}' 缺少 [{nameof(UIAttribute)}] 标注，无法打开。");
            }

            // 2. 加载 Prefab 并取得 View（契约：返回未激活的视图）
            var layerRoot = _root.GetLayerRoot(layer);
            _logger.LogDebug("Loading Prefab: {PrefabPath}", attr.PrefabPath);
            var view = await _loader.InstantiateAsync<TView>(attr.PrefabPath, layerRoot, cancellationToken);
            if (view == null)
            {
                throw new UIOpenException($"加载 Prefab 失败: {attr.PrefabPath}");
            }

            view.SetActive(false);

            // 3. 注入打开参数 / 对话框结果句柄（由打开链决定，编译期类型安全）
            configureView(view);

            // 4. 初始化 View（View 自行完成业务初始化，框架不感知 ViewModel）
            view.Initialize();

            // 5. 显示（入场动画由 View 自行启动，框架不参与也不等待）
            view.SetActive(true);

            // 6. 注册
            var id = handle.Id;
            var entry = new Entry
            {
                Id = id,
                ParentId = parentId,
                Layer = layer,
                View = view,
                Handle = handle,
                ChildIds = new List<int>(),
            };

            _openById[id] = entry;

            if (!_openByType.TryGetValue(viewType, out var typeIds))
            {
                typeIds = new List<int>();
                _openByType[viewType] = typeIds;
            }

            typeIds.Add(id);

            _logger.LogInformation("UI opened: {ViewType} (Id: {Id}, Layer: {Layer})", viewType.Name, id, layer);
            return entry;
        }
        catch (Exception ex) when (ex is not UIOpenException)
        {
            throw new UIOpenException($"打开 {viewType.Name} 失败", ex);
        }
    }

    private Entry ValidateDialogParent(UIHandle parent)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        if (!_openById.TryGetValue(parent.Id, out var parentEntry))
        {
            throw new InvalidOperationException($"父界面 '{parent.Id}' 未找到，无法打开对话框。");
        }

        return parentEntry;
    }

    private void CloseById(int id)
    {
        if (!_openById.TryGetValue(id, out var entry))
        {
            return;
        }

        // 先级联关闭子对话框
        foreach (var childId in entry.ChildIds.ToArray())
        {
            CloseById(childId);
        }

        entry.ChildIds.Clear();

        // 从父节点移除
        if (entry.ParentId >= 0 && _openById.TryGetValue(entry.ParentId, out var parentEntry))
        {
            parentEntry.ChildIds.Remove(id);
        }

        // 如果是对话框，通知等待方被关闭
        entry.Handle.Dismiss();

        // 通知隐藏（等待出场动画，由 View 自行决定是否播放）
        entry.View.HideAsync().Forget();

        // 释放资源
        _loader.Release(entry.View);

        // 从索引移除
        _openById.Remove(id);

        var viewType = entry.View.GetType();
        if (_openByType.TryGetValue(viewType, out var typeIds))
        {
            typeIds.Remove(id);
            if (typeIds.Count == 0)
            {
                _openByType.Remove(viewType);
            }
        }

        // 清理 Main 引用
        if (_currentMainId == id)
        {
            _currentMainId = -1;
        }

        _logger.LogInformation("UI closed: {ViewType} (Id: {Id})", viewType.Name, id);
    }

    private async UniTask CloseInternalAsync(int id)
    {
        if (!_openById.TryGetValue(id, out var entry))
        {
            return;
        }

        // 先级联关闭子对话框
        foreach (var childId in entry.ChildIds.ToArray())
        {
            await CloseInternalAsync(childId);
        }

        entry.ChildIds.Clear();

        // 从父节点移除
        if (entry.ParentId >= 0 && _openById.TryGetValue(entry.ParentId, out var parentEntry))
        {
            parentEntry.ChildIds.Remove(id);
        }

        // 如果是对话框，通知等待方被关闭
        entry.Handle.Dismiss();

        // 通知隐藏（等待出场动画，由 View 自行决定是否播放）
        await entry.View.HideAsync();

        // 释放资源
        _loader.Release(entry.View);

        // 从索引移除
        _openById.Remove(id);

        var viewType = entry.View.GetType();
        if (_openByType.TryGetValue(viewType, out var typeIds))
        {
            typeIds.Remove(id);
            if (typeIds.Count == 0)
            {
                _openByType.Remove(viewType);
            }
        }

        // 清理 Main 引用
        if (_currentMainId == id)
        {
            _currentMainId = -1;
        }

        _logger.LogInformation("UI closed: {ViewType} (Id: {Id})", viewType.Name, id);
    }

    private sealed class Entry
    {
        public int Id { get; set; }

        public int ParentId { get; set; } = -1;

        public UILayer Layer { get; set; }

        public IView View { get; set; }

        public UIHandle Handle { get; set; }

        public List<int> ChildIds { get; set; }
    }
}
