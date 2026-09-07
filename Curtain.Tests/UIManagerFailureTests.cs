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
}
