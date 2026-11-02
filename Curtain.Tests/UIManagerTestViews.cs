// <copyright file="UIManagerTestViews.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

#pragma warning disable SA1402, SA1649 // 测试 View 集中放置，允许单文件多类型

namespace Curtain.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Curtain.Abstraction;
using Curtain.Core;

/// <summary>
/// 记录生命周期事件的 View 测试基类（纯 C# 实现 <see cref="IView"/>，无引擎依赖）。
/// 各具体测试 View 仅通过 [UI] 标注区分 Prefab 路径与类型。
/// </summary>
public abstract class RecordingView : IView
{
    /// <inheritdoc/>
    public virtual UIOpenMode Mode { get; set; } = UIOpenMode.Exclusive;

    /// <summary>当前是否处于激活状态。</summary>
    public bool IsActive { get; private set; }

    /// <summary>最后一次关闭原因。</summary>
    public CloseReason LastCloseReason { get; private set; }

    /// <inheritdoc/>
    public void SetActive(bool active)
    {
        IsActive = active;
        TestLifecycleLog.Add(active ? "View.SetActive(true)" : "View.SetActive(false)");
    }

    /// <inheritdoc/>
    void IView.Initialize()
    {
        TestLifecycleLog.Add("View.Initialize");
        OnInitialize();
    }

    /// <inheritdoc/>
    public virtual void BeforeClose(CloseReason reason)
    {
        LastCloseReason = reason;
        TestLifecycleLog.Add("View.BeforeClose(" + reason + ")");
    }

    /// <inheritdoc/>
    public virtual ValueTask BeforeCloseAsync(CloseReason reason, CancellationToken cancellationToken = default)
    {
        TestLifecycleLog.Add("View.BeforeCloseAsync");
        return default;
    }

    /// <summary>子类钩子：初始化时调用。</summary>
    protected virtual void OnInitialize()
    {
    }
}

/// <summary>
/// 主界面 View 测试替身。
/// </summary>
[UI("Test/Main")]
public sealed class TestMainView : RecordingView
{
}

/// <summary>
/// 堆叠层页面 View 测试替身。
/// </summary>
[UI("Test/Page")]
public sealed class TestPageView : RecordingView
{
}

/// <summary>
/// 叠加模式的堆叠层页面测试替身。
/// </summary>
[UI("Test/OverlayPage")]
public sealed class TestOverlayPageView : RecordingView
{
    /// <summary>初始化叠加模式。</summary>
    public TestOverlayPageView()
    {
        Mode = UIOpenMode.Overlay;
    }
}

/// <summary>
/// 顶层页面 View 测试替身。
/// </summary>
[UI("Test/Top")]
public sealed class TestTopView : RecordingView
{
}

/// <summary>
/// 对话框 View 测试替身：接收结果句柄并提供确认/取消操作。
/// </summary>
[UI("Test/Dialog")]
public sealed class TestDialogView : RecordingView, IViewWithResult
{
    /// <summary>框架注入的结果句柄。</summary>
    public UIHandle ResultHandle { get; private set; }

    /// <inheritdoc/>
    void IViewWithResult.SetResultHandle(UIHandle handle)
    {
        ResultHandle = handle;
        TestLifecycleLog.Add("View.SetResultHandle");
    }

    /// <summary>模拟用户确认，返回指定值。</summary>
    /// <param name="value">对话框返回值。</param>
    public void Confirm(int value)
    {
        ((UIHandle<int>)ResultHandle).Complete(value);
    }

    /// <summary>模拟用户取消（通过公开的 Dispose 关闭对话框）。</summary>
    public void Cancel()
    {
        ResultHandle.Dispose();
    }
}

/// <summary>
/// 带参数与返回值的对话框 View 测试替身。
/// </summary>
[UI("Test/ParamDialog")]
public sealed class TestParamDialogView : RecordingView, IViewWithParameter<TestParam>, IViewWithResult
{
    /// <summary>框架注入的参数。</summary>
    public TestParam ReceivedParam { get; private set; }

    /// <summary>框架注入的结果句柄。</summary>
    public UIHandle ResultHandle { get; private set; }

    /// <inheritdoc/>
    void IViewWithParameter<TestParam>.SetParameter(TestParam parameter)
    {
        ReceivedParam = parameter;
        TestLifecycleLog.Add("View.SetParameter");
    }

    /// <inheritdoc/>
    void IViewWithResult.SetResultHandle(UIHandle handle)
    {
        ResultHandle = handle;
        TestLifecycleLog.Add("View.SetResultHandle");
    }

    /// <summary>模拟用户确认，返回指定值。</summary>
    /// <param name="value">对话框返回值。</param>
    public void Confirm(int value)
    {
        ((UIHandle<int>)ResultHandle).Complete(value);
    }
}

/// <summary>
/// 初始化抛异常的 View 测试替身（用于失败路径测试）。
/// </summary>
[UI("Test/Throwing")]
public sealed class ThrowingView : RecordingView
{
    /// <inheritdoc/>
    protected override void OnInitialize()
    {
        throw new InvalidOperationException("View.Initialize failed");
    }
}

/// <summary>
/// 异步关闭完成时机由测试控制的 View 测试替身：用于断言关闭钩子完成后才释放实例。
/// </summary>
[UI("Test/ControlledClose")]
public sealed class ControlledCloseView : RecordingView
{
    private static TaskCompletionSource<bool> _closeGate;

    /// <summary>开始一次受控的 BeforeCloseAsync：在调用 <see cref="CompleteControlledClose"/> 前不会完成。</summary>
    public static void BeginControlledClose()
    {
        _closeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>结束当前受控的 BeforeCloseAsync。</summary>
    public static void CompleteControlledClose()
    {
        _closeGate?.TrySetResult(true);
    }

    /// <inheritdoc/>
    public override async ValueTask BeforeCloseAsync(
        CloseReason reason,
        CancellationToken cancellationToken = default)
    {
        TestLifecycleLog.Add("View.BeforeCloseAsync(start)");
        await _closeGate.Task;
        TestLifecycleLog.Add("View.BeforeCloseAsync(end)");
    }
}

/// <summary>
/// 关闭钩子抛异常的 View 测试替身：用于断言关闭流程仍会释放实例。
/// </summary>
[UI("Test/ThrowingClose")]
public sealed class ThrowingCloseView : RecordingView
{
    /// <inheritdoc/>
    public override void BeforeClose(CloseReason reason)
    {
        base.BeforeClose(reason);
        throw new InvalidOperationException("View.BeforeClose failed");
    }

    /// <inheritdoc/>
    public override ValueTask BeforeCloseAsync(
        CloseReason reason,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("View.BeforeCloseAsync failed");
    }
}

/// <summary>
/// 缺少 [UI] 标注的 View 测试替身（用于失败路径测试）。
/// </summary>
public sealed class NoAttributeView : RecordingView
{
}
#pragma warning restore SA1402, SA1649
