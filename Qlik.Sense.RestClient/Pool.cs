using System;
using System.Collections.Concurrent;

namespace Qlik.Sense.RestClient
{
    public class Pool<T>
    {
        private readonly ConcurrentBag<T> _pool = new ConcurrentBag<T>();
        private readonly Func<T> _constructor;

        public Pool(Func<T> contsructor)
        {
            _constructor = contsructor;
        }

        public Borrowed<T> Borrow()
        {
            return _pool.TryTake(out var x) ? new Borrowed<T>(x, _pool) : new Borrowed<T>(_constructor(), _pool);
        }

        public void Drain()
        {
            while (_pool.TryTake(out var t))
            {
                if (_pool.IsEmpty)
                {
                    _pool.Add(t);
                    return;
                }
            }
        }
    }

    public class Borrowed<T> : IDisposable
    {
        private readonly ConcurrentBag<T> _thePool;
        private bool _returned = false;

        private readonly T _it;

        public T It
        {
            get
            {
                if (_returned) throw new Exception("Object already returned to pool.");
                return _it;
            }
        }

        public Borrowed(T x, ConcurrentBag<T> pool)
        {
            _it = x;
            _thePool = pool;
        }

        public void Return()
        {
            if (_returned) throw new Exception("Object already returned to pool.");
            _returned = true;
            _thePool.Add(_it);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_returned)
                    Return();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}