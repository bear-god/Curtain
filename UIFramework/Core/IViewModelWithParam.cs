// <copyright file="IViewModelWithParam.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace UIFramework.Core;

/// <summary>
/// ViewModel parameter injection interface.
/// The framework calls <see cref="SetParameter"/> before <see cref="ViewModelBase.OnInitialize"/>.
/// </summary>
/// <typeparam name="TParam">The parameter type.</typeparam>
public interface IViewModelWithParam<TParam>
{
    /// <summary>Receives the parameter passed when opening.</summary>
    /// <param name="parameter">The page or dialog parameter.</param>
    void SetParameter(TParam parameter);
}