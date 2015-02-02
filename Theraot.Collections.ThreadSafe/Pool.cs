﻿using System;
using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    internal class Pool<T>
        where T : class
    {
        private readonly int _id;
        private readonly FixedSizeQueueBucket<T> _entries;
        private readonly Action<T> _recycler;

        public Pool(int capacity, Action<T> recycler)
        {
            if (recycler == null)
            {
                throw new ArgumentNullException("recycler");
            }
            _id = PoolHelper.GetId();
            _entries = new FixedSizeQueueBucket<T>(capacity);
            _recycler = recycler;
        }

        internal void Donate(T entry)
        {
            if (!ReferenceEquals(entry, null) && !AppDomain.CurrentDomain.IsFinalizingForUnload() && PoolHelper.Enter(_id))
            {
                try
                {
                    _recycler(entry);
                    _entries.Add(entry);
                }
                finally
                {
                    PoolHelper.Leave(_id);
                }
            }
        }

        internal bool TryGet(out T result)
        {
            return _entries.TryTake(out result);
        }
    }
}
