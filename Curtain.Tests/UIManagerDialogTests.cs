// <copyright file="UIManagerDialogTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Curtain.Core;

/// <summary>
/// UIManager 对话框测试：结果返回、取消、级联关闭、父句柄校验、参数与句柄注入。
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
        UIHandle parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        UIHandle<int> handle = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();

        Assert.That(handle, Is.Not.Null);
        Assert.That(handle.IsOpen, Is.True);
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(1));
    }

    /// <summary>对话框 Complete 后，GetResult 返回 IsCompleted=true 与返回值。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task Dialog_Complete_ReturnsValue()
    {
        UIHandle parent = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        UIHandle<int> handle = await _context.Manager.OpenDialogAsync<TestDialogView, int>(parent);

        handle.Complete(42);

        UIResult<int> result = await handle.GetResult();
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.Value, Is.EqualTo(42));
    }

    /// <summary>对话框未 Complete 即被关闭，GetResult 返回 IsCompleted=false。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task Dialog_CloseWithoutComplete_ReturnsCancelled()
    {
        UIHandle parent = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        UIHandle<int> handle = await _context.Manager.OpenDialogAsync<TestDialogView, int>(parent);

        await handle.CloseAsync();

        UIResult<int> result = await handle.GetResult();
        Assert.That(result.IsCompleted, Is.False);
        Assert.That(result.Value, Is.EqualTo(default(int)));
    }

    /// <summary>父页面关闭时，子对话框以 ParentClosed 原因级联关闭并返回 IsCompleted=false。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task Dialog_CascadeClosed_WhenParentPageClosed()
    {
        UIHandle parent = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        UIHandle<int> handle = await _context.Manager.OpenDialogAsync<TestDialogView, int>(parent);

        await parent.CloseAsync();

        UIResult<int> result = await handle.GetResult();
        Assert.That(result.IsCompleted, Is.False, "级联关闭的对话框应返回未完成结果");
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(2), "页面与对话框实例都应被释放");

        RecordingView dialogView = (RecordingView)_context.Loader.Created[1];
        Assert.That(dialogView.LastCloseReason, Is.EqualTo(CloseReason.ParentClosed));
    }

    /// <summary>BackToMainAsync 关闭 Stack 页面连同其子对话框。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task Dialog_ClosedByBackToMain()
    {
        UIHandle parent = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        UIHandle<int> handle = await _context.Manager.OpenDialogAsync<TestDialogView, int>(parent);

        await _context.Manager.BackToMainAsync();

        UIResult<int> result = await handle.GetResult();
        Assert.That(result.IsCompleted, Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
    }

    /// <summary>CloseTopAsync 关闭对话框所在的独立页面及其级联子项。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task CloseTop_ClosesParentPageWithDialog()
    {
        UIHandle parent = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        UIHandle<int> handle = await _context.Manager.OpenDialogAsync<TestDialogView, int>(parent);

        Assert.That(await _context.Manager.CloseTopAsync(), Is.True);

        Assert.That((await handle.GetResult()).IsCompleted, Is.False);
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

    /// <summary>父界面已关闭后，用其句柄打开对话框抛出 UIOpenException。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task OpenDialog_ClosedParent_Throws()
    {
        UIHandle parent = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        await parent.CloseAsync();

        Assert.Catch<UIOpenException>(() =>
            _context.Manager
                .OpenDialogAsync<TestDialogView, int>(parent)
                .GetAwaiter()
                .GetResult());
    }

    /// <summary>对话框参数通过 SetParameter 注入到 View。</summary>
    [Test]
    public void Dialog_ParameterInjected()
    {
        UIHandle parent = _context.Manager
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

        TestParamDialogView view = (TestParamDialogView)_context.Loader.LastCreated;
        Assert.That(view, Is.Not.Null);
        Assert.That(view.ReceivedParam, Is.Not.Null);
        Assert.That(view.ReceivedParam.Value, Is.EqualTo(7));
    }

    /// <summary>对话框返回结果句柄被注入到实现 IViewWithResult 的 View。</summary>
    [Test]
    public void Dialog_ResultHandleInjected()
    {
        UIHandle parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        UIHandle<int> handle = _context.Manager
            .OpenDialogAsync<TestParamDialogView, TestParam, int>(new TestParam(), parent)
            .GetAwaiter()
            .GetResult();

        TestParamDialogView view = (TestParamDialogView)_context.Loader.LastCreated;
        Assert.That(view, Is.Not.Null);
        Assert.That(view.ResultHandle, Is.Not.Null);
        Assert.That(view.ResultHandle, Is.SameAs(handle));
    }

    /// <summary>嵌套对话框随父对话框逐级级联关闭，全部返回 IsCompleted=false。</summary>
    [Test]
    public void NestedDialogs_CascadeClosed()
    {
        UIHandle parent = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        UIHandle<int> outer = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(parent)
            .GetAwaiter()
            .GetResult();
        UIHandle<int> inner = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(outer)
            .GetAwaiter()
            .GetResult();

        _context.Manager.CloseAsync(parent).GetAwaiter().GetResult();

        Assert.That(outer.GetResult().GetAwaiter().GetResult().IsCompleted, Is.False);
        Assert.That(inner.GetResult().GetAwaiter().GetResult().IsCompleted, Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(3));
    }

    /// <summary>对话框 View 通过注入的句柄 Complete 返回值（从 View 侧驱动）。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task Dialog_ViewDrivenComplete_ReturnsValue()
    {
        UIHandle parent = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        UIHandle<int> handle = await _context.Manager.OpenDialogAsync<TestDialogView, int>(parent);

        TestDialogView view = (TestDialogView)_context.Loader.LastCreated;
        view.Confirm(99);

        UIResult<int> result = await handle.GetResult();
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.Value, Is.EqualTo(99));
    }

    /// <summary>对话框挂到 Main 句柄上，BackToMainAsync 也会关闭它。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task Dialog_ParentedToMain_ClosedByBackToMain()
    {
        UIHandle main = await _context.Manager.OpenMainAsync<TestMainView>();
        UIHandle<int> handle = await _context.Manager.OpenDialogAsync<TestDialogView, int>(main);

        await _context.Manager.BackToMainAsync();

        Assert.That((await handle.GetResult()).IsCompleted, Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
        Assert.That(main.IsOpen, Is.True, "BackToMain 不应关闭 Main");
    }
}
