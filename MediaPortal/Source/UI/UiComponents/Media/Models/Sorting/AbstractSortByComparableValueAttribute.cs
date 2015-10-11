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
using MediaPortal.Common.MediaManagement;
using MediaPortal.Utilities;

namespace MediaPortal.UiComponents.Media.Models.Sorting
{
  public abstract class AbstractSortByComparableValueAttribute<T> : Sorting where T : struct , IComparable<T>
  {
    protected string _displayName;
    protected MediaItemAspectMetadata.AttributeSpecification _sortAttr;

    protected AbstractSortByComparableValueAttribute(string displayName, MediaItemAspectMetadata.AttributeSpecification sortAttr)
    {
      _displayName = displayName;
      _sortAttr = sortAttr;
    }

    public override string DisplayName
    {
      get { return _displayName; }
    }

    public override int Compare(MediaItem x, MediaItem y)
    {
      MediaItemAspect aspectX;
      MediaItemAspect aspectY;
      MediaItemAspectMetadata metadata = _sortAttr.ParentMIAM;
      if (MediaItemAspect.TryGetAspect(x.Aspects, metadata, out aspectX) && MediaItemAspect.TryGetAspect(y.Aspects, metadata, out aspectY))
      {
        T? valX = (T?) aspectX.GetAttributeValue(_sortAttr);
        T? valY = (T?) aspectY.GetAttributeValue(_sortAttr);
        return ObjectUtils.Compare(valX, valY);
      }
      return 0;
    }
  }
}
