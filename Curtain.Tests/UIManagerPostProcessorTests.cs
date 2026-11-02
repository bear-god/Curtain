// <copyright file="UIManagerPostProcessorTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using System.Collections.Generic;
using Curtain.Abstraction;
using Curtain.Core;
using NUnit.Framework;

/// <summary>
/// UIManager 视图装饰接缝测试：调用时机、入参、失败清理。
/// </summary>
[TestFixture]
public class UIManagerPostProcessorTests
{
    private RecordingPostProcessor _processor;
    private FakeUiResourceLoader _loader;
    private UIManager _manager;

    /// <summary>每个测试执行前的初始化。</summary>
    [SetUp]
    public void SetUp()
    {
        TestLifecycleLog.Events = new List<string>();
        _processor = new RecordingPostProcessor();
        _loader = new FakeUiResourceLoader();
        _manager = new UIManager(_loader, new FakeUiRoot(), null, _processor);
    }

    /// <summary>每个测试执行后的清理。</summary>
    [TearDown]
    public void TearDown()
    {
        TestLifecycleLog.Events = new List<string>();
    }

    /// <summary>接缝在 Initialize 之前调用，且此时句柄尚未打开。</summary>
    [Test]
    public void OpenPage_InvokesPostProcessorBeforeInitialize()
    {
        UIHandle handle = _manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult();

        Assert.That(TestLifecycleLog.Events, Is.EqualTo(new[]
        {
            "View.SetActive(false)",
            "Processor.Process",
            "View.Initialize",
            "View.SetActive(true)",
        }));
        Assert.That(_processor.HandleWasOpen, Is.False);
        Assert.That(_processor.Layer, Is.EqualTo(UILayer.Stack));
        Assert.That(_processor.View, Is.SameAs(handle.View));
        Assert.That(handle.IsOpen, Is.True);
    }

    /// <summary>接缝抛异常时按打开失败处理：释放实例并结束句柄。</summary>
    [Test]
    public void OpenPage_PostProcessorThrows_ReleasesViewAndClosesHandle()
    {
        _processor.ThrowOnProcess = new InvalidOperationException("注入失败");

        Assert.Catch<UIOpenException>(() =>
            _manager.OpenPageAsync<TestPageView>(UILayer.Stack).GetAwaiter().GetResult());

        Assert.That(_loader.ReleasedCount, Is.EqualTo(1));
        Assert.That(_processor.Handle.IsOpen, Is.False);
        Assert.That(_manager.GetOpenCount<TestPageView>(), Is.EqualTo(0));
    }

    private sealed class RecordingPostProcessor : IViewPostProcessor
    {
        public IView View { get; private set; }

        public UIHandle Handle { get; private set; }

        public UILayer Layer { get; private set; }

        public bool HandleWasOpen { get; private set; }

        public Exception ThrowOnProcess { get; set; }

        public void Process(IView view, UIHandle handle, UILayer layer)
        {
            View = view;
            Handle = handle;
            Layer = layer;
            HandleWasOpen = handle.IsOpen;
            TestLifecycleLog.Add("Processor.Process");

            if (ThrowOnProcess != null)
            {
                throw ThrowOnProcess;
            }
        }
    }
}
