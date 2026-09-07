// <copyright file="IUIRoot.cs" company="xpg">
// Copyright (c) xpg. All rights reserved.
// </copyright>

namespace Curtain.Abstraction;

using Curtain.Core;

/// <summary>
/// UI root abstraction. Provides layer containers for UI hierarchy.
/// </summary>
public interface IUIRoot
{
    /// <summary>Gets the layer container for the specified layer.</summary>
    /// <param name="layer">The target UI layer.</param>
    /// <returns>The container for the layer.</returns>
    IViewContainer GetLayerRoot(UILayer layer);
}
