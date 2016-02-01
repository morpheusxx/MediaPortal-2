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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Forms;
using MediaPortal.Common.General;
using MediaPortal.UiComponents.SkinBase.General;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.Utilities;

namespace MediaPortal.UiComponents.RisingSkin.Models
{
  public class NestedItem : ListItem
  {
    public NestedItem(string name, string value):
      base(name, value)
    {
      SubItems = new ItemsList();
    }
    public NestedItem()
    {
      SubItems = new ItemsList();
    }
    public ItemsList SubItems { get; private set; }
  }


  /// <summary>
  /// <see cref="NavigationList{T}"/> provides navigation features for moving inside a <see cref="List{T}"/> and exposing <see cref="Current"/> item.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class NavigationList<T> : List<T>, IObservable
  {
    public delegate void CurrentChangedEvent(int oldIndex, int newIndex);
    public CurrentChangedEvent OnCurrentChanged;
    public EventHandler OnListChanged;
    protected WeakEventMulticastDelegate _objectChanged = new WeakEventMulticastDelegate();

    /// <summary>
    /// Event which gets fired when the collection changes.
    /// </summary>
    public event ObjectChangedDlgt ObjectChanged
    {
      add { _objectChanged.Attach(value); }
      remove { _objectChanged.Detach(value); }
    }

    public void FireChange()
    {
      _objectChanged.Fire(new object[] { this });
    }

    private int _current;

    public T Current
    {
      get { return Count > 0 && _current < Count ? this[_current] : default(T); }
    }

    public int CurrentIndex
    {
      get { return Count > 0 ? _current : -1; }
      set
      {
        if (Count == 0 || value < 0 || value >= Count)
          return;
        int oldIndex = CurrentIndex;
        _current = value;
        FireCurrentChanged(oldIndex);
      }
    }

    public void MoveNext()
    {
      if (Count == 0)
        return;
      int oldIndex = CurrentIndex;
      _current++;
      if (_current >= Count)
        _current = 0;
      FireCurrentChanged(oldIndex);
    }

    public void MovePrevious()
    {
      if (Count == 0)
        return;
      int oldIndex = CurrentIndex;
      _current--;
      if (_current < 0)
        _current = Count - 1;
      FireCurrentChanged(oldIndex);
    }

    public void SetIndex(int index)
    {
      if (Count == 0 || index < 0 || index >= Count)
        return;
      int oldIndex = CurrentIndex;
      _current = index;
      FireCurrentChanged(oldIndex);
    }

    public bool MoveTo(Predicate<T> condition)
    {
      int oldIndex = CurrentIndex;
      for (int index = 0; index < Count; index++)
      {
        T item = this[index];
        if (!condition.Invoke(item))
          continue;
        _current = index;
        return true;
      }
      FireCurrentChanged(oldIndex);
      return false;
    }

    public void FireCurrentChanged(int oldIndex)
    {
      var currentIndex = CurrentIndex;
      if (OnCurrentChanged != null && oldIndex != currentIndex)
        OnCurrentChanged(oldIndex, currentIndex);
    }

    public void FireListChanged()
    {
      if (OnListChanged != null)
        OnListChanged(this, EventArgs.Empty);
    }
  }

  public class HomeMenuModel
  {
    public NavigationList<NestedItem> MenuItems { get; private set; }
    public ItemsList SubItems { get; private set; }

    public void MoveNext()
    {
      MenuItems.MoveNext();
    }

    public void MovePrevious()
    {
      MenuItems.MovePrevious();
    }

    public void SetSelectedItem(object sender, SelectionChangedEventArgs e)
    {
      var item = e.FirstAddedItem as NestedItem;
      SetSubItems(item);
    }

    private void SetSubItems(NestedItem item)
    {
      if (item != null)
      {
        SubItems.Clear();
        CollectionUtils.AddAll(SubItems, item.SubItems);
        SubItems.FireChange();
      }
    }

    public void OnKeyPress(object sender, KeyPressEventArgs e)
    {
      
    }

    public HomeMenuModel()
    {
      MenuItems = new NavigationList<NestedItem>();
      SubItems = new ItemsList();
      for (int i = 0; i < 10; i++)
      {
        NestedItem item = new NestedItem(Consts.KEY_NAME, "Main " + i);
        for (int j = 0; j < new Random(i).Next(5); j++)
        {
          var subItem = new ListItem(Consts.KEY_NAME, "Sub " + j);
          item.SubItems.Add(subItem);
        }
        MenuItems.Add(item);
      }
      SetSubItems(MenuItems.Current);
      MenuItems.FireChange();
      SetSelection(-1, 0);
      MenuItems.OnCurrentChanged += SetSelection;
    }

    private void SetSelection(int oldindex, int newindex)
    {
      foreach (var nestedItem in MenuItems)
      {
        nestedItem.Selected = nestedItem == MenuItems.Current;
      }
      SetSubItems(MenuItems.Current);
    }
  }
}
