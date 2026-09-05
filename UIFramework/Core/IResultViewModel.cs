// <copyright file="IResultViewModel.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace UIFramework.Core;

/// <summary>
/// Dialog ViewModel result interface.
/// The framework injects the handle before <see cref="ViewModelBase.OnInitialize"/>,
/// and the ViewModel returns the result via <c>Complete</c> / <c>Dismiss</c>.
/// </summary>
public interface IResultViewModel
{
    /// <summary>Receives the dialog handle for returning results.</summary>
    /// <param name="handle">The dialog handle.</param>
    void SetResultHandle(UIHandle handle);
}