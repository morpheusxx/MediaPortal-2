#define DEBUG_LOCK_HOLDS

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace UPnP.Infrastructure
{
  public struct SmartLock : IDisposable
  {
#if DEBUG_LOCK_HOLDS
    private static readonly IDictionary<object, string> _lockHolders = new ConcurrentDictionary<object, string>();
#endif

    private const double MAX_LOCK_DURATION = 300; // Max ms within locked code
    private object _lockedObject;
    private bool _lockTaken;
    private Timer _timer;

    public void TryEnter(object onObject, TimeSpan timeout)
    {
      TryEnter(onObject, (int)timeout.TotalMilliseconds);
    }

    public void TryEnter(object onObject, int timeoutMillisecond = 400 /* ms */)
    {
#if DEBUG
      if (onObject == null) throw new ArgumentNullException(nameof(onObject));
      if (_lockedObject != null) throw new InvalidOperationException("Illegal use of Lock: Lock method must only called once per Lock instance.");
      if (onObject.GetType().IsValueType) throw new InvalidOperationException("Illegal use of Lock. Must not lock on a value type object.");
#endif
      _lockedObject = onObject;
      _timer = new Timer();
      _timer.Elapsed += ExitMonitor;
      _timer.Interval = MAX_LOCK_DURATION;
      _timer.AutoReset = false;
      _timer.Enabled = true;
      Monitor.TryEnter(onObject, timeoutMillisecond, ref _lockTaken);
#if DEBUG_LOCK_HOLDS
      // Remember who got the lock
      var st = new StackTrace().ToString();
      if (_lockTaken)
        _lockHolders[_lockedObject] = st;
      else
      {
        string holdingStackTrace;
        _lockHolders.TryGetValue(_lockedObject, out holdingStackTrace);
        WriteLog("Did not aquire lock in specified time. Caller: " + st + "\r\nHeld by: " + holdingStackTrace);
      }
#endif
    }

    private void ExitMonitor(object sender, ElapsedEventArgs e)
    {
      string holdingStackTrace = string.Empty;
#if DEBUG_LOCK_HOLDS
      _lockHolders.TryGetValue(_lockedObject, out holdingStackTrace);
#endif
      WriteLog("Failed to exit lock within timespan. Stacktrace: " + holdingStackTrace);
      ExitMonitor();
    }

    private void WriteLog(string message)
    {
      using (EventLog eventLog = new EventLog("Application"))
      {
        eventLog.Source = "Application";
        eventLog.WriteEntry(message, EventLogEntryType.Error);
      }
    }

    private void ExitMonitor()
    {
      if (_lockTaken)
      {
        _lockTaken = false;
        Monitor.Exit(_lockedObject);
#if DEBUG_LOCK_HOLDS
        _lockHolders.Remove(_lockedObject);
#endif
      }
    }

    public void Dispose()
    {
#if DEBUG
      if (_lockedObject == null) throw new InvalidOperationException("Illegal use of Lock. Lock must have been called before releasing/disposing.");
#endif
      ExitMonitor();
      _timer?.Dispose();
    }
  }
}
