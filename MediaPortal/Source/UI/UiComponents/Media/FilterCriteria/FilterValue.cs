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

using MediaPortal.Common.MediaManagement.MLQueries;

namespace MediaPortal.UiComponents.Media.FilterCriteria
{
  public class FilterValue
  {
    protected string _title;
    protected IFilter _filter;
    protected IFilter _selectAttributeFilter;
    protected int? _numItems = null;
    protected MLFilterCriterion _criterion;

    public FilterValue(string title, IFilter filter, IFilter selectAttributeFilter, MLFilterCriterion criterion)
    {
      _title = title;
      _filter = filter;
      _selectAttributeFilter = selectAttributeFilter;
      _criterion = criterion;
    }

    public FilterValue(string title, IFilter filter, IFilter selectAttributeFilter, int numItems, MLFilterCriterion criterion)
    {
      _title = title;
      _filter = filter;
      _selectAttributeFilter = selectAttributeFilter;
      _numItems = numItems;
      _criterion = criterion;
    }

    public string Title
    {
      get { return _title; }
    }

    public int? NumItems
    {
      get { return _numItems; }
    }

    public MLFilterCriterion Criterion
    {
      get { return _criterion; }
    }

    public IFilter Filter
    {
      get { return _filter; }
    }

    public IFilter SelectAttributeFilter
    {
      get { return _selectAttributeFilter; }
    }
  }
}
