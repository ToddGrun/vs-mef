﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Composition.Reflection;

    /// <summary>
    /// Static factory methods for creating .NET Lazy{T} instances.
    /// </summary>
    internal static class LazyServices
    {
        private static readonly MethodInfo CreateStronglyTypedLazyOfTMValue = typeof(LazyServices).GetTypeInfo().GetMethod("CreateStronglyTypedLazyOfTM", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly MethodInfo CreateStronglyTypedLazyOfTValue = typeof(LazyServices).GetTypeInfo().GetMethod("CreateStronglyTypedLazyOfT", BindingFlags.NonPublic | BindingFlags.Static)!;
        private static readonly string Lazy1FullName = typeof(Lazy<>).FullName!;
        private static readonly string Lazy2FullName = typeof(Lazy<,>).FullName!;

        internal static readonly Type DefaultMetadataViewType = typeof(IDictionary<string, object>);
        internal static readonly Type DefaultExportedValueType = typeof(object);

        /// <summary>
        /// Gets a value indicating whether a type represents either a <see cref="Lazy{T}"/>
        /// or a <see cref="Lazy{T, TMetadata}"/> (as opposed to not a Lazy type at all).
        /// </summary>
        /// <param name="type">The type to be tested.</param>
        /// <returns><see langword="true"/> if <paramref name="type"/> is some Lazy type; <see langword="false"/> otherwise.</returns>
        internal static bool IsAnyLazyType(this Type type)
        {
            if (type?.GetTypeInfo().IsGenericType ?? false)
            {
                Type genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Lazy<>) || genericTypeDefinition == typeof(Lazy<,>))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a value indicating whether a type represents either a <see cref="Lazy{T}"/>
        /// or a <see cref="Lazy{T, TMetadata}"/> (as opposed to not a Lazy type at all).
        /// </summary>
        /// <param name="typeRef">The type to be tested.</param>
        /// <returns><see langword="true"/> if <paramref name="typeRef"/> is some Lazy type; <see langword="false"/> otherwise.</returns>
        internal static bool IsAnyLazyType(this TypeRef typeRef)
        {
            if (typeRef?.IsGenericType ?? false)
            {
                if (typeRef.FullName == Lazy1FullName || typeRef.FullName == Lazy2FullName)
                {
                    return true;
                }
            }

            return false;
        }

        internal static Lazy<T> FromValue<T>(T value)
            where T : class
        {
            return new Lazy<T>(DelegateServices.FromValue(value), LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Creates a factory that takes a Func{object} and object-typed metadata
        /// and returns a strongly-typed Lazy{T, TMetadata} instance.
        /// </summary>
        /// <param name="exportType">The type of values created by the Func{object} value factories. Null is interpreted to be <c>typeof(object)</c>.</param>
        /// <param name="metadataViewType">The type of metadata passed to the lazy factory. Null is interpreted to be <c>typeof(IDictionary{string, object})</c>.</param>
        /// <returns>A function that takes a Func{object} value factory and metadata, and produces a Lazy{T, TMetadata} instance.</returns>
        internal static Func<AssemblyName, Func<object?>, object, object> CreateStronglyTypedLazyFactory(Type? exportType, Type? metadataViewType)
        {
            MethodInfo genericMethod;
            if (metadataViewType != null)
            {
                genericMethod = CreateStronglyTypedLazyOfTMValue.MakeGenericMethod(exportType ?? DefaultExportedValueType, metadataViewType);
            }
            else
            {
                genericMethod = CreateStronglyTypedLazyOfTValue.MakeGenericMethod(exportType ?? DefaultExportedValueType);
            }

            return (Func<AssemblyName, Func<object?>, object, object>)genericMethod.CreateDelegate(typeof(Func<AssemblyName, Func<object?>, object, object>));
        }

        internal static Func<T> AsFunc<T>(this Lazy<T> lazy)
        {
            Requires.NotNull(lazy, nameof(lazy));

            // Theoretically, this is the most efficient approach. It only allocates a delegate (no closure).
            // But it unfortunately results in a slow path within the CLR (clr!COMDelegate::DelegateConstruct)
            // That ends up taking 52ms in Auto7 solution open.
            ////return new Func<T>(lazy.GetLazyValue);

            // So instead, we allocate the closure and qualify for the CLR's fast path.
            return () => lazy.Value;
        }

        private static T GetLazyValue<T>(this Lazy<T> lazy)
        {
            return lazy.Value;
        }

        private static Lazy<T> CreateStronglyTypedLazyOfT<T>(AssemblyName assemblyName, Func<object> funcOfObject, object metadata)
        {
            Requires.NotNull(funcOfObject, nameof(funcOfObject));

            return new ComposedLazy<T>(assemblyName, funcOfObject.As<T>());
        }

        private static Lazy<T, TMetadata> CreateStronglyTypedLazyOfTM<T, TMetadata>(AssemblyName assemblyName, Func<object> funcOfObject, object metadata)
        {
            Requires.NotNull(funcOfObject, nameof(funcOfObject));
            Requires.NotNullAllowStructs(metadata, nameof(metadata));

            return new ComposedLazy<T, TMetadata>(assemblyName, funcOfObject.As<T>(), (TMetadata)metadata);
        }
    }
}
