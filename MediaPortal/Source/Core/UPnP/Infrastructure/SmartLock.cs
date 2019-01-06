#define DEBUG_LOCK_HOLDS

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace UPnP.Infrastructure
{
  public struct SmartLock : IDisposable
  {
#if DEBUG_LOCK_HOLDS
    private static readonly IDictionary<object, string> _lockHolders = new ConcurrentDictionary<object, string>();
#endif
    private object _lockedObject;
    private bool _lockTaken;

    public void TryEnter(object onObject, TimeSpan timeout)
    {
      TryEnter(onObject, (int)timeout.TotalMilliseconds);
    }
    public void TryEnter(object onObject, int timeoutMillisecond = 200 /* ms */)
    {
#if DEBUG
      if (onObject == null) throw new ArgumentNullException(nameof(onObject));
      if (_lockedObject != null) throw new InvalidOperationException("Illegal use of Lock: Lock method must only called once per Lock instance.");
      if (onObject.GetType().IsValueType) throw new InvalidOperationException("Illegal use of Lock. Must not lock on a value type object.");
#endif
      _lockedObject = onObject;
      Monitor.TryEnter(onObject, timeoutMillisecond, ref _lockTaken);
      if (_lockTaken == false)
      {
        //ServiceRegistration.Get<ILogger>().Error("Could not aquire lock.");
        string holdingStackTrace = string.Empty;
#if DEBUG_LOCK_HOLDS
        _lockHolders.TryGetValue(_lockedObject, out holdingStackTrace);
#endif
        throw new TimeoutException("Did not aquire lock in specified time. Hold by: " + holdingStackTrace);
      }

#if DEBUG_LOCK_HOLDS
      _lockHolders[_lockedObject] = new StackTrace().ToString();
#endif
    }

    public void Dispose()
    {
#if DEBUG
      if (_lockedObject == null) throw new InvalidOperationException("Illegal use of Lock. Lock must have been called before releasing/disposing.");
#endif
      if (_lockTaken)
      {
        _lockTaken = false;
        Monitor.Exit(_lockedObject);
#if DEBUG_LOCK_HOLDS
        _lockHolders.Remove(_lockedObject);
#endif
      }
    }
  }
}
