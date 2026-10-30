// <copyright file="UIManagerTestViews.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

#pragma warning disable SA1402, SA1649 // 测试 View 集中放置，允许单文件多类型

namespace Curtain.Tests;

using System;
using System.Threading.Tasks;
using Curtain.Abstraction;
using Curtain.Core;

/// <summary>
/// 记录生命周期事件的 View 测试基类（纯 C# 实现 <see cref="IView"/>，无 Unity 依赖）。
/// 各具体测试 View 仅通过 [UI] 标注区分 Prefab 路径与类型。
/// </summary>
public abstract class RecordingView : IView
{
    /// <inheritdoc/>
    public void SetActive(bool active)
    {
        TestLifecycleLog.Add(active ? "View.SetActive(true)" : "View.SetActive(false)");
    }

    /// <inheritdoc/>
    void IView.Initialize()
    {
        TestLifecycleLog.Add("View.Initialize");
        OnInitialize();
    }

    /// <inheritdoc/>
    public virtual async ValueTask HideAsync()
    {
        TestLifecycleLog.Add("View.Hide");
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
/// HideAsync 完成时机由测试控制的 View 测试替身：用于断言「出场动画完成后才释放实例」。
/// </summary>
[UI("Test/SlowHide")]
public sealed class SlowHideView : RecordingView
{
    private static TaskCompletionSource<bool> _hideGate;

    /// <summary>开始一次受控的 HideAsync：在调用 <see cref="SetHideCompleted"/> 前不会完成。</summary>
    public static void BeginControlledHide()
    {
        _hideGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>结束当前受控的 HideAsync。</summary>
    public static void SetHideCompleted()
    {
        _hideGate?.TrySetResult(true);
    }

    /// <inheritdoc/>
    public override async ValueTask HideAsync()
    {
        TestLifecycleLog.Add("View.HideAsync(start)");
        await _hideGate.Task;
        TestLifecycleLog.Add("View.HideAsync(end)");
    }
}

/// <summary>
/// HideAsync 抛异常的 View 测试替身：用于断言关闭流程仍会释放实例。
/// </summary>
[UI("Test/ThrowingHide")]
public sealed class ThrowingHideView : RecordingView
{
    /// <inheritdoc/>
    public override ValueTask HideAsync()
    {
        throw new InvalidOperationException("View.HideAsync failed");
    }
}

/// <summary>
/// 缺少 [UI] 标注的 View 测试替身（用于失败路径测试）。
/// </summary>
public sealed class NoAttributeView : RecordingView
{
}
#pragma warning restore SA1402, SA1649
