#region Copyright (C) 2007-2021 Team MediaPortal

/*
    Copyright (C) 2007-2021 Team MediaPortal
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

// The XAML parser of the SkinEngine uses the markup attributes of WPF to describe the object model.
// They are not available outside of Windows, so the used attributes are defined here, in the same namespace.
namespace System.Windows.Markup
{
  /// <summary>
  /// Maps an XML namespace to a CLR namespace.
  /// </summary>
  [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
  public sealed class XmlnsDefinitionAttribute : Attribute
  {
    public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace)
    {
      XmlNamespace = xmlNamespace;
      ClrNamespace = clrNamespace;
    }

    public string XmlNamespace { get; private set; }

    public string ClrNamespace { get; private set; }

    public string AssemblyName { get; set; }
  }

  /// <summary>
  /// Base class of markup extensions.
  /// </summary>
  public abstract class MarkupExtension
  {
    public abstract object ProvideValue(IServiceProvider serviceProvider);
  }

  /// <summary>
  /// Specifies the property which receives the content of an element.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
  public sealed class ContentPropertyAttribute : Attribute
  {
    public ContentPropertyAttribute()
    { }

    public ContentPropertyAttribute(string name)
    {
      Name = name;
    }

    public string Name { get; private set; }
  }
}
