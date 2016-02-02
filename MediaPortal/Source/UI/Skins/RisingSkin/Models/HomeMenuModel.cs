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
using System.Windows.Forms;
using MediaPortal.Common;
using MediaPortal.Common.Commands;
using MediaPortal.Common.Messaging;
using MediaPortal.UiComponents.SkinBase.General;
using MediaPortal.UiComponents.SkinBase.Models;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.Presentation.Workflow;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.Utilities;
using MediaPortal.Utilities.Events;

namespace MediaPortal.UiComponents.RisingSkin.Models
{
  public class HomeMenuModel: MenuModel
  {
    private readonly DelayedEvent _delayedMenueUpdateEvent;
    public NavigationList<ListItem> NestedMenuItems { get; private set; }
    public ItemsList SubItems { get; private set; }

    public void MoveNext()
    {
      NestedMenuItems.MoveNext();
    }

    public void MovePrevious()
    {
      NestedMenuItems.MovePrevious();
    }

    public void SetSelectedItem(object sender, SelectionChangedEventArgs e)
    {
      var item = e.FirstAddedItem as NestedItem;
      SetSubItems(item);
    }

    private void SetSubItems(ListItem item)
    {
      if (item != null)
      {
        SubItems.Clear();
        // Add self reference
        SubItems.Add(item);
        object oAction;
        if (item.AdditionalProperties.TryGetValue(Consts.KEY_ITEM_ACTION, out oAction))
        {
          PushNavigationTransition wfAction = oAction as PushNavigationTransition;
          if (wfAction != null)
          {
            var wf = ServiceRegistration.Get<IWorkflowManager>();
            foreach (var workflowAction in wf.MenuStateActions.Values)
            {
              if (workflowAction.SourceStateIds != null && workflowAction.SourceStateIds.Contains(wfAction.TargetStateId))
              {
                var action = workflowAction;
                var listItem = new ListItem(Consts.KEY_NAME, action.DisplayTitle);
                listItem.AdditionalProperties[Consts.KEY_ITEM_ACTION] = action;
                listItem.Command = new MethodDelegateCommand(() => action.Execute());
                SubItems.Add(listItem);
              }
            }
          }
        }
        SubItems.FireChange();
      }
    }

    public void OnKeyPress(object sender, KeyPressEventArgs e)
    {
      
    }

    public HomeMenuModel()
    {
      NestedMenuItems = new NavigationList<ListItem>();
      SubItems = new ItemsList();

      SubscribeToMessages();

      _delayedMenueUpdateEvent = new DelayedEvent(200); // Update menu items only if no more requests are following after 200 ms
      _delayedMenueUpdateEvent.OnEventHandler += ReCreateMenuItems;

      NestedMenuItems.OnCurrentChanged += SetSelection;
    }

    private void ReCreateMenuItems(object sender, EventArgs e)
    {
      var previousSelected = NestedMenuItems.Current;
      NestedMenuItems.Clear();
      CollectionUtils.AddAll(NestedMenuItems, MenuItems);
      NestedMenuItems.MoveTo(item => item == previousSelected);
      NestedMenuItems.FireChange();
    }

    private void SetSelection(int oldindex, int newindex)
    {
      foreach (var nestedItem in NestedMenuItems)
      {
        nestedItem.Selected = nestedItem == NestedMenuItems.Current;
      }
      SetSubItems(NestedMenuItems.Current);
    }


    private void SubscribeToMessages()
    {
      if (_messageQueue == null)
        return;
      _messageQueue.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      //if (message.ChannelName == MenuModelMessaging.CHANNEL)
      //{
      //  if (((MenuModelMessaging.MessageType)message.MessageType) == MenuModelMessaging.MessageType.UpdateMenu)
        {
          UpdateMenu();
        }
      //}
    }

    private void UpdateMenu()
    {
      _delayedMenueUpdateEvent.EnqueueEvent(this, EventArgs.Empty);
    }
  }
}
