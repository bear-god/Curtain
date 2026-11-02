// <copyright file="ToastService.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Toast;

using System;
using System.Collections.Generic;

/// <summary>
/// Toast 队列：最多保留 <see cref="Capacity"/> 条，新在上、旧在下；溢出时淘汰最旧一条。
/// 不进入页面栈，不参与导航；标识自增，过期回调负责出队。
/// </summary>
public sealed class ToastService
{
    /// <summary>默认队列容量。</summary>
    public const int DefaultCapacity = 2;

    private readonly IToastPresenter _presenter;
    private readonly List<ToastEntry> _entries = new List<ToastEntry>();
    private readonly int _capacity;
    private int _nextId = 1;

    /// <summary>初始化 <see cref="ToastService"/> 的新实例。</summary>
    /// <param name="presenter">Toast 呈现接缝。</param>
    /// <param name="capacity">队列容量，至少 1 条。</param>
    public ToastService(IToastPresenter presenter, int capacity = DefaultCapacity)
    {
        if (presenter == null)
        {
            throw new ArgumentNullException(nameof(presenter));
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Toast 队列容量至少为 1。");
        }

        _presenter = presenter;
        _capacity = capacity;
        _presenter.Expired += OnExpired;
    }

    /// <summary>获取队列容量。</summary>
    public int Capacity => _capacity;

    /// <summary>显示一条 Toast；队列已满时淘汰最旧一条。</summary>
    /// <param name="message">提示文案。</param>
    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            throw new ArgumentException("Toast 文案不能为空。", nameof(message));
        }

        ToastEntry entry = new ToastEntry(_nextId++, message);
        ToastEntry evicted = null;

        _entries.Insert(0, entry);
        if (_entries.Count > _capacity)
        {
            evicted = _entries[_entries.Count - 1];
            _entries.RemoveAt(_entries.Count - 1);
        }

        if (evicted != null)
        {
            _presenter.Dismiss(evicted.Id);
        }

        _presenter.Show(entry.Id, entry.Message);
    }

    private void OnExpired(int toastId)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].Id == toastId)
            {
                _entries.RemoveAt(i);
                return;
            }
        }
    }

    private sealed class ToastEntry
    {
        public ToastEntry(int id, string message)
        {
            Id = id;
            Message = message;
        }

        public int Id { get; }

        public string Message { get; }
    }
}
