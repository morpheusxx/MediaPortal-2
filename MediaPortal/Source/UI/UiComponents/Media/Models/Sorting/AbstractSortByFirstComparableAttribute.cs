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
using System.Linq;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Utilities;

namespace MediaPortal.UiComponents.Media.Models.Sorting
{
  public abstract class AbstractSortByFirstComparableAttribute<T> : SortByTitle where T : IComparable<T>
  {
    protected SingleMediaItemAspectMetadata.SingleAttributeSpecification _attr;

    protected AbstractSortByFirstComparableAttribute(SingleMediaItemAspectMetadata.SingleAttributeSpecification attr)
    {
      _attr = attr;
    }

    public override int Compare(MediaItem x, MediaItem y)
    {
      SingleMediaItemAspect aspectX;
      SingleMediaItemAspect aspectY;
      SingleMediaItemAspectMetadata metadata = _attr.ParentMIAM;
      if (MediaItemAspect.TryGetAspect(x.Aspects, metadata, out aspectX) && MediaItemAspect.TryGetAspect(y.Aspects, metadata, out aspectY))
      {
        List<string> valuesX = new List<string>(aspectX.GetCollectionAttribute<string>(_attr));
        valuesX.Sort();
        string firstValueX = valuesX.FirstOrDefault();
        List<string> valuesY = new List<string>(aspectY.GetCollectionAttribute<string>(_attr));
        valuesY.Sort();
        string firstValueY = valuesY.FirstOrDefault();
        return ObjectUtils.Compare(firstValueX, firstValueY);
      }
      return base.Compare(x, y);
    }
  }
}
