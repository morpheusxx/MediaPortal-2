#region Copyright (C) 2007-2017 Team MediaPortal

/*
    Copyright (C) 2007-2017 Team MediaPortal
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
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using MediaPortal.Common;
using MediaPortal.Common.PathManager;
using MediaPortal.Utilities.DB;
using MediaPortal.Common.Services.Logging;
using MediaPortal.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MediaPortal.Backend.Services.Database
{
  /// <summary>
  /// Wrapper class for DB commands to support debug logging.
  /// </summary>
  public class LoggingDbCommandWrapper : IDbCommand
  {
    #region Private variables

    private static readonly FileLogger sqlDebugLog = FileLogger.CreateFileLogger(
        ServiceRegistration.Get<IPathManager>().GetPath(@"<LOG>\SQLDebug.log"), Common.Logging.LogLevel.Debug, false, true);
    private readonly IDbCommand _command = null;

    #endregion

    #region Constructor

    public LoggingDbCommandWrapper(IDbCommand command)
    {
      _command = command;
    }

    #endregion

    #region Protected members

    protected void DumpCommand()
    {
      DumpCommand(false, 0);
    }

    protected void DumpCommand(bool includeParameters, double timeSpanMs)
    {
      var settings = new JsonSerializerSettings
      {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
      };

      var wrapper = new DbCommandWrapper();
      wrapper.FromCommand(_command);
      var serialized = JsonConvert.SerializeObject(wrapper, settings);
      sqlDebugLog.Debug(StringUtils.EscapeCurlyBraces(serialized));
      //StringBuilder sbLogText = new StringBuilder();
      //sbLogText.Append("\r\n-------------------------------------------------------");
      //sbLogText.Append(SqlUtils.FormatSQL(_command.CommandText));
      //if (includeParameters)
      //{
      //  sbLogText.Append("\r\n-------------------------------------------------------");
      //  sbLogText.Append(SqlUtils.FormatSQLParameters(_command.Parameters));
      //}
      //sbLogText.AppendFormat("\r\n Query time {0:0.00} ms", timeSpanMs);
      //sbLogText.Append("\r\n=======================================================");
      //sqlDebugLog.Debug(StringUtils.EscapeCurlyBraces(sbLogText.ToString()));
    }

    public class DbCommandWrapper
    {
      public string CommandText;
      public CommandType CommandType;
      public List<DbParameterWrapper> Parameters = new List<DbParameterWrapper>();

      public void FromCommand(IDbCommand cmd)
      {
        CommandType = cmd.CommandType;
        CommandText = cmd.CommandText;
        foreach (DbParameter parameter in cmd.Parameters)
        {
          var pw = new DbParameterWrapper();
          pw.FromParam(parameter);
          Parameters.Add(pw);
        }
      }

      public void ToCommand(IDbCommand cmd)
      {
        cmd.CommandType = CommandType;
        cmd.CommandText = CommandText;
        foreach (DbParameterWrapper wrapper in Parameters)
        {
          var param = cmd.CreateParameter();
          wrapper.ToParam(param);
          cmd.Parameters.Add(param);
        }
      }
    }

    public class DbParameterWrapper
    {
      public string ParameterName;
      public DbType DbType;
      public ParameterDirection Direction;
      public int Size;
      public object Value;

      public void FromParam(DbParameter parameter)
      {
        ParameterName = parameter.ParameterName;
        DbType = parameter.DbType;
        Size = parameter.Size;
        Value = parameter.Value;
        Direction = parameter.Direction;
      }

      public void ToParam(IDbDataParameter parameter)
      {
        parameter.ParameterName = ParameterName;
        parameter.DbType = DbType;
        parameter.Size = Size;
        parameter.Value = Value;
        parameter.Direction = Direction;
      }
    }

    #endregion

    #region IDbCommand implementation

    public void Cancel()
    {
      _command.Cancel();
    }

    public string CommandText
    {
      get { return _command.CommandText; }
      set { _command.CommandText = value; }
    }

    public int CommandTimeout
    {
      get { return _command.CommandTimeout; }
      set { _command.CommandTimeout = value; }
    }

    public CommandType CommandType
    {
      get { return _command.CommandType; }
      set { _command.CommandType = value; }
    }

    public IDbConnection Connection
    {
      get { return _command.Connection; }
      set { _command.Connection = value; }
    }

    public IDbDataParameter CreateParameter()
    {
      return _command.CreateParameter();
    }

    public int ExecuteNonQuery()
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      var result = _command.ExecuteNonQuery();
      sw.Stop();
      DumpCommand(true, sw.Elapsed.TotalMilliseconds);
      return result;
    }

    public IDataReader ExecuteReader(CommandBehavior behavior)
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      var result = _command.ExecuteReader(behavior);
      sw.Stop();
      DumpCommand(true, sw.Elapsed.TotalMilliseconds);
      return result;
    }

    public IDataReader ExecuteReader()
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      var result = _command.ExecuteReader();
      sw.Stop();
      DumpCommand(true, sw.Elapsed.TotalMilliseconds);
      return result;
    }

    public object ExecuteScalar()
    {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      var result = _command.ExecuteScalar();
      sw.Stop();
      DumpCommand(true, sw.Elapsed.TotalMilliseconds);
      return result;
    }

    public IDataParameterCollection Parameters
    {
      get { return _command.Parameters; }
    }

    public void Prepare()
    {
      _command.Prepare();
    }

    public IDbTransaction Transaction
    {
      get
      {
        return _command.Transaction;
      }
      set
      {
        _command.Transaction = value;
      }
    }

    public UpdateRowSource UpdatedRowSource
    {
      get
      {
        return _command.UpdatedRowSource;
      }
      set
      {
        _command.UpdatedRowSource = value;
      }
    }

    #endregion

    #region IDisposable Member

    public void Dispose()
    {
      _command.Dispose();
    }

    #endregion
  }
}
