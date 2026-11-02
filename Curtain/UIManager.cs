// <copyright file="UIManager.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curtain.Abstraction;
using Curtain.Core;

/// <summary>
/// UI 管理器，负责 UI 的打开、关闭、层级与激活管理。
/// 不感知 ViewModel：打开参数通过 <see cref="IViewWithParameter{TParam}"/> 注入 View，
/// 对话框结果句柄通过 <see cref="IViewWithResult"/> 注入 View，VM 的创建与生命周期由 View 自持。
/// 不管理打开/关闭动画：入场动画由 View 在 <see cref="IView.SetActive"/> 中自行启动；
/// 关闭时框架依次调用 <see cref="IView.BeforeClose"/> 与 <see cref="IView.BeforeCloseAsync"/>，
/// 最后一个必调钩子结束后交还 <see cref="IUIResourceLoader"/> 释放实例。
/// 所有改变导航状态的操作进入严格 FIFO 的命令队列串行执行；
/// 生命周期钩子内不要等待导航 API，否则会等待队列中的自身命令而无法完成。
/// 通过 <see cref="IUIResourceLoader"/> 加载实例，通过 <see cref="IUIRoot"/> 获取各层级父节点。
/// 本类型不依赖任何引擎类型，引擎桥接由宿主实现。
/// </summary>
public sealed class UIManager
{
    private readonly ILogger _logger;
    private readonly IUIResourceLoader _loader;
    private readonly IUIRoot _root;

    // 状态索引
    private readonly object _stateLock = new object();
    private readonly Dictionary<int, Entry> _openById = new Dictionary<int, Entry>();
    private readonly Dictionary<Type, List<int>> _openByType = new Dictionary<Type, List<int>>();
    private readonly List<Entry> _stack = new List<Entry>();

    // 命令队列锁：_queueTail 只以成功状态完成，保证一次失败不会阻断后续命令
    private readonly object _queueGate = new object();

    // Main 层当前页面，null 表示无
    private Entry _main;

    // ID 自增计数器
    private int _nextId = 1;

    // 命令队列尾
    private Task _queueTail = Task.CompletedTask;

    /// <summary>初始化 UIManager。</summary>
    /// <param name="loader">UI 资源加载器。</param>
    /// <param name="root">UI 根节点。</param>
    /// <param name="logger">可选的日志实现，默认 <see cref="NullLogger"/>（不输出）；核心不依赖任何日志框架，宿主实现 <see cref="ILogger"/> 后传入。</param>
    public UIManager(IUIResourceLoader loader, IUIRoot root, ILogger logger = null)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>获取 Stack 层最上方界面（可能是对话框）的句柄；Stack 层为空时返回 null。</summary>
    public UIHandle Top
    {
        get
        {
            lock (_stateLock)
            {
                if (_stack.Count == 0)
                {
                    return null;
                }

                return _stack[_stack.Count - 1].Handle;
            }
        }
    }

    /// <summary>是否已打开 Main 层界面。</summary>
    public bool HasMain
    {
        get
        {
            lock (_stateLock)
            {
                return _main != null;
            }
        }
    }

    /// <summary>当前 Main 层界面的句柄；没有 Main 时返回 null。</summary>
    public UIHandle CurrentMain
    {
        get
        {
            lock (_stateLock)
            {
                return _main?.Handle;
            }
        }
    }

    /// <summary>打开 Main 层页面，无参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <param name="mode">已有 Main 时的处理策略，默认拒绝。</param>
    /// <param name="cancellationToken">取消令牌，仅在界面摘除前生效。</param>
    /// <returns>页面句柄。</returns>
    public ValueTask<UIHandle> OpenMainAsync<TView>(
        MainOpenMode mode = MainOpenMode.Reject,
        CancellationToken cancellationToken = default)
        where TView : IView
    {
        return new ValueTask<UIHandle>(
            EnqueueAsync(token => OpenMainCoreAsync<TView>(mode, null, token), cancellationToken));
    }

    /// <summary>打开 Main 层页面，带参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TParam">参数类型。</typeparam>
    /// <param name="parameter">页面参数。</param>
    /// <param name="mode">已有 Main 时的处理策略，默认拒绝。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>页面句柄。</returns>
    public ValueTask<UIHandle> OpenMainAsync<TView, TParam>(
        TParam parameter,
        MainOpenMode mode = MainOpenMode.Reject,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithParameter<TParam>
    {
        return new ValueTask<UIHandle>(
            EnqueueAsync(
                token => OpenMainCoreAsync<TView>(
                    mode,
                    (view, handle) => view.SetParameter(parameter),
                    token),
                cancellationToken));
    }

    /// <summary>打开 Stack 或 Top 层页面，无参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <param name="layer">UI 层级（Stack 或 Top）。</param>
    /// <param name="mode">打开模式；null 时使用 <see cref="IView.Mode"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>页面句柄。</returns>
    public ValueTask<UIHandle> OpenPageAsync<TView>(
        UILayer layer,
        UIOpenMode? mode = null,
        CancellationToken cancellationToken = default)
        where TView : IView
    {
        ValidatePageLayer(layer);
        return new ValueTask<UIHandle>(
            EnqueueAsync(token => OpenPageCommandAsync<TView>(layer, mode, null, token), cancellationToken));
    }

    /// <summary>打开 Stack 或 Top 层页面，带参数。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TParam">参数类型。</typeparam>
    /// <param name="parameter">页面参数。</param>
    /// <param name="layer">UI 层级（Stack 或 Top）。</param>
    /// <param name="mode">打开模式；null 时使用 <see cref="IView.Mode"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>页面句柄。</returns>
    public ValueTask<UIHandle> OpenPageAsync<TView, TParam>(
        TParam parameter,
        UILayer layer,
        UIOpenMode? mode = null,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithParameter<TParam>
    {
        ValidatePageLayer(layer);
        return new ValueTask<UIHandle>(
            EnqueueAsync(
                token => OpenPageCommandAsync<TView>(
                    layer,
                    mode,
                    view => view.SetParameter(parameter),
                    token),
                cancellationToken));
    }

    /// <summary>打开对话框（无参数），返回带结果的句柄。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="parent">父界面句柄。</param>
    /// <param name="mode">打开模式；null 时使用 <see cref="IView.Mode"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>带返回值的对话框句柄。</returns>
    public ValueTask<UIHandle<TResult>> OpenDialogAsync<TView, TResult>(
        UIHandle parent,
        UIOpenMode? mode = null,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithResult
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        return new ValueTask<UIHandle<TResult>>(
            EnqueueAsync(token => OpenDialogCommandAsync<TView, TResult>(parent, mode, null, token), cancellationToken));
    }

    /// <summary>打开对话框（带参数），返回带结果的句柄。</summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <typeparam name="TParam">参数类型。</typeparam>
    /// <typeparam name="TResult">返回值类型。</typeparam>
    /// <param name="parameter">对话框参数。</param>
    /// <param name="parent">父界面句柄。</param>
    /// <param name="mode">打开模式；null 时使用 <see cref="IView.Mode"/>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>带返回值的对话框句柄。</returns>
    public ValueTask<UIHandle<TResult>> OpenDialogAsync<TView, TParam, TResult>(
        TParam parameter,
        UIHandle parent,
        UIOpenMode? mode = null,
        CancellationToken cancellationToken = default)
        where TView : IView, IViewWithParameter<TParam>, IViewWithResult
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        return new ValueTask<UIHandle<TResult>>(
            EnqueueAsync(
                token => OpenDialogCommandAsync<TView, TResult>(
                    parent,
                    mode,
                    view => view.SetParameter(parameter),
                    token),
                cancellationToken));
    }

    /// <summary>
    /// 关闭 Stack 层最上方的独立界面及其后代；最上方是对话框时，关闭其所在的独立顶级界面。
    /// </summary>
    /// <param name="cancellationToken">取消令牌，仅在第一个界面摘除前生效。</param>
    /// <returns>关闭了至少一个界面时为 true；Stack 层为空时为 false。</returns>
    public ValueTask<bool> CloseTopAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(
            EnqueueAsync(token => CloseTopCoreAsync(token), cancellationToken));
    }

    /// <summary>关闭指定界面及其后代；已关闭时立即完成，多次调用共享同一次关闭的完成。</summary>
    /// <param name="handle">要关闭的界面句柄。</param>
    /// <param name="cancellationToken">取消令牌，仅在界面摘除前生效。</param>
    /// <returns>关闭完成时结束的 ValueTask。</returns>
    public ValueTask CloseAsync(UIHandle handle, CancellationToken cancellationToken = default)
    {
        if (handle == null)
        {
            throw new ArgumentNullException(nameof(handle));
        }

        return handle.RequestCloseAsync(CloseReason.Normal, cancellationToken);
    }

    /// <summary>关闭 Stack 层所有独立界面及其后代，保留 Main 与 Top 层。</summary>
    /// <param name="cancellationToken">取消令牌，仅在第一个界面摘除前生效。</param>
    /// <returns>全部关闭完成时结束的 ValueTask。</returns>
    public ValueTask BackToMainAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask(
            EnqueueAsync(token => BackToMainCoreAsync(token), cancellationToken));
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
        lock (_stateLock)
        {
            return _openByType.TryGetValue(typeof(TView), out List<int> ids) ? ids.Count : 0;
        }
    }

    /// <summary>获取指定层级当前打开的实例数量。</summary>
    /// <param name="layer">UI 层级。</param>
    /// <returns>该层级的打开实例数量。</returns>
    public int GetOpenCount(UILayer layer)
    {
        lock (_stateLock)
        {
            int count = 0;
            foreach (Entry entry in _openById.Values)
            {
                if (entry.Layer == layer)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>在 Effect 层播放特效。</summary>
    /// <typeparam name="TEffect">特效类型。</typeparam>
    /// <param name="effect">特效参数。</param>
    public void PlayEffect<TEffect>(TEffect effect)
        where TEffect : IUIEffect
    {
        // TODO: 特效系统实现（对象池 + DOTween）
        LogDebug($"PlayEffect: {typeof(TEffect).Name}");
    }

    /// <summary>框架内部：把句柄的关闭请求加入命令队列。</summary>
    /// <param name="handle">要关闭的界面句柄。</param>
    /// <param name="reason">关闭原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>关闭完成时结束的任务。</returns>
    internal Task CloseHandleAsync(UIHandle handle, CloseReason reason, CancellationToken cancellationToken)
    {
        return EnqueueAsync(token => CloseHandleCoreAsync(handle, reason, token), cancellationToken);
    }

    private static void ValidatePageLayer(UILayer layer)
    {
        if (layer != UILayer.Stack && layer != UILayer.Top)
        {
            throw new ArgumentException($"OpenPageAsync 仅支持 Stack 或 Top 层级，当前传入: {layer}", nameof(layer));
        }
    }

    private static void PlanActivation(Entry entry, bool visible, List<Entry> toActivate, List<Entry> toDeactivate)
    {
        if (entry.IsActive == visible)
        {
            return;
        }

        entry.IsActive = visible;
        if (visible)
        {
            toActivate.Add(entry);
        }
        else
        {
            toDeactivate.Add(entry);
        }
    }

    /// <summary>把命令接到队尾；命令在队列中按调用顺序串行执行。</summary>
    /// <typeparam name="T">结果类型。</typeparam>
    /// <param name="operation">命令本体。</param>
    /// <param name="cancellationToken">取消令牌，由命令在开始时检查。</param>
    /// <returns>命令完成的任务；失败或取消以异常形式返回。</returns>
    private Task<T> EnqueueAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<byte> slot = new TaskCompletionSource<byte>();
        Task previous;
        lock (_queueGate)
        {
            previous = _queueTail;
            _queueTail = slot.Task;
        }

        return RunSerialAsync(previous, operation, cancellationToken, slot);
    }

    /// <summary>把无返回值的命令接到队尾。</summary>
    /// <param name="operation">命令本体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命令完成的任务。</returns>
    private Task EnqueueAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<byte> slot = new TaskCompletionSource<byte>();
        Task previous;
        lock (_queueGate)
        {
            previous = _queueTail;
            _queueTail = slot.Task;
        }

        return RunSerialAsync(previous, operation, cancellationToken, slot);
    }

    private async Task<T> RunSerialAsync<T>(
        Task previous,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken,
        TaskCompletionSource<byte> slot)
    {
        try
        {
            await previous;
            return await operation(cancellationToken);
        }
        finally
        {
            slot.TrySetResult(0);
        }
    }

    private async Task RunSerialAsync(
        Task previous,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken,
        TaskCompletionSource<byte> slot)
    {
        try
        {
            await previous;
            await operation(cancellationToken);
        }
        finally
        {
            slot.TrySetResult(0);
        }
    }

    private async Task<UIHandle> OpenMainCoreAsync<TView>(
        MainOpenMode mode,
        Action<TView, UIHandle> configure,
        CancellationToken cancellationToken)
        where TView : IView
    {
        cancellationToken.ThrowIfCancellationRequested();

        Entry oldMain;
        lock (_stateLock)
        {
            oldMain = _main;
        }

        if (oldMain != null && mode == MainOpenMode.Reject)
        {
            throw new UIOpenException($"Main 界面已存在（Id: {oldMain.Id}），Reject 模式下不能重复打开。");
        }

        Entry entry = await OpenEntryAsync<TView>(
            UILayer.Main,
            -1,
            null,
            configure,
            id => new UIHandle(id, this),
            cancellationToken).ConfigureAwait(false);

        lock (_stateLock)
        {
            _main = entry;
        }

        RecomputeActivation();

        if (oldMain != null)
        {
            await CloseEntryTreeAsync(oldMain, CloseReason.Normal, CancellationToken.None).ConfigureAwait(false);
        }

        return entry.Handle;
    }

    private async Task<UIHandle> OpenPageCommandAsync<TView>(
        UILayer layer,
        UIOpenMode? requestedMode,
        Action<TView> parameterSetter,
        CancellationToken cancellationToken)
        where TView : IView
    {
        Entry entry = await OpenEntryAsync<TView>(
            layer,
            -1,
            requestedMode,
            (view, handle) =>
            {
                if (parameterSetter != null)
                {
                    parameterSetter(view);
                }
            },
            id => new UIHandle(id, this),
            cancellationToken).ConfigureAwait(false);

        return entry.Handle;
    }

    private async Task<UIHandle<TResult>> OpenDialogCommandAsync<TView, TResult>(
        UIHandle parent,
        UIOpenMode? requestedMode,
        Action<TView> parameterSetter,
        CancellationToken cancellationToken)
        where TView : IView, IViewWithResult
    {
        cancellationToken.ThrowIfCancellationRequested();

        GetOpenEntry(parent, "父界面");

        Entry entry = await OpenEntryAsync<TView>(
            UILayer.Stack,
            parent.Id,
            requestedMode,
            (view, handle) =>
            {
                if (parameterSetter != null)
                {
                    parameterSetter(view);
                }

                view.SetResultHandle(handle);
            },
            id => new UIHandle<TResult>(id, this),
            cancellationToken).ConfigureAwait(false);

        lock (_stateLock)
        {
            if (_openById.TryGetValue(parent.Id, out Entry currentParent))
            {
                currentParent.ChildIds.Add(entry.Id);
            }
        }

        return (UIHandle<TResult>)entry.Handle;
    }

    private async Task<bool> CloseTopCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Entry root;
        lock (_stateLock)
        {
            root = FindTopStackRoot();
        }

        if (root == null)
        {
            return false;
        }

        await CloseEntryTreeAsync(root, CloseReason.Normal, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task BackToMainCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            Entry root;
            lock (_stateLock)
            {
                root = FindTopStackRoot();
            }

            if (root == null)
            {
                return;
            }

            await CloseEntryTreeAsync(root, CloseReason.Normal, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task CloseHandleCoreAsync(
        UIHandle handle,
        CloseReason reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Entry entry;
        lock (_stateLock)
        {
            entry = _openById.TryGetValue(handle.Id, out Entry found) ? found : null;
        }

        if (entry == null || entry.Closing)
        {
            return;
        }

        await CloseEntryTreeAsync(entry, reason, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 打开一个 View 并注册：实例化 -> 隐藏 -> 配置 -> 注册 -> Initialize -> 显示 -> 重算激活。
    /// 任一步骤失败都会摘除注册、执行 OpenFailed 生命周期并释放实例。
    /// </summary>
    /// <typeparam name="TView">View 类型。</typeparam>
    /// <param name="layer">UI 层级。</param>
    /// <param name="parentId">父界面 id；无父时为 -1。</param>
    /// <param name="requestedMode">显式打开模式；null 时使用 <see cref="IView.Mode"/>。</param>
    /// <param name="configure">在 Initialize 前配置 View 与句柄的回调。</param>
    /// <param name="createHandle">按 id 创建句柄的工厂。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已注册的界面记录。</returns>
    private async Task<Entry> OpenEntryAsync<TView>(
        UILayer layer,
        int parentId,
        UIOpenMode? requestedMode,
        Action<TView, UIHandle> configure,
        Func<int, UIHandle> createHandle,
        CancellationToken cancellationToken)
        where TView : IView
    {
        cancellationToken.ThrowIfCancellationRequested();

        Type viewType = typeof(TView);
        LogDebug($"Opening UI: {viewType.Name} (Layer: {layer})");

        UIAttribute attribute = (UIAttribute)Attribute.GetCustomAttribute(viewType, typeof(UIAttribute));
        if (attribute == null)
        {
            throw new UIOpenException($"View '{viewType.Name}' 缺少 [{nameof(UIAttribute)}] 标注，无法打开。");
        }

        IViewContainer layerRoot = _root.GetLayerRoot(layer);
        LogDebug($"Loading Prefab: {attribute.PrefabPath}");

        IView createdView = null;
        Entry entry = null;
        UIHandle handle = null;

        try
        {
            TView view = await _loader.InstantiateAsync<TView>(attribute.PrefabPath, layerRoot, cancellationToken);
            createdView = view;
            if (view == null)
            {
                throw new UIOpenException($"加载 Prefab 失败: {attribute.PrefabPath}");
            }

            view.SetActive(false);

            int id = NextId();
            handle = createHandle(id);
            if (configure != null)
            {
                configure(view, handle);
            }

            entry = new Entry
            {
                Id = id,
                ParentId = parentId,
                Layer = layer,
                View = view,
                Handle = handle,
                Mode = requestedMode ?? view.Mode,
            };

            handle.AttachView(view);
            Register(entry);

            view.Initialize();

            entry.IsActive = true;
            view.SetActive(true);
            RecomputeActivation();

            LogInfo($"UI opened: {viewType.Name} (Id: {id}, Layer: {layer})");
            return entry;
        }
        catch (OperationCanceledException)
        {
            await CleanupFailedOpenAsync(createdView, entry, handle, viewType).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await CleanupFailedOpenAsync(createdView, entry, handle, viewType).ConfigureAwait(false);
            LogError($"打开 {viewType.Name} 失败", ex);

            if (ex is UIOpenException)
            {
                throw;
            }

            throw new UIOpenException($"打开 {viewType.Name} 失败", ex);
        }
    }

    /// <summary>打开失败清理：摘除注册、执行 OpenFailed 生命周期、释放实例并结束句柄。</summary>
    /// <param name="view">已实例化的视图，可能为 null。</param>
    /// <param name="entry">已注册的界面记录，可能为 null。</param>
    /// <param name="handle">已创建的句柄，可能为 null。</param>
    /// <param name="viewType">视图类型（仅用于日志）。</param>
    /// <returns>清理完成时结束的任务。</returns>
    private async Task CleanupFailedOpenAsync(IView view, Entry entry, UIHandle handle, Type viewType)
    {
        if (entry != null)
        {
            Detach(entry);
        }

        if (view != null)
        {
            await RunBeforeCloseAsync(view, CloseReason.OpenFailed, CancellationToken.None).ConfigureAwait(false);
            ReleaseView(view, viewType);
        }

        if (handle != null)
        {
            handle.MarkClosed();
        }
    }

    /// <summary>
    /// 关闭一个界面及其后代：先摘除并重算激活，再从新到旧关闭子项，
    /// 然后执行 BeforeClose 生命周期并交还 loader 释放。
    /// 关闭开始后视图保持当前激活状态直到释放。
    /// </summary>
    /// <param name="entry">要关闭的界面记录。</param>
    /// <param name="reason">关闭原因。</param>
    /// <param name="cancellationToken">取消令牌，仅在第一个界面摘除前生效。</param>
    /// <returns>关闭完成时结束的任务。</returns>
    private async Task CloseEntryTreeAsync(Entry entry, CloseReason reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int[] childIds;
        lock (_stateLock)
        {
            childIds = entry.ChildIds.ToArray();
            entry.ChildIds.Clear();
        }

        Detach(entry);

        for (int i = childIds.Length - 1; i >= 0; i--)
        {
            Entry child;
            lock (_stateLock)
            {
                child = _openById.TryGetValue(childIds[i], out Entry found) ? found : null;
            }

            if (child != null && !child.Closing)
            {
                await CloseEntryTreeAsync(child, CloseReason.ParentClosed, CancellationToken.None).ConfigureAwait(false);
            }
        }

        await RunBeforeCloseAsync(entry.View, reason, CancellationToken.None).ConfigureAwait(false);
        ReleaseView(entry.View, entry.View.GetType());
        entry.Handle.MarkClosed();

        LogInfo($"UI closed: {entry.View.GetType().Name} (Id: {entry.Id})");
    }

    /// <summary>从注册表、Stack 顺序、父子关系与 Main 引用中摘除界面，并立即重算激活。</summary>
    /// <param name="entry">要摘除的界面记录。</param>
    private void Detach(Entry entry)
    {
        lock (_stateLock)
        {
            if (entry.Closing)
            {
                return;
            }

            entry.Closing = true;
            _openById.Remove(entry.Id);

            Type viewType = entry.View.GetType();
            if (_openByType.TryGetValue(viewType, out List<int> ids))
            {
                ids.Remove(entry.Id);
                if (ids.Count == 0)
                {
                    _openByType.Remove(viewType);
                }
            }

            if (entry.Layer == UILayer.Stack)
            {
                _stack.Remove(entry);
            }

            if (entry.ParentId >= 0 && _openById.TryGetValue(entry.ParentId, out Entry parent))
            {
                parent.ChildIds.Remove(entry.Id);
            }

            if (ReferenceEquals(_main, entry))
            {
                _main = null;
            }
        }

        entry.Handle.MarkDetached();
        RecomputeActivation();
    }

    /// <summary>
    /// 重算激活：最后一个 Exclusive 及其上方视图可见，其下方隐藏；
    /// 没有 Exclusive 时全部堆叠项可见，Main 仅在无 Exclusive 时可见。
    /// </summary>
    private void RecomputeActivation()
    {
        List<Entry> toActivate = new List<Entry>();
        List<Entry> toDeactivate = new List<Entry>();

        lock (_stateLock)
        {
            int lastExclusive = -1;
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].Mode == UIOpenMode.Exclusive)
                {
                    lastExclusive = i;
                }
            }

            for (int i = 0; i < _stack.Count; i++)
            {
                bool visible = i >= lastExclusive;
                PlanActivation(_stack[i], visible, toActivate, toDeactivate);
            }

            if (_main != null)
            {
                PlanActivation(_main, lastExclusive < 0, toActivate, toDeactivate);
            }
        }

        foreach (Entry entry in toDeactivate)
        {
            SetActiveSafe(entry, false);
        }

        foreach (Entry entry in toActivate)
        {
            SetActiveSafe(entry, true);
        }
    }

    private void SetActiveSafe(Entry entry, bool active)
    {
        try
        {
            entry.View.SetActive(active);
        }
        catch (Exception ex)
        {
            LogWarning($"SetActive 执行失败: {entry.View.GetType().Name}", ex);
        }
    }

    /// <summary>在 State 锁内调用：取 Stack 层最上方界面所在的 Stack 顶级祖先。</summary>
    /// <returns>Stack 顶级祖先；Stack 层为空时返回 null。</returns>
    private Entry FindTopStackRoot()
    {
        if (_stack.Count == 0)
        {
            return null;
        }

        Entry entry = _stack[_stack.Count - 1];
        while (entry.ParentId >= 0
            && _openById.TryGetValue(entry.ParentId, out Entry parent)
            && parent.Layer == UILayer.Stack)
        {
            entry = parent;
        }

        return entry;
    }

    private Entry GetOpenEntry(UIHandle handle, string role)
    {
        lock (_stateLock)
        {
            if (!_openById.TryGetValue(handle.Id, out Entry entry))
            {
                throw new UIOpenException($"{role} '{handle.Id}' 已关闭，无法打开对话框。");
            }

            return entry;
        }
    }

    private int NextId()
    {
        lock (_stateLock)
        {
            return _nextId++;
        }
    }

    /// <summary>在 State 锁内完成索引登记。</summary>
    /// <param name="entry">要登记的界面记录。</param>
    private void Register(Entry entry)
    {
        lock (_stateLock)
        {
            _openById[entry.Id] = entry;

            Type viewType = entry.View.GetType();
            if (!_openByType.TryGetValue(viewType, out List<int> ids))
            {
                ids = new List<int>();
                _openByType[viewType] = ids;
            }

            ids.Add(entry.Id);

            if (entry.Layer == UILayer.Stack)
            {
                _stack.Add(entry);
            }
        }
    }

    /// <summary>执行关闭生命周期：同步钩子与异步钩子的异常都只记录日志。</summary>
    /// <param name="view">要关闭的视图。</param>
    /// <param name="reason">关闭原因。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步钩子完成时结束的任务。</returns>
    private async Task RunBeforeCloseAsync(IView view, CloseReason reason, CancellationToken cancellationToken)
    {
        Type viewType = view.GetType();

        try
        {
            view.BeforeClose(reason);
        }
        catch (Exception ex)
        {
            LogWarning($"BeforeClose 执行失败: {viewType.Name}", ex);
        }

        try
        {
            await view.BeforeCloseAsync(reason, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogWarning($"BeforeCloseAsync 执行失败: {viewType.Name}", ex);
        }
    }

    /// <summary>释放视图；Release 自身出错只记录日志，不阻断关闭流程。</summary>
    /// <param name="view">要释放的视图。</param>
    /// <param name="viewType">视图类型（仅用于日志）。</param>
    private void ReleaseView(IView view, Type viewType)
    {
        try
        {
            _loader.Release(view);
        }
        catch (Exception ex)
        {
            LogError($"Release 执行失败: {viewType.Name}", ex);
        }
    }

    // 宿主日志实现自身抛异常时静默跳过，避免日志问题打断 UI 生命周期流程。
    private void LogDebug(string message)
    {
        try
        {
            _logger.LogDebug(message);
        }
        catch (Exception)
        {
        }
    }

    private void LogInfo(string message)
    {
        try
        {
            _logger.LogInfo(message);
        }
        catch (Exception)
        {
        }
    }

    private void LogWarning(string message, Exception exception = null)
    {
        try
        {
            _logger.LogWarning(message, exception);
        }
        catch (Exception)
        {
        }
    }

    private void LogError(string message, Exception exception = null)
    {
        try
        {
            _logger.LogError(message, exception);
        }
        catch (Exception)
        {
        }
    }

    private sealed class Entry
    {
        public int Id { get; set; }

        public int ParentId { get; set; } = -1;

        public UILayer Layer { get; set; }

        public IView View { get; set; }

        public UIHandle Handle { get; set; }

        public UIOpenMode Mode { get; set; }

        public List<int> ChildIds { get; } = new List<int>();

        public bool IsActive { get; set; }

        public bool Closing { get; set; }
    }
}
