﻿using MediaPortal.Common;
using MediaPortal.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace MediaPortal.Plugins.ServerStateService.Interfaces
{
  public static class ServerStateSerializer
  {
    /// <summary>
    /// Custom assembly resolver, if set it will be used for state type lookups only.
    /// </summary>
    public static ResolveEventHandler CustomAssemblyResolver;

    /// <summary>
    /// Tries to get the <see cref="Type"/> from the given <paramref name="typeName"/>.
    /// </summary>
    /// <param name="typeName">Assembly qualified type name.</param>
    /// <returns></returns>
    public static Type GetStateType(string typeName)
    {
      ResolveEventHandler customAssemblyResolver = CustomAssemblyResolver;
      try
      {
        if (customAssemblyResolver != null)
          AppDomain.CurrentDomain.AssemblyResolve += customAssemblyResolver;

        return Type.GetType(typeName);
      }
      finally
      {
        if (customAssemblyResolver != null)
          AppDomain.CurrentDomain.AssemblyResolve -= customAssemblyResolver;
      }
    }

    public static T Deserialize<T>(string serialized)
    {
      return (T)Deserialize(typeof(T), serialized);
    }

    public static object Deserialize(string typeName, string serialized)
    {
      Type type = GetStateType(typeName);
      if (type == null)
      {
        ServiceRegistration.Get<ILogger>().Debug("ServerStateSerializer: Unable to deserialize object of type {0}", typeName);
        return null;
      }
      return Deserialize(type, serialized);
    }

    public static object Deserialize(Type type, string serialized)
    {
      XmlSerializer xmlSerializer = new XmlSerializer(type);
      return xmlSerializer.Deserialize(new StringReader(serialized));
    }

    public static string Serialize(object value)
    {
      StringBuilder serialized = new StringBuilder();
      XmlSerializer xmlSerializer = new XmlSerializer(value.GetType());
      using (XmlWriter writer = XmlWriter.Create(serialized, new XmlWriterSettings { OmitXmlDeclaration = true }))
        xmlSerializer.Serialize(writer, value);
      return serialized.ToString();
    }
  }
}