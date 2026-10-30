// <copyright file="UIManagerPageTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using Curtain.Abstraction;
using Curtain.Core;

/// <summary>
/// UIManager 页面管理测试：打开/关闭、层级校验、Main 替换、多开计数、CloseAll、特效。
/// 所有 OpenXxxAsync 在测试中同步完成，统一通过 GetAwaiter().GetResult() 同步取结果。
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

    /// <summary>打开 Main 层页面后，IsOpen 返回 true 且计数为 1。</summary>
    [Test]
    public void OpenMain_RegistersPage()
    {
        var handle = _context.Manager
            .OpenMainAsync<TestMainView>()
            .GetAwaiter()
            .GetResult();

        Assert.That(handle, Is.Not.Null);
        Assert.That(_context.Manager.IsOpen<TestMainView>(), Is.True);
        Assert.That(_context.Manager.GetOpenCount<TestMainView>(), Is.EqualTo(1));
    }

    /// <summary>再次打开 Main 页面时，旧的 Main 页面被自动关闭并释放。</summary>
    [Test]
    public void OpenMain_ReplacesPreviousMain()
    {
        _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();
        _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();

        Assert.That(_context.Manager.GetOpenCount<TestMainView>(), Is.EqualTo(1));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1), "旧的 Main 页面应被关闭释放");
    }

    /// <summary>Stack 与 Top 层页面均可正常打开并分别计数。</summary>
    [Test]
    public void OpenPage_StackAndTopLayers_OpenIndependently()
    {
        _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        _context.Manager
            .OpenPageAsync<TestTopView>(UILayer.Top)
            .GetAwaiter()
            .GetResult();

        Assert.That(_context.Manager.IsOpen<TestPageView>(), Is.True);
        Assert.That(_context.Manager.IsOpen<TestTopView>(), Is.True);
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(1));
        Assert.That(_context.Manager.GetOpenCount<TestTopView>(), Is.EqualTo(1));
        Assert.That(_context.Loader.InstantiatedPaths, Does.Contain("Test/Page"));
        Assert.That(_context.Loader.InstantiatedPaths, Does.Contain("Test/Top"));
    }

    /// <summary>OpenPageAsync 拒绝 Main 与 Effect 层级。</summary>
    [Test]
    public void OpenPage_RejectsMainAndEffectLayers()
    {
        Assert.Catch<ArgumentException>(() =>
            _context.Manager
                .OpenPageAsync<TestPageView>(UILayer.Main)
                .GetAwaiter()
                .GetResult());
        Assert.Catch<ArgumentException>(() =>
            _context.Manager
                .OpenPageAsync<TestPageView>(UILayer.Effect)
                .GetAwaiter()
                .GetResult());
    }

    /// <summary>CloseAll 关闭 Stack 层页面，保留 Main 与 Top 层页面。</summary>
    [Test]
    public void CloseAll_ClosesStackPages_KeepsMainAndTop()
    {
        _context.Manager.OpenMainAsync<TestMainView>().GetAwaiter().GetResult();
        _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        _context.Manager
            .OpenPageAsync<TestTopView>(UILayer.Top)
            .GetAwaiter()
            .GetResult();

        _context.Manager.CloseAll();

        Assert.That(_context.Manager.GetOpenCount<TestMainView>(), Is.EqualTo(1), "Main 层不应被 CloseAll 关闭");
        Assert.That(_context.Manager.GetOpenCount<TestTopView>(), Is.EqualTo(1), "Top 层不应被 CloseAll 关闭");
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0), "Stack 层页面应被 CloseAll 关闭");
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(2), "两个 Stack 页面应被释放");
    }

    /// <summary>通过句柄 Dispose 关闭页面后，实例被释放且计数归零。</summary>
    [Test]
    public void HandleDispose_ClosesPage()
    {
        var handle = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();

        handle.Dispose();

        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
        Assert.That(_context.Manager.IsOpen<TestPageView>(), Is.False);
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1));
    }

    /// <summary>句柄重复 Dispose 是幂等的，实例只释放一次。</summary>
    [Test]
    public void HandleDispose_Idempotent()
    {
        var handle = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();

        handle.Dispose();
        handle.Dispose();

        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1));
    }

    /// <summary>句柄 Dispose 关闭页面时，必须等 HideAsync 完成后才释放实例。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task HandleDispose_WaitsHideBeforeRelease()
    {
        SlowHideView.BeginControlledHide();
        var handle = await _context.Manager
            .OpenPageAsync<SlowHideView>(UILayer.Stack);

        handle.Dispose();

        Assert.That(_context.LifecycleLog, Does.Contain("View.HideAsync(start)"));
        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(0), "出场动画未完成前不得释放实例");

        SlowHideView.SetHideCompleted();

        Assert.That(
            await WaitUntilAsync(() => _context.Loader.ReleasedCount == 1),
            Is.True,
            "出场动画完成后应释放实例");
        Assert.That(
            IndexOfLifecycleEvent("View.HideAsync(end)"),
            Is.LessThan(IndexOfLifecycleEvent("Loader.Release")),
            "释放必须发生在 HideAsync 完成之后");
        Assert.That(_context.Manager.GetOpenCount<SlowHideView>(), Is.EqualTo(0));
    }

    /// <summary>CloseAll 关闭页面时同样等待 HideAsync 完成后才释放实例。</summary>
    /// <returns>表示异步测试的 Task。</returns>
    [Test]
    public async Task CloseAll_WaitsHideBeforeRelease()
    {
        SlowHideView.BeginControlledHide();
        await _context.Manager
            .OpenPageAsync<SlowHideView>(UILayer.Stack);

        _context.Manager.CloseAll();

        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(0), "出场动画未完成前不得释放实例");

        SlowHideView.SetHideCompleted();

        Assert.That(
            await WaitUntilAsync(() => _context.Loader.ReleasedCount == 1),
            Is.True,
            "出场动画完成后应释放实例");
    }

    /// <summary>HideAsync 抛异常时不阻断关闭流程，实例仍被释放。</summary>
    [Test]
    public void HandleDispose_HideThrows_StillReleases()
    {
        var handle = _context.Manager
            .OpenPageAsync<ThrowingHideView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();

        Assert.DoesNotThrow(handle.Dispose);

        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1), "HideAsync 失败后仍应释放实例");
        Assert.That(_context.Manager.GetOpenCount<ThrowingHideView>(), Is.EqualTo(0));
    }

    /// <summary>同一类型页面允许多开，计数随打开/关闭增减。</summary>
    [Test]
    public void MultiOpen_SameType_CountsInstances()
    {
        var first = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();
        var second = _context.Manager
            .OpenPageAsync<TestPageView>(UILayer.Stack)
            .GetAwaiter()
            .GetResult();

        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(2));
        Assert.That(_context.Manager.IsOpen<TestPageView>(), Is.True);

        first.Dispose();

        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(1));
        Assert.That(_context.Manager.IsOpen<TestPageView>(), Is.True);
        Assert.That(second.IsValid, Is.True);
    }

    /// <summary>未打开任何页面时，IsOpen 返回 false、计数为 0。</summary>
    [Test]
    public void IsOpen_ReturnsFalseWhenNothingOpen()
    {
        Assert.That(_context.Manager.IsOpen<TestPageView>(), Is.False);
        Assert.That(_context.Manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
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
        var stopwatch = Stopwatch.StartNew();
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
        for (var i = 0; i < _context.LifecycleLog.Count; i++)
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
