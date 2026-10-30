// <copyright file="UIManagerTestDoubles.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

#pragma warning disable SA1402, SA1649 // 测试替身集中放置，允许单文件多类型

namespace Curtain.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Curtain.Abstraction;
using Curtain.Core;

/// <summary>
/// 生命周期事件记录器，供测试 View 记录框架调用时序。
/// 每个测试用例在 SetUp 时重置，避免用例间相互污染。
/// </summary>
public static class TestLifecycleLog
{
    /// <summary>事件序列。</summary>
    public static List<string> Events { get; set; } = new();

    /// <summary>追加一条事件。</summary>
    /// <param name="evt">事件名。</param>
    public static void Add(string evt)
    {
        Events.Add(evt);
    }
}

/// <summary>
/// 测试页面/对话框参数类型。
/// </summary>
public sealed class TestParam
{
    /// <summary>参数值。</summary>
    public int Value { get; set; }
}

/// <summary>
/// UI 资源加载器测试替身：按 View 类型创建测试 View 实例，
/// 记录加载/释放调用，可配置返回 null 或抛异常以模拟失败。
/// </summary>
public sealed class FakeUiResourceLoader : IUIResourceLoader
{
    private readonly List<string> _instantiatedPaths = new();
    private readonly List<IView> _released = new();

    /// <summary>是否让 InstantiateAsync 返回 null（模拟加载失败）。</summary>
    public bool ReturnNull { get; set; }

    /// <summary>设置后，InstantiateAsync 返回该异常（模拟加载异常）。</summary>
    public Exception ThrowOnInstantiate { get; set; }

    /// <summary>最后一次实例化的视图（供用例断言捕获）。</summary>
    public object LastCreated { get; private set; }

    /// <summary>已请求加载的 Prefab 路径列表。</summary>
    public IReadOnlyList<string> InstantiatedPaths => _instantiatedPaths;

    /// <summary>已释放的实例数量。</summary>
    public int ReleasedCount => _released.Count;

    /// <summary>判断指定实例是否已被释放。</summary>
    /// <param name="view">要检查的实例。</param>
    /// <returns>true 表示已释放。</returns>
    public bool WasReleased(IView view)
    {
        return _released.Contains(view);
    }

    /// <inheritdoc/>
    public ValueTask<TView> InstantiateAsync<TView>(
        string prefabPath,
        IViewContainer parent,
        CancellationToken cancellationToken = default)
        where TView : IView
    {
        _instantiatedPaths.Add(prefabPath);

        if (ThrowOnInstantiate != null)
        {
            return new ValueTask<TView>(Task.FromException<TView>(ThrowOnInstantiate));
        }

        if (ReturnNull)
        {
            return new ValueTask<TView>(default(TView));
        }

        var instance = (TView)Activator.CreateInstance(typeof(TView));
        LastCreated = instance;
        return new ValueTask<TView>(instance);
    }

    /// <inheritdoc/>
    public void Release(IView view)
    {
        _released.Add(view);
        TestLifecycleLog.Add("Loader.Release");
    }
}

/// <summary>
/// UI 根节点测试替身：为每个层级返回独立的假容器，不依赖任何 Unity 类型。
/// </summary>
public sealed class FakeUiRoot : IUIRoot
{
    private readonly Dictionary<UILayer, FakeViewContainer> _containers = new();

    /// <inheritdoc/>
    public IViewContainer GetLayerRoot(UILayer layer)
    {
        if (!_containers.TryGetValue(layer, out var container))
        {
            container = new FakeViewContainer();
            _containers[layer] = container;
        }

        return container;
    }
}

/// <summary>
/// 测试用假层级容器。
/// </summary>
public sealed class FakeViewContainer : IViewContainer
{
}

/// <summary>
/// 日志记录器测试替身：记录每次调用的级别、内容与异常，可配置为写入时抛异常。
/// </summary>
public sealed class FakeLogger : ILogger
{
    private readonly List<FakeLogRecord> _records = new();

    /// <summary>写入时是否抛异常（模拟宿主日志实现自身故障）。</summary>
    public bool ThrowOnWrite { get; set; }

    /// <summary>已记录的日志条目。</summary>
    public IReadOnlyList<FakeLogRecord> Records => _records;

    /// <summary>取得指定级别的最后一条记录，没有则返回 null。</summary>
    /// <param name="level">日志级别。</param>
    /// <returns>最后一条记录。</returns>
    public FakeLogRecord LastOf(FakeLogLevel level)
    {
        for (var i = _records.Count - 1; i >= 0; i--)
        {
            if (_records[i].Level == level)
            {
                return _records[i];
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public void LogDebug(string message)
    {
        Write(FakeLogLevel.Debug, message, null);
    }

    /// <inheritdoc/>
    public void LogInfo(string message)
    {
        Write(FakeLogLevel.Info, message, null);
    }

    /// <inheritdoc/>
    public void LogWarning(string message, Exception exception = null)
    {
        Write(FakeLogLevel.Warning, message, exception);
    }

    /// <inheritdoc/>
    public void LogError(string message, Exception exception = null)
    {
        Write(FakeLogLevel.Error, message, exception);
    }

    private void Write(FakeLogLevel level, string message, Exception exception)
    {
        if (ThrowOnWrite)
        {
            throw new InvalidOperationException("FakeLogger 故意抛异常");
        }

        _records.Add(new FakeLogRecord
        {
            Level = level,
            Message = message,
            Exception = exception,
        });
    }
}

/// <summary>测试日志级别。</summary>
public enum FakeLogLevel
{
    /// <summary>调试。</summary>
    Debug,

    /// <summary>信息。</summary>
    Info,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>错误。</summary>
    Error,
}

/// <summary>一条测试日志记录。</summary>
public sealed class FakeLogRecord
{
    /// <summary>日志级别。</summary>
    public FakeLogLevel Level { get; set; }

    /// <summary>日志内容。</summary>
    public string Message { get; set; }

    /// <summary>附带的异常，可为 null。</summary>
    public Exception Exception { get; set; }
}

/// <summary>
/// UIManager 测试上下文：组装被测管理器与全部测试替身，并管理生命周期日志。
/// </summary>
public sealed class UiTestContext
{
    /// <summary>初始化测试上下文。</summary>
    public UiTestContext()
    {
        Loader = new FakeUiResourceLoader();
        Root = new FakeUiRoot();
        Logger = new FakeLogger();
        Manager = new UIManager(Loader, Root, Logger);
        TestLifecycleLog.Events = new List<string>();
    }

    /// <summary>资源加载器替身。</summary>
    public FakeUiResourceLoader Loader { get; }

    /// <summary>日志记录器替身。</summary>
    public FakeLogger Logger { get; }

    /// <summary>UI 根节点替身。</summary>
    public FakeUiRoot Root { get; }

    /// <summary>被测的 UIManager。</summary>
    public UIManager Manager { get; }

    /// <summary>当前用例的生命周期事件序列。</summary>
    public IReadOnlyList<string> LifecycleLog => TestLifecycleLog.Events;

    /// <summary>用例结束清理：结束未完成的受控 HideAsync，并重置生命周期日志。</summary>
    public void TearDown()
    {
        SlowHideView.SetHideCompleted();
        TestLifecycleLog.Events = new List<string>();
    }
}
#pragma warning restore SA1402, SA1649
