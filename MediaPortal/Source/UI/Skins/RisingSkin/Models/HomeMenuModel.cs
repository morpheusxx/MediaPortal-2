﻿#region Copyright (C) 2007-2014 Team MediaPortal

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
using MediaPortal.UiComponents.SkinBase.General;
using MediaPortal.UI.Presentation.DataObjects;

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

  public class HomeMenuModel
  {
    public ItemsList MenuItems { get; private set; }

    public HomeMenuModel()
    {
      MenuItems = new ItemsList();
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
      MenuItems.FireChange();
    }
  }
}
