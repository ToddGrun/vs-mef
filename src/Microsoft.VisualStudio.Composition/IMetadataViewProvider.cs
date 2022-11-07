﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides metadata view proxy instances for arbitrary metadata view interfaces.
    /// </summary>
    internal interface IMetadataViewProvider
    {
        /// <summary>
        /// Gets a value indicating whether this provider can create a metadata proxy for a given type.
        /// </summary>
        /// <param name="metadataType">The type of the required proxy.</param>
        /// <returns><see langword="true"/> if the provider can create a proxy for this type. Otherwise false.</returns>
        bool IsMetadataViewSupported(Type metadataType);

        /// <summary>
        /// Creates a metadata view that acts as a strongly-typed accessor
        /// to a metadata dictionary.
        /// </summary>
        /// <param name="metadata">The metadata dictionary. Never null.</param>
        /// <param name="defaultValues">The metadata dictionary of defaults, to be used when <paramref name="metadata"/> is missing a key. Is never null.</param>
        /// <param name="metadataViewType">The type of metadata view to create.</param>
        /// <returns>The proxy instance.</returns>
        object CreateProxy(IReadOnlyDictionary<string, object?> metadata, IReadOnlyDictionary<string, object?> defaultValues, Type metadataViewType);
    }
}
