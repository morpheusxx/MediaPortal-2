using System.ComponentModel;

namespace MediaPortal.UI.Players.Video.Tools
{
  public static class InvokeHelper
  {
    public delegate void InvokeIfRequiredDelegate<T>(T obj)
      where T : ISynchronizeInvoke;

    public static void InvokeIfRequired<T>(this T obj, InvokeIfRequiredDelegate<T> action)
      where T : ISynchronizeInvoke
    {
      if (obj.InvokeRequired)
      {
        obj.Invoke(action, new object[] { obj });
      }
      else
      {
        action(obj);
      }
    }

    public delegate TE InvokeIfRequiredDelegate<T, TE>(T obj)
      where T : ISynchronizeInvoke;

    public static TE InvokeIfRequired2<T, TE>(this T obj, InvokeIfRequiredDelegate<T, TE> action)
      where T : ISynchronizeInvoke
    {
      if (obj.InvokeRequired)
      {
        return (TE)obj.Invoke(action, new object[] { obj });
      }
      return action(obj);
    }
  }
}
