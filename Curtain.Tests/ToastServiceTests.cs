// <copyright file="ToastServiceTests.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Tests;

using System;
using System.Collections.Generic;
using Curtain.Toast;
using NUnit.Framework;

/// <summary>
/// ToastService 队列测试：容量、淘汰、过期出队与参数校验。
/// </summary>
[TestFixture]
public class ToastServiceTests
{
    private FakeToastPresenter _presenter;
    private ToastService _toasts;

    /// <summary>每个测试执行前的初始化。</summary>
    [SetUp]
    public void SetUp()
    {
        _presenter = new FakeToastPresenter();
        _toasts = new ToastService(_presenter);
    }

    /// <summary>显示 Toast 时自增标识并交给呈现器。</summary>
    [Test]
    public void Show_NotifiesPresenterWithIncrementingIds()
    {
        _toasts.Show("第一条");
        _toasts.Show("第二条");

        Assert.That(_presenter.Shown, Is.EqualTo(new[] { "1:第一条", "2:第二条" }));
        Assert.That(_presenter.DismissedIds, Is.Empty);
    }

    /// <summary>超过默认容量时淘汰最旧一条。</summary>
    [Test]
    public void Show_ExceedingCapacity_DismissesOldest()
    {
        _toasts.Show("第一条");
        _toasts.Show("第二条");
        _toasts.Show("第三条");

        Assert.That(_presenter.DismissedIds, Is.EqualTo(new[] { 1 }));
        Assert.That(_presenter.Shown, Has.Count.EqualTo(3));
    }

    /// <summary>过期回调只移除对应条目，后续显示不再淘汰它。</summary>
    [Test]
    public void Expired_RemovesEntryFromQueue()
    {
        _toasts.Show("第一条");
        _toasts.Show("第二条");
        _presenter.Expire(1);
        _toasts.Show("第三条");

        Assert.That(_presenter.DismissedIds, Is.Empty, "队列已因过期腾出空间，不应淘汰");
    }

    /// <summary>容量必须为正，空文案被拒绝。</summary>
    [Test]
    public void InvalidArguments_Throw()
    {
        Assert.Catch<ArgumentOutOfRangeException>(() => new ToastService(_presenter, 0));
        Assert.Catch<ArgumentException>(() => _toasts.Show(string.Empty));
    }

    private sealed class FakeToastPresenter : IToastPresenter
    {
        private readonly List<string> _shown = new List<string>();
        private readonly List<int> _dismissedIds = new List<int>();

        public event Action<int> Expired;

        public IReadOnlyList<string> Shown => _shown;

        public IReadOnlyList<int> DismissedIds => _dismissedIds;

        public void Show(int toastId, string message)
        {
            _shown.Add(toastId + ":" + message);
        }

        public void Dismiss(int toastId)
        {
            _dismissedIds.Add(toastId);
        }

        public void Expire(int toastId)
        {
            Expired?.Invoke(toastId);
        }
    }
}
