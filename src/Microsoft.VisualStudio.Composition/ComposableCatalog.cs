// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.Composition.Reflection;

    public class ComposableCatalog : IEquatable<ComposableCatalog>
    {
        /// <summary>
        /// The parts in the catalog. Do not mutate.
        /// </summary>
        private HashSet<ComposablePartDefinition> parts;

        /// <summary>
        /// The exports from parts in this catalog, indexed by contract name. Do not mutate.
        /// </summary>
        private Dictionary<string, List<ExportDefinitionBinding>> exportsByContract;

        /// <summary>
        /// The types that are represented in this catalog. Do not mutate.
        /// </summary>
        private HashSet<TypeRef> typesBackingParts;

        private ComposableCatalog(HashSet<ComposablePartDefinition> parts, Dictionary<string, List<ExportDefinitionBinding>> exportsByContract, HashSet<TypeRef> typesBackingParts, DiscoveredParts discoveredParts, Resolver resolver)
        {
            Requires.NotNull(parts, nameof(parts));
            Requires.NotNull(exportsByContract, nameof(exportsByContract));
            Requires.NotNull(typesBackingParts, nameof(typesBackingParts));
            Requires.NotNull(discoveredParts, nameof(discoveredParts));
            Requires.NotNull(resolver, nameof(resolver));

            this.parts = parts;
            this.exportsByContract = exportsByContract;
            this.typesBackingParts = typesBackingParts;
            this.DiscoveredParts = discoveredParts;
            this.Resolver = resolver;
        }

        /// <summary>
        /// Gets the set of parts that belong to the catalog.
        /// </summary>
        public IImmutableSet<ComposablePartDefinition> Parts
        {
            get { return new NonSharingImmutableHashSet<ComposablePartDefinition>(this.parts); }
        }

        /// <summary>
        /// Gets the parts that were added to the catalog via a <see cref="PartDiscovery"/> class.
        /// </summary>
        public DiscoveredParts DiscoveredParts { get; private set; }

        internal Resolver Resolver { get; }

        public static ComposableCatalog Create(Resolver resolver)
        {
            return new ComposableCatalog(
                new HashSet<ComposablePartDefinition>(),
                new Dictionary<string, List<ExportDefinitionBinding>>(),
                new HashSet<TypeRef>(),
                DiscoveredParts.Empty,
                resolver);
        }

        public ComposableCatalog AddPart(ComposablePartDefinition partDefinition)
        {
            Requires.NotNull(partDefinition, nameof(partDefinition));

            return this.AddParts(new[] { partDefinition });
        }

        private int thisAddPartsCall = 0;
        private static int maxAddPartsCall = 0;
        private static int addPartsCalls = 0;

        public ComposableCatalog AddParts(IEnumerable<ComposablePartDefinition> parts)
        {
            Requires.NotNull(parts, nameof(parts));

            Interlocked.Increment(ref this.thisAddPartsCall);
            Interlocked.Increment(ref addPartsCalls);

            if (this.thisAddPartsCall > maxAddPartsCall)
            {
                maxAddPartsCall = this.thisAddPartsCall;
            }

            var newParts = new HashSet<ComposablePartDefinition>(this.parts);
            var newTypesBackingParts = new HashSet<TypeRef>(this.typesBackingParts);
            var newExportsByContract = new Dictionary<string, List<ExportDefinitionBinding>>();
            var updatedContracts = new HashSet<string>();

            foreach (var kvp in this.exportsByContract)
            {
                newExportsByContract.Add(kvp.Key, kvp.Value);
            }

            foreach (var partDefinition in parts)
            {
                if (newParts.Contains(partDefinition))
                {
                    // This part is already in the catalog.
                    continue;
                }

                if (newTypesBackingParts.Contains(partDefinition.TypeRef))
                {
                    Requires.Argument(false, nameof(partDefinition), Strings.TypeAlreadyInCatalogAsAnotherPart, partDefinition.TypeRef.FullName);
                }

                AddExportDefinitionBindings(partDefinition.ExportedTypes, partDefinition, default(MemberRef), newExportsByContract, updatedContracts);

                foreach (var exportPair in partDefinition.ExportingMembers)
                {
                    AddExportDefinitionBindings(exportPair.Value, partDefinition, exportPair.Key, newExportsByContract, updatedContracts);
                }

                newParts.Add(partDefinition);
                newTypesBackingParts.Add(partDefinition.TypeRef);
            }

            return new ComposableCatalog(
                newParts,
                newExportsByContract,
                newTypesBackingParts,
                this.DiscoveredParts,
                this.Resolver);

            static void AddExportDefinitionBindings(
                IReadOnlyCollection<ExportDefinition> exportedTypes,
                ComposablePartDefinition partDefinition,
                MemberRef? member,
                Dictionary<string, List<ExportDefinitionBinding>> newExportsByContract,
                HashSet<string> updatedContracts)
            {
                foreach (var exportDefinition in exportedTypes)
                {
                    var contractName = exportDefinition.ContractName;
                    var newExportBinding = new ExportDefinitionBinding(exportDefinition, partDefinition, member);

                    if (!updatedContracts.Contains(contractName))
                    {
                        // This contract hasn't yet been changed. Create a new list for storing it's export bindings.
                        updatedContracts.Add(contractName);

                        if (!newExportsByContract.TryGetValue(contractName, out var exportBindings))
                        {
                            exportBindings = new List<ExportDefinitionBinding>();
                        }
                        else
                        {
                            // Duplicate existing export bindings
                            exportBindings = new List<ExportDefinitionBinding>(exportBindings);
                        }

                        newExportsByContract[contractName] = exportBindings;
                        exportBindings.Add(newExportBinding);
                    }
                    else
                    {
                        // This contract's bindings have already changed. Update in place.
                        newExportsByContract[contractName].Add(newExportBinding);
                    }
                }
            }
        }

        public ComposableCatalog AddParts(DiscoveredParts parts)
        {
            Requires.NotNull(parts, nameof(parts));

            var catalog = this.AddParts(parts.Parts);
            return new ComposableCatalog(catalog.parts, catalog.exportsByContract, catalog.typesBackingParts, catalog.DiscoveredParts.Merge(parts), catalog.Resolver);
        }

        /// <summary>
        /// Merges this catalog with another one, including parts, discovery details and errors.
        /// </summary>
        /// <param name="catalogToMerge">The catalog to be merged with this one.</param>
        /// <returns>The merged version of the catalog.</returns>
        public ComposableCatalog AddCatalog(ComposableCatalog catalogToMerge)
        {
            Requires.NotNull(catalogToMerge, nameof(catalogToMerge));

            var catalog = this.AddParts(catalogToMerge.Parts);
            return new ComposableCatalog(catalog.parts, catalog.exportsByContract, catalog.typesBackingParts, catalog.DiscoveredParts.Merge(catalogToMerge.DiscoveredParts), catalog.Resolver);
        }

        /// <summary>
        /// Merges this catalog with others, including parts, discovery details and errors.
        /// </summary>
        /// <param name="catalogsToMerge">The catalogs to be merged with this one.</param>
        /// <returns>The merged version of the catalog.</returns>
        public ComposableCatalog AddCatalogs(IEnumerable<ComposableCatalog> catalogsToMerge)
        {
            Requires.NotNull(catalogsToMerge, nameof(catalogsToMerge));

            return catalogsToMerge.Aggregate(this, (aggregate, mergeCatalog) => aggregate.AddCatalog(mergeCatalog));
        }

        public IReadOnlyCollection<AssemblyName> GetInputAssemblies()
        {
            var assemblyCache = new Dictionary<Assembly, AssemblyName>();

            var inputAssemblies = ImmutableHashSet.CreateBuilder(ByValueEquality.AssemblyName);
            foreach (var part in this.Parts)
            {
                part.GetInputAssemblies(inputAssemblies, GetAssemblyName);
            }

            return inputAssemblies.ToImmutable();

            AssemblyName GetAssemblyName(Assembly assembly)
            {
                // Assembly.GetName() is non-trivial to calculate and adds up when asking
                // for the same set of assemblies over and over again, so cache the retrieval
                if (!assemblyCache.TryGetValue(assembly, out AssemblyName? assemblyName))
                {
                    assemblyName = assembly.GetName();
                    assemblyCache.Add(assembly, assemblyName);
                }

                return assemblyName;
            }
        }

        public bool Equals(ComposableCatalog? other)
        {
            if (other == null)
            {
                return false;
            }

            // A catalog is just the sum of its parts. Anything else is a side-effect of how it was discovered,
            // which shouldn't impact an equivalence check.
            bool result = this.parts.SetEquals(other.parts);
            return result;
        }

        public override int GetHashCode()
        {
            int hashCode = this.Parts.Count;
            foreach (var part in this.Parts)
            {
                hashCode += part.GetHashCode();
            }

            return hashCode;
        }

        public void ToString(TextWriter writer)
        {
            var indentingWriter = IndentingTextWriter.Get(writer);
            using (indentingWriter.Indent())
            {
                foreach (var part in this.parts)
                {
                    indentingWriter.WriteLine("Part");
                    using (indentingWriter.Indent())
                    {
                        part.ToString(indentingWriter);
                    }
                }
            }
        }

        public IReadOnlyList<ExportDefinitionBinding> GetExports(ImportDefinition importDefinition)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));

            // We always want to consider exports with a matching contract name.
            if (!this.exportsByContract.TryGetValue(importDefinition.ContractName, out var exports))
            {
                exports = new List<ExportDefinitionBinding>();
            }

            // For those imports of generic types, we also want to consider exports that are based on open generic exports,
            string? genericTypeDefinitionContractName;
            Type[]? genericTypeArguments;
            if (TryGetOpenGenericExport(importDefinition, out genericTypeDefinitionContractName, out genericTypeArguments))
            {
                if (!this.exportsByContract.TryGetValue(genericTypeDefinitionContractName, out var openGenericExports))
                {
                    openGenericExports = new List<ExportDefinitionBinding>();
                }

                // We have to synthesize exports to match the required generic type arguments.
                exports.AddRange(
                    from export in openGenericExports
                    select export.CloseGenericExport(genericTypeArguments));
            }

            var filteredExports = from export in exports
                                  where importDefinition.ExportConstraints.All(c => c.IsSatisfiedBy(export.ExportDefinition))
                                  select export;

            return ImmutableList.CreateRange(filteredExports);
        }

        internal static bool TryGetOpenGenericExport(ImportDefinition importDefinition, [NotNullWhen(true)] out string? contractName, [NotNullWhen(true)] out Type[]? typeArguments)
        {
            Requires.NotNull(importDefinition, nameof(importDefinition));

            // TODO: if the importer isn't using a customized contract name.
            if (importDefinition.Metadata.TryGetValue(CompositionConstants.GenericContractMetadataName, out contractName) &&
                importDefinition.Metadata.TryGetValue(CompositionConstants.GenericParametersMetadataName, out typeArguments))
            {
                return true;
            }

            contractName = null;
            typeArguments = null;
            return false;
        }
    }
}
