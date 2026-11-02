// <copyright file="UIHandleTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using NUnit.Framework;
using Curtain.Abstraction;
using Curtain.Core;

/// <summary>
/// <see cref="UIHandle{TResult}"/> 对话框结果语义测试：Complete 的幂等性，以及句柄状态访问契约。
/// 通过 InternalsVisibleTo 直接构造句柄。
/// </summary>
[TestFixture]
public class UIHandleTests
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

    /// <summary>Complete 设置已完成结果。</summary>
    [Test]
    public void Complete_SetsCompletedResult()
    {
        UIHandle<int> handle = new UIHandle<int>(1, _context.Manager);
        handle.Complete(42);

        UIResult<int> result = handle.GetResult().GetAwaiter().GetResult();
        Assert.That(result.IsCompleted, Is.True);
        Assert.That(result.Value, Is.EqualTo(42));
    }

    /// <summary>多次 Complete 只有第一次生效。</summary>
    [Test]
    public void Complete_Twice_FirstWins()
    {
        UIHandle<int> handle = new UIHandle<int>(1, _context.Manager);
        handle.Complete(1);
        handle.Complete(2);

        Assert.That(handle.GetResult().GetAwaiter().GetResult().Value, Is.EqualTo(1));
    }

    /// <summary>未打开的句柄访问 View 抛出 InvalidOperationException。</summary>
    [Test]
    public void View_ThrowsWhenNotOpen()
    {
        UIHandle handle = new UIHandle(1, _context.Manager);

        IView view = null;
        Assert.Throws<InvalidOperationException>(() => view = handle.View);
        Assert.That(handle.IsOpen, Is.False);
        Assert.That(view, Is.Null);
    }
}
