// <copyright file="IViewModelFactory.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

using System;

namespace UIFramework.Core;

/// <summary>
/// ViewModel 创建工厂抽象。
/// 由 DI 层（Reflex）实现，使 VM 可通过构造函数注入业务服务。
/// </summary>
public interface IViewModelFactory
{
    /// <summary>按类型创建一个 ViewModel 实例。</summary>
    /// <param name="viewModelType">ViewModel 类型。</param>
    /// <returns>创建的 ViewModel 实例。</returns>
    ViewModelBase Create(Type viewModelType);
}