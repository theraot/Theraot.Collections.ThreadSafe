﻿using System;
using System.Collections.Generic;
using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    public partial class Mapper<T> : IEnumerable<T>
    {
        private const int INT_Capacity = 16;
        private const int INT_MaxOffset = 32;
        private const int INT_OffsetStep = 4;
        private readonly Branch _root;
        private int _count;

        public Mapper()
        {
            _count = 0;
            _root = new Branch(INT_MaxOffset - INT_OffsetStep);
        }

        private interface INode
        {
            // Empty
        }

        /// <summary>
        /// Gets the number of items actually contained.
        /// </summary>
        public int Count
        {
            get
            {
                return _count;
            }
        }

        /// <summary>
        /// Copies the items to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="arrayIndex">Index of the array.</param>
        /// <exception cref="System.ArgumentNullException">array</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">arrayIndex;Non-negative number is required.</exception>
        /// <exception cref="System.ArgumentException">array;The array can not contain the number of elements.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException("arrayIndex", "Non-negative number is required.");
            }
            if (_count > array.Length - arrayIndex)
            {
                throw new ArgumentException("The array can not contain the number of elements.", "array");
            }
            try
            {
                foreach (var entry in _root)
                {
                    array[arrayIndex] = entry;
                    arrayIndex++;
                }
            }
            catch (IndexOutOfRangeException exception)
            {
                throw new ArgumentOutOfRangeException("array", exception.Message);
            }
        }

        /// <summary>
        /// Sets the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <param name="previous">The previous item in the specified index.</param>
        /// <returns>
        ///   <c>true</c> if the item was new; otherwise, <c>false</c>.
        /// </returns>
        public bool Exchange(int index, T item, out T previous)
        {
            if (_root.Exchange(unchecked((uint)index), item, out previous))
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _root.GetEnumerator();
        }

        /// <summary>
        /// Inserts the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <returns>
        ///   <c>true</c> if the item was inserted; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// The insertion can fail if the index is already used or is being written by another thread.
        /// If the index is being written it can be understood that the insert operation happened before but the item was overwritten or removed.
        /// </remarks>
        public bool Insert(int index, T item)
        {
            T previous;
            if (_root.Insert(unchecked((uint)index), item, out previous))
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            else
            {
                return false;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGet(int index, out T value)
        {
            return _root.TryGet(unchecked((uint)index), out value);
        }

        public void TrySet(int index, T value, out bool isNew)
        {
            _root.TrySet(unchecked((uint)index), value, out isNew);
            if (isNew)
            {
                Interlocked.Increment(ref _count);
            }
        }

        private struct Leaf : INode
        {
            private readonly T _value;

            public Leaf(T value)
            {
                _value = value;
            }

            public T Value
            {
                get
                {
                    return _value;
                }
            }
        }
    }
}