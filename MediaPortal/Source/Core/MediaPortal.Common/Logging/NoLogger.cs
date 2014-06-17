#region Copyright (C) 2007-2014 Team MediaPortal

/*
    Copyright (C) 2007-2014 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;

namespace MediaPortal.Common.Logging
{
  /// <summary>
  /// Default <see cref="ILogger"/> implementation that does absolutely nothing.
  /// </summary>
  public class NoLogger : ILogger
  {
    #region ILogger Members

    public void Debug(string format, params object[] args) { }
    public void Debug(string format, Exception ex, params object[] args) { }

    public void Info(string format, params object[] args) { }
    public void Info(string format, Exception ex, params object[] args) { }

    public void Warn(string format, params object[] args) { }
    public void Warn(string format, Exception ex, params object[] args) { }

    public void Error(string format, params object[] args) { }
    public void Error(string format, Exception ex, params object[] args) { }
    public void Error(Exception ex) { }

    public void Critical(string format, params object[] args) {}
    public void Critical(string format, Exception ex, params object[] args) { }
    public void Critical(Exception ex) { }

    #endregion
  }
}
