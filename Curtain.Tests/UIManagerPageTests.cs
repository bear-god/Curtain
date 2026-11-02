// <copyright file="UIManagerPageTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Curtain.Abstraction;
using Curtain.Core;

/// <summary>
/// UIManager 页面管理测试：打开/关闭、层级校验、Main 策略、激活、队列顺序与生命周期。
/// </summary>
[TestFixture]
public class UIManagerPageTests
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

    /// <summary>打开 Main 层页面后注册成功，HasMain、CurrentMain 与计数一致。</summary>
    [Test]
    public void OpenMain_RegistersPage()
    {
        UIHandle handle = _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();

        Assert.That(handle, Is.Not.Null);
        Assert.That(handle.IsOpen, Is.True);
        Assert.That(_context.Manager.HasMain, Is.True);
        Assert.That(_context.Manager.CurrentMain, Is.SameAs(handle));
        Assert.That(_context.Manager.GetOpenCount<TestMainView>(), Is.EqualTo(1));
    }

    /// <summary>已存在 Main 时默认 Reject，抛出 UIOpenException 且不改变现状。</summary>
    [Test]
    public void OpenMain_Reject_ThrowsWhenMainExists()
    {
        _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();

        Assert.Catch<UIOpenException>(() =>
            _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult());

        Assert.That(_context.Manager.GetOpenCount<TestMainView>(), Is.EqualTo(1));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(0), "Reject 不应关闭旧 Main");
    }

    /// <summary>Replace 模式先打开新 Main，成功后关闭旧 Main。</summary>
    [Test]
    public void OpenMain_Replace_ClosesPreviousMain()
    {
        UIHandle first = _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();
        UIHandle second = _context.Manager
            .OpenMainAsync<TestMainView>(MainOpenMode.Replace)
            .GetAwaiter()
            .GetResult();

        Assert.That(_context.Manager.GetOpenCount<TestMainView>(), Is.EqualTo(1));
        Assert.That(_context.Manager.CurrentMain, Is.SameAs(second));
        Assert.That(first.IsOpen, Is.False, "旧 Main 应被关闭");
        Assert.That(second.IsOpen, Is.True);
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1), "旧的 Main 页面应被释放");
    }

    /// <summary>Stack 与 Top 层页面均可正常打开并分别计数。</summary>
    [Test]
    public void OpenPage_StackAndTopLayers_OpenIndependently()
    {
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();
        _context.Manager.OpenPageAsync<TestTopView>(UILayer.Top).GetAwaiter().GetResult();

        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(1));
        Assert.That(_context.Manager.GetOpenCount<TestTopView>(), Is.EqualTo(1));
        Assert.That(_context.Manager.GetOpenCount(UILayer.Stack), Is.EqualTo(1));
        Assert.That(_context.Manager.GetOpenCount(UILayer.Top), Is.EqualTo(1));
        Assert.That(_context.Loader.InstantiatedPaths, Does.Contain("Test/Page"));
        Assert.That(_context.Loader.InstantiatedPaths, Does.Contain("Test/Top"));
    }

    /// <summary>OpenPageAsync 拒绝 Main 与 Effect 层级。</summary>
    [Test]
    public void OpenPage_RejectsMainAndEffectLayers()
    {
        Assert.Catch<ArgumentException>(() =>
            _context.Manager.OpenPageAsync<TestPageView>(UILayer.Main).GetAwaiter().GetResult());
        Assert.Catch<ArgumentException>(() =>
            _context.Manager.OpenPageAsync<TestPageView>(UILayer.Effect).GetAwaiter().GetResult());
    }

    /// <summary>Top 返回 Stack 层最上方句柄，CurrentMain 与 HasMain 反映 Main 状态。</summary>
    [Test]
    public void TopAndCurrentMain_ExposeHandles()
    {
        UIHandle main = _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();
        UIHandle page = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        UIHandle dialog = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(page)
            .GetAwaiter()
            .GetResult();

        Assert.That(_context.Manager.Top, Is.SameAs(dialog));
        Assert.That(_context.Manager.CurrentMain, Is.SameAs(main));
        Assert.That(_context.Manager.HasMain, Is.True);
    }

    /// <summary>独占页面隐藏 Main 与下方独占项；最新的独占项可见。</summary>
    [Test]
    public void ExclusivePage_HidesMainAndLowerStack()
    {
        _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();

        RecordingView mainView = (RecordingView)_context.Loader.Created[0];
        RecordingView firstPage = (RecordingView)_context.Loader.Created[1];
        RecordingView secondPage = (RecordingView)_context.Loader.Created[2];

        Assert.That(mainView.IsActive, Is.False, "独占页面应隐藏 Main");
        Assert.That(firstPage.IsActive, Is.False, "上方的独占页面应隐藏下方独占项");
        Assert.That(secondPage.IsActive, Is.True, "最新的独占项应可见");
    }

    /// <summary>叠加页面不隐藏下方视图；无独占时 Main 可见。</summary>
    [Test]
    public void OverlayPage_KeepsLowerViewsVisible()
    {
        _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();
        _context.Manager
            .OpenPageAsync<TestOverlayPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        _context.Manager
            .OpenPageAsync<TestOverlayPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();

        RecordingView mainView = (RecordingView)_context.Loader.Created[0];
        RecordingView firstOverlay = (RecordingView)_context.Loader.Created[1];
        RecordingView secondOverlay = (RecordingView)_context.Loader.Created[2];

        Assert.That(mainView.IsActive, Is.True);
        Assert.That(firstOverlay.IsActive, Is.True);
        Assert.That(secondOverlay.IsActive, Is.True);
    }

    /// <summary>关闭独占页面后立刻恢复下层激活，但关闭中的视图在释放前保持激活。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task ClosingTop_RestoresLowerActivationBeforeRelease()
    {
        UIHandle main = await _context.Manager.OpenMainAsync<TestMainView>();
        ControlledCloseView.BeginControlledClose();
        UIHandle page = await _context.Manager.OpenPageAsync<ControlledCloseView>(UILayer.Stack);

        RecordingView mainView = (RecordingView)_context.Loader.Created[0];
        ControlledCloseView pageView = (ControlledCloseView)_context.Loader.Created[1];
        Assert.That(mainView.IsActive, Is.False);

        ValueTask close = _context.Manager.CloseAsync(page);

        Assert.That(mainView.IsActive, Is.True, "摘除后应立即恢复下层激活");
        Assert.That(pageView.IsActive, Is.True, "关闭中的视图保持激活直到释放");
        Assert.That(page.IsOpen, Is.False, "摘除后句柄不再是打开状态");
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(0), "关闭钩子未完成前不得释放");

        ControlledCloseView.CompleteControlledClose();
        await close;

        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1));
        Assert.That(_context.Manager.GetOpenCount<ControlledCloseView>(), Is.EqualTo(0));
        Assert.That(main.IsOpen, Is.True);
    }

    /// <summary>CloseTopAsync 关闭最上方的独立页面，保留下方页面。</summary>
    [Test]
    public void CloseTop_ClosesTopIndependentPage()
    {
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();

        Assert.That(_context.Manager.CloseTopAsync().GetAwaiter().GetResult(), Is.True);

        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(1));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1));
    }

    /// <summary>Stack 层为空时 CloseTopAsync 返回 false。</summary>
    [Test]
    public void CloseTop_EmptyStack_ReturnsFalse()
    {
        Assert.That(_context.Manager.CloseTopAsync().GetAwaiter().GetResult(), Is.False);
    }

    /// <summary>BackToMainAsync 关闭所有 Stack 页面，保留 Main 与 Top。</summary>
    [Test]
    public void BackToMain_ClosesStackPages_KeepsMainAndTop()
    {
        _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();
        _context.Manager.OpenPageAsync<TestTopView>(UILayer.Top).GetAwaiter().GetResult();

        _context.Manager.BackToMainAsync().GetAwaiter().GetResult();

        Assert.That(_context.Manager.GetOpenCount<TestMainView>(), Is.EqualTo(1), "Main 不应被关闭");
        Assert.That(_context.Manager.GetOpenCount<TestTopView>(), Is.EqualTo(1), "Top 不应被关闭");
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(2));
    }

    /// <summary>BackToMainAsync 自顶向下关闭页面及其对话框，兄弟对话框按新到旧。</summary>
    [Test]
    public void BackToMain_ClosesDialogsWithPages()
    {
        UIHandle page = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        UIHandle<int> first = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(page)
            .GetAwaiter()
            .GetResult();
        UIHandle<int> second = _context.Manager
            .OpenDialogAsync<TestDialogView, int>(page)
            .GetAwaiter()
            .GetResult();

        _context.Manager.BackToMainAsync().GetAwaiter().GetResult();

        Assert.That(first.GetResult().GetAwaiter().GetResult().IsCompleted, Is.False);
        Assert.That(second.GetResult().GetAwaiter().GetResult().IsCompleted, Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestDialogView>(), Is.EqualTo(0));
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(3));
        Assert.That(
            IndexOfLifecycleEvent("Loader.Release:TestDialogView"),
            Is.LessThan(IndexOfLifecycleEvent("Loader.Release:TestPageView")),
            "对话框应先于所属页面释放");
    }

    /// <summary>通过句柄 Dispose 关闭页面后，实例被释放且计数归零。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task HandleDispose_ClosesPage()
    {
        UIHandle handle = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);

        handle.Dispose();

        Assert.That(await WaitUntilAsync(() => _context.Loader.ReleasedCount == 1), Is.True);
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(handle.IsOpen, Is.False);
    }

    /// <summary>句柄重复 Dispose 是幂等的，实例只释放一次。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task HandleDispose_Idempotent()
    {
        UIHandle handle = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);

        handle.Dispose();
        handle.Dispose();

        Assert.That(await WaitUntilAsync(() => _context.Loader.ReleasedCount == 1), Is.True);
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1));
    }

    /// <summary>CloseAsync 必须等 BeforeCloseAsync 完成后才释放实例。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task HandleCloseAsync_WaitsBeforeCloseBeforeRelease()
    {
        ControlledCloseView.BeginControlledClose();
        UIHandle handle = await _context.Manager.OpenPageAsync<ControlledCloseView>(UILayer.Stack);

        ValueTask close = handle.CloseAsync();

        Assert.That(_context.LifecycleLog, Does.Contain("View.BeforeCloseAsync(start)"));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(0), "关闭钩子未完成前不得释放实例");

        ControlledCloseView.CompleteControlledClose();
        await close;

        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1));
        Assert.That(
            IndexOfLifecycleEvent("View.BeforeCloseAsync(end)"),
            Is.LessThan(IndexOfLifecycleEvent("Loader.Release:ControlledCloseView")),
            "释放必须发生在关闭钩子完成之后");
    }

    /// <summary>取消令牌在摘除前生效：关闭被取消时页面保持打开。</summary>
    [Test]
    public void HandleCloseAsync_CancelledBeforeDetach_KeepsOpen()
    {
        UIHandle handle = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();

        using CancellationTokenSource source = new CancellationTokenSource();
        source.Cancel();

        Assert.Catch<OperationCanceledException>(() =>
            handle.CloseAsync(source.Token).GetAwaiter().GetResult());

        Assert.That(handle.IsOpen, Is.True);
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(1));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(0));
    }

    /// <summary>关闭钩子抛异常时不阻断关闭流程，实例仍被释放。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task CloseHooksThrow_StillReleases()
    {
        UIHandle handle = await _context.Manager.OpenPageAsync<ThrowingCloseView>(UILayer.Stack);

        Assert.DoesNotThrow(() => handle.Dispose());

        Assert.That(await WaitUntilAsync(() => _context.Loader.ReleasedCount == 1), Is.True);
        Assert.That(_context.Manager.GetOpenCount<ThrowingCloseView>(), Is.EqualTo(0));
    }

    /// <summary>主动关闭时 BeforeClose 收到 Normal 原因。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task CloseHooks_ReceiveNormalReason()
    {
        UIHandle handle = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);

        await handle.CloseAsync();

        RecordingView view = (RecordingView)_context.Loader.Created[0];
        Assert.That(view.LastCloseReason, Is.EqualTo(CloseReason.Normal));
        Assert.That(_context.LifecycleLog, Does.Contain("View.BeforeClose(Normal)"));
    }

    /// <summary>成功打开页面时输出 Info 级别日志。</summary>
    [Test]
    public void OpenPage_LogsInfoOnSuccess()
    {
        _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();

        FakeLogRecord record = _context.Logger.LastOf(FakeLogLevel.Info);
        Assert.That(record, Is.Not.Null, "成功打开应输出 Info 日志");
        Assert.That(record.Message, Does.Contain("UI opened"));
    }

    /// <summary>日志实现自身抛异常时不影响打开与关闭流程。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task LoggerThrows_DoesNotBreakOpenAndClose()
    {
        _context.Logger.ThrowOnWrite = true;

        UIHandle handle = await _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);

        Assert.DoesNotThrow(() => handle.Dispose());
        Assert.That(await WaitUntilAsync(() => _context.Loader.ReleasedCount == 1), Is.True);
    }

    /// <summary>同一类型页面允许多开，计数随打开/关闭增减。</summary>
    [Test]
    public void MultiOpen_SameType_CountsInstances()
    {
        UIHandle first = _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();
        UIHandle second = _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();

        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(2));

        _context.Manager.CloseAsync(first).GetAwaiter().GetResult();

        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(1));
        Assert.That(second.IsOpen, Is.True);
    }

    /// <summary>未打开任何页面时计数为 0。</summary>
    [Test]
    public void IsOpen_ReturnsFalseWhenNothingOpen()
    {
        Assert.That(_context.Manager.IsOpen<TestPageView>(), Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
    }

    /// <summary>并发调用（不等待）的命令按调用顺序执行。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task Queue_ProcessesCommandsInCallOrder()
    {
        ValueTask<UIHandle> first = _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        ValueTask<UIHandle> second = _context.Manager.OpenPageAsync<TestPageView>(UILayer.Stack);
        ValueTask close = _context.Manager.BackToMainAsync();

        await first;
        await second;
        await close;

        Assert.That(_context.Loader.Created.Count, Is.EqualTo(2));
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(2));
    }

    /// <summary>播放特效不抛异常（当前为 TODO 桩实现）。</summary>
    [Test]
    public void PlayEffect_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => _context.Manager.PlayEffect(default(TestEffect)));
    }

    /// <summary>轮询等待条件成立（用于断言异步收尾完成）；超时返回 false。</summary>
    /// <param name="condition">条件。</param>
    /// <param name="timeoutMs">超时毫秒数。</param>
    /// <returns>条件在超时前成立返回 true。</returns>
    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMs)
            {
                return false;
            }

            await Task.Delay(10);
        }

        return true;
    }

    /// <summary>取生命周期事件首次出现的下标；不存在返回 -1。</summary>
    /// <param name="evt">事件名。</param>
    /// <returns>事件下标。</returns>
    private int IndexOfLifecycleEvent(string evt)
    {
        for (int i = 0; i < _context.LifecycleLog.Count; i++)
        {
            if (_context.LifecycleLog[i] == evt)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>测试用特效类型。</summary>
    private readonly struct TestEffect : IUIEffect
    {
    }
}
