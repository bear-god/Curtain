// <copyright file="UIManagerFailureTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using NUnit.Framework;
using Curtain.Core;

/// <summary>
/// UIManager 打开失败路径测试：缺少 [UI]、加载失败、生命周期异常。
/// 所有 OpenXxxAsync 在测试中同步完成，统一通过 GetAwaiter().GetResult() 同步取结果。
/// </summary>
[TestFixture]
public class UIManagerFailureTests
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

    /// <summary>View 缺少 [UI] 标注时抛出 UIOpenException，且不触发加载。</summary>
    [Test]
    public void OpenPage_MissingUiAttribute_Throws()
    {
        Assert.Catch<UIOpenException>(() =>
            _context.Manager
                .OpenPageAsync<NoAttributeView>(UILayer.Stack)
                .GetAwaiter()
                .GetResult());

        Assert.That(_context.Loader.InstantiatedPaths.Count, Is.EqualTo(0), "缺少标注时不应尝试加载资源");
    }

    /// <summary>资源加载返回 null 时抛出 UIOpenException。</summary>
    [Test]
    public void OpenPage_LoaderReturnsNull_Throws()
    {
        _context.Loader.ReturnNull = true;

        Assert.Catch<UIOpenException>(() =>
            _context.Manager
                .OpenPageAsync<TestPageView>(UILayer.Stack)
                .GetAwaiter()
                .GetResult());

        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(0), "没有实例化成功过，不应调用 Release");
    }

    /// <summary>资源加载抛异常时包装为 UIOpenException。</summary>
    [Test]
    public void OpenPage_LoaderThrows_WrapsAsUIOpenException()
    {
        _context.Loader.ThrowOnInstantiate = new InvalidOperationException("load failed");

        Assert.Catch<UIOpenException>(() =>
            _context.Manager
                .OpenPageAsync<TestPageView>(UILayer.Stack)
                .GetAwaiter()
                .GetResult());
    }

    /// <summary>打开失败时输出 Error 级别日志，异常对象原样透传。</summary>
    [Test]
    public void OpenPage_LoadFailure_LogsErrorWithException()
    {
        var failure = new InvalidOperationException("load failed");
        _context.Loader.ThrowOnInstantiate = failure;

        Assert.Catch<UIOpenException>(() =>
            _context.Manager
                .OpenPageAsync<TestPageView>(UILayer.Stack)
                .GetAwaiter()
                .GetResult());

        FakeLogRecord record = _context.Logger.LastOf(FakeLogLevel.Error);
        Assert.That(record, Is.Not.Null, "打开失败应输出 Error 日志");
        Assert.That(record.Exception, Is.SameAs(failure));
    }

    /// <summary>View 的 Initialize 抛异常时包装为 UIOpenException。</summary>
    [Test]
    public void OpenPage_ViewInitializeThrows_WrapsAsUIOpenException()
    {
        Assert.Catch<UIOpenException>(() =>
            _context.Manager
                .OpenPageAsync<ThrowingView>(UILayer.Stack)
                .GetAwaiter()
                .GetResult());
    }

    /// <summary>View 的 Initialize 抛异常时，已实例化的实例被释放且不登记为已打开（避免隐藏节点泄漏）。</summary>
    [Test]
    public void OpenPage_ViewInitializeThrows_ReleasesInstantiatedView()
    {
        Assert.Catch<UIOpenException>(() =>
            _context.Manager
                .OpenPageAsync<ThrowingView>(UILayer.Stack)
                .GetAwaiter()
                .GetResult());

        Assert.That(_context.Loader.ReleasedCount, Is.EqualTo(1), "打开失败后必须释放已实例化的 View");
        Assert.That(_context.Manager.GetOpenCount<ThrowingView>(), Is.EqualTo(0));
    }
}
