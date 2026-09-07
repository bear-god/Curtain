// <copyright file="UIManagerPageTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
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

    /// <summary>测试用特效类型。</summary>
    private readonly struct TestEffect : IUIEffect
    {
    }
}
