// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.Composition
{
    using System.Collections;
    using System.Collections.Immutable;

    internal class NonSharingImmutableHashSet<T> : IImmutableSet<T>
    {
        private readonly HashSet<T> set;
        public static readonly NonSharingImmutableHashSet<T> Empty = new NonSharingImmutableHashSet<T>(new HashSet<T>());

        public NonSharingImmutableHashSet(HashSet<T> set)
        {
            this.set = set;
        }

        public int Count => this.set.Count;

        public IImmutableSet<T> Add(T value)
        {
            var newSet = new HashSet<T>(this.set);
            newSet.Add(value);

            return new NonSharingImmutableHashSet<T>(newSet);
        }

        public IImmutableSet<T> Clear()
        {
            return NonSharingImmutableHashSet<T>.Empty;
        }

        public bool Contains(T value)
        {
            return this.set.Contains(value);
        }

        public IImmutableSet<T> Except(IEnumerable<T> other)
        {
            var newSet = new HashSet<T>(this.set);
            newSet.ExceptWith(other);

            return new NonSharingImmutableHashSet<T>(newSet);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.set.GetEnumerator();
        }

        public IImmutableSet<T> Intersect(IEnumerable<T> other)
        {
            var newSet = new HashSet<T>(this.set);
            newSet.IntersectWith(other);

            return new NonSharingImmutableHashSet<T>(newSet);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return this.set.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return this.set.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return this.set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return this.set.IsSubsetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return this.set.Overlaps(other);
        }

        public IImmutableSet<T> Remove(T value)
        {
            var newSet = new HashSet<T>(this.set);
            newSet.Remove(value);

            return new NonSharingImmutableHashSet<T>(newSet);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return this.set.SetEquals(other);
        }

        public IImmutableSet<T> SymmetricExcept(IEnumerable<T> other)
        {
            var newSet = new HashSet<T>(this.set);
            newSet.SymmetricExceptWith(other);

            return new NonSharingImmutableHashSet<T>(newSet);
        }

        public bool TryGetValue(T equalValue, out T actualValue)
        {
#if NETCOREAPP
            return this.set.TryGetValue(equalValue, out actualValue!);
#else
            actualValue = equalValue;

            if (!this.set.Contains(equalValue))
            {
                return false;
            }

            var comparer = this.set.Comparer;
            foreach (var value in this.set)
            {
                if (comparer.Equals(value, equalValue))
                {
                    actualValue = value;
                    break;
                }
            }

            return true;
#endif
        }

        public IImmutableSet<T> Union(IEnumerable<T> other)
        {
            var newSet = new HashSet<T>(this.set);
            newSet.UnionWith(other);

            return new NonSharingImmutableHashSet<T>(newSet);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.set.GetEnumerator();
        }
    }
}
