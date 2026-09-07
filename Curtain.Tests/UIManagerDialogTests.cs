// <copyright file="UIManagerDialogTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using NUnit.Framework;
using Curtain.Core;

/// <summary>
/// UIManager 对话框测试：结果返回、取消、级联关闭、父句柄校验、参数与句柄注入。
/// 所有 OpenXxxAsync 在测试中同步完成，统一通过 GetAwaiter().GetResult() 同步取结果。
/// </summary>
[TestFixture]
public class UIManagerDialogTests
{
    private UiTestContext _context;

    /// <summary>每个测试执行前的初始化。</summary>
    [SetUp]
    public void SetUp()
    {
        _context = new UiTestContext();
    }

    /// <summary>每个测试执行后的清理。</summary>
    [TearDown]
    public void TearDown()
    {
        _context.TearDown();
    }

    /// <summary>打开对话框返回带结果的句柄，且对话框已注册。</summary>
    [Test]
    public void OpenDialog_ReturnsTypedHandle()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var handle = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();

        Assert.That(handle, Is.Not.Null);
        Assert.That(_context.Manager.IsOpen<TestDialogView>(), Is.True);
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(1));
    }

    /// <summary>对话框 Complete 后，GetResult 返回 IsCompleted=true 与返回值。</summary>
    [Test]
    public void Dialog_Complete_ReturnsValue()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var handle = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();

        handle.Complete(42);

        var result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.Value, Is.EqualTo(42));
    }

    /// <summary>对话框未 Complete 即被关闭，GetResult 返回 IsCompleted=false。</summary>
    [Test]
    public void Dialog_DisposeWithoutComplete_ReturnsCancelled()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var handle = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();

        handle.Dispose();

        var result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.False);
        Assert.That(result.Value, Is.EqualTo(default(int)));
    }

    /// <summary>父页面关闭时，子对话框被级联关闭并返回 IsCompleted=false。</summary>
    [Test]
    public void Dialog_CascadeClosed_WhenParentPageDisposed()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var handle = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();

        parent.Dispose();

        var result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.False, "级联关闭的对话框应返回未完成结果");
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(2), "页面与对话框实例都应被释放");
    }

    /// <summary>CloseAll 时 Stack 页面连同其子对话框一起关闭。</summary>
    [Test]
    public void Dialog_ClosedByCloseAll()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var handle = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();

        _context.Manager.CloseAll();

        var result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
    }

    /// <summary>父句柄为 null 时打开对话框抛出 ArgumentNullException。</summary>
    [Test]
    public void OpenDialog_NullParent_Throws()
    {
        Assert.Catch<ArgumentNullException>(() =>
            _context.Manager
                .OpenDialogAsync<TestDialogView, int>(null)
                .GetAwaiter()
                .GetResult());
    }

    /// <summary>父界面已关闭后，用其句柄打开对话框抛出 InvalidOperationException。</summary>
    [Test]
    public void OpenDialog_DisposedParent_Throws()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        parent.Dispose();

        Assert.Catch<InvalidOperationException>(() =>
            _context.Manager
                .OpenDialogAsync<TestDialogView, int>(parent)
                .GetAwaiter()
                .GetResult());
    }

    /// <summary>对话框参数通过 SetParameter 注入到 View。</summary>
    [Test]
    public void Dialog_ParameterInjected()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        _context.Manager
            .OpenDialogAsync<TestParamDialogView, TestParam, int>(
                new TestParam
                {
                    Value = 7,
                },
                parent)
            .GetAwaiter()
            .GetResult();

        var view = (TestParamDialogView)_context.Loader.LastCreated;
        Assert.That(view, Is.Not.Null);
        Assert.That(view.ReceivedParam, Is.Not.Null);
        Assert.That(view.ReceivedParam.Value, Is.EqualTo(7));
    }

    /// <summary>对话框返回结果句柄被注入到实现 IViewWithResult 的 View。</summary>
    [Test]
    public void Dialog_ResultHandleInjected()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var handle = _context.Manager
            .OpenDialogAsync<TestParamDialogView, TestParam, int>(new TestParam(), parent)
            .GetAwaiter()
            .GetResult();

        var view = (TestParamDialogView)_context.Loader.LastCreated;
        Assert.That(view, Is.Not.Null);
        Assert.That(view.ResultHandle, Is.Not.Null);
        Assert.That(view.ResultHandle, Is.SameAs(handle));
    }

    /// <summary>嵌套对话框随父对话框逐级级联关闭，全部返回 IsCompleted=false。</summary>
    [Test]
    public void NestedDialogs_CascadeClosed()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var outer = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();
        var inner = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(outer)
            .GetAwaiter()
            .GetResult();

        parent.Dispose();

        Assert.That(outer.GetResult().GetAwaiter().GetResult().IsCompleted, Is.False);
        Assert.That(inner.GetResult().GetAwaiter().GetResult().IsCompleted, Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(3));
    }

    /// <summary>对话框 View 通过注入的句柄 Complete 返回值（从 View 侧驱动）。</summary>
    [Test]
    public void Dialog_ViewDrivenComplete_ReturnsValue()
    {
        var parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var handle = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();

        var view = (TestDialogView)_context.Loader.LastCreated;
        view.Confirm(99);

        var result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.Value, Is.EqualTo(99));
    }
}
