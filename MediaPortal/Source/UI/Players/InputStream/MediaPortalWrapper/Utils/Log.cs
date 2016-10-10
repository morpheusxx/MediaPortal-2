﻿using System.IO;

namespace MediaPortalWrapper.Utils
{
  public static class Logger
  {
    const string WRAPPER_LOG = "Wrapper.log";

    public static void Clear()
    {
      if (File.Exists(WRAPPER_LOG))
        File.Delete(WRAPPER_LOG);
    }

    public static void Log(string format, params object[] args)
    {
      using (var logFile = new FileStream(WRAPPER_LOG, FileMode.Append))
      using (var writer = new StreamWriter(logFile))
      {
        writer.WriteLine(format, args);
      }
    }
  }
}
