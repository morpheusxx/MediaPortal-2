#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using System.IO;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;

namespace MediaItemAspectModelBuilder
{
  class Program
  {
    static void Main(string[] args)
    {
      const string classNamespace = "MediaPortal.UiComponents.Media.Models.AspectWrappers";
      const bool createAsControl = true;
      const bool exposeNullables = true;

      List<Type> typeList = new List<Type> { typeof(MediaAspect), typeof(VideoAspect), typeof(AudioAspect), typeof(ImageAspect), typeof(MovieAspect), typeof(EpisodeAspect) };

      foreach (Type aspectType in typeList)
      {
        AspectModelBuilder amb = new AspectModelBuilder();
        string source = amb.BuildCodeTemplate(aspectType, classNamespace, createAsControl, exposeNullables);
        string targetFileName = string.Format("{0}Wrapper.cs", aspectType.Name);
        string targetPath = @"..\..\..\..\..\Source\UI\UiComponents\Media\Models\AspectWrappers\" + targetFileName;
        using (FileStream file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
        using(StreamWriter sw = new StreamWriter(file))
          sw.Write(source);
      }
    }
  }
}
