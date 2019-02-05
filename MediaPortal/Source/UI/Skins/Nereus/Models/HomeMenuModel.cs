﻿#region Copyright (C) 2007-2018 Team MediaPortal

/*
    Copyright (C) 2007-2018 Team MediaPortal
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

using MediaPortal.Common;
using MediaPortal.Common.General;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.Runtime;
using MediaPortal.Extensions.UserServices.FanArtService.Client.Models;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.Presentation.Models;
using MediaPortal.UI.Presentation.Screens;
using MediaPortal.UI.Presentation.Workflow;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.UiComponents.Media.Models;
using MediaPortal.UiComponents.SkinBase.General;
using MediaPortal.Utilities.Events;
using System;
using System.Windows.Forms;

namespace MediaPortal.UiComponents.Nereus.Models
{
  public class HomeMenuModel : BaseMessageControlledModel
  {
    protected class ActionEventArgs : EventArgs
    {
      public WorkflowAction Action { get; set; }
    }

    public static readonly Guid MODEL_ID = new Guid("CED34107-565C-48D9-BEC8-195F7969F90F");
    public static readonly Guid HOME_STATE_ID = new Guid("7F702D9C-F2DD-42da-9ED8-0BA92F07787F");

    protected const int UPDATE_DELAY_MS = 500;

    protected readonly object _syncObj = new object();

    protected AbstractProperty _content0ActionIdProperty;
    protected AbstractProperty _content1ActionIdProperty;
    protected AbstractProperty _contentIndexProperty;
    protected AbstractProperty _selectedItemProperty;

    protected bool _isInit = false;
    protected DelayedEvent _updateEvent;

    public HomeMenuModel()
    {
      _content0ActionIdProperty = new WProperty(typeof(string), null);
      _content1ActionIdProperty = new WProperty(typeof(string), null);
      _contentIndexProperty = new WProperty(typeof(int), 0);
      _selectedItemProperty = new WProperty(typeof(ListItem), null);

      _updateEvent = new DelayedEvent(UPDATE_DELAY_MS);
      _updateEvent.OnEventHandler += OnUpdate;
      _selectedItemProperty.Attach(OnSelectedItemChanged);

      SubscribeToMessages();
    }

    void InitDefaultLists()
    {
      var mlm = GetMediaListModel();
      mlm.Limit = 6;
      var list = mlm.Lists["LatestAudio"].AllItems;
      list = mlm.Lists["ContinuePlayAlbum"].AllItems;
      list = mlm.Lists["FavoriteAudio"].AllItems;
      list = mlm.Lists["UnplayedAlbums"].AllItems;

      list = mlm.Lists["LatestMovies"].AllItems;
      list = mlm.Lists["ContinuePlayMovies"].AllItems;
      list = mlm.Lists["FavoriteMovies"].AllItems;
      list = mlm.Lists["UnplayedMovies"].AllItems;

      list = mlm.Lists["LatestEpisodes"].AllItems;
      list = mlm.Lists["ContinuePlaySeries"].AllItems;
      list = mlm.Lists["FavoriteSeries"].AllItems;
      list = mlm.Lists["UnplayedSeries"].AllItems;

      list = mlm.Lists["LatestImages"].AllItems;
      list = mlm.Lists["FavoriteImages"].AllItems;
      list = mlm.Lists["UnplayedImages"].AllItems;

      list = mlm.Lists["LastPlayTV"].AllItems;
      list = mlm.Lists["FavoriteTV"].AllItems;
      list = mlm.Lists["CurrentPrograms"].AllItems;
      list = mlm.Lists["CurrentSchedules"].AllItems;
    }

    #region Message Handling

    private void SubscribeToMessages()
    {
      if (_messageQueue == null)
        return;
      _messageQueue.SubscribeToMessageChannel(SystemMessaging.CHANNEL);
      _messageQueue.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (_isInit)
        return;

      if (message.ChannelName == WorkflowManagerMessaging.CHANNEL)
      {
        WorkflowManagerMessaging.MessageType messageType = (WorkflowManagerMessaging.MessageType)message.MessageType;
        if (messageType == WorkflowManagerMessaging.MessageType.NavigationComplete)
        {
          var context = ServiceRegistration.Get<IWorkflowManager>().CurrentNavigationContext;
          if (context != null && context.WorkflowState.StateId == HOME_STATE_ID)
          {
            _isInit = true;
            InitDefaultLists();
          }
        }
      }
    }

    #endregion

    #region Members to be accessed from the GUI

    public AbstractProperty Content0ActionIdProperty
    {
      get { return _content0ActionIdProperty; }
    }

    public string Content0ActionId
    {
      get { return (string)_content0ActionIdProperty.GetValue(); }
      set { _content0ActionIdProperty.SetValue(value); }
    }

    public AbstractProperty Content1ActionIdProperty
    {
      get { return _content1ActionIdProperty; }
    }

    public string Content1ActionId
    {
      get { return (string)_content1ActionIdProperty.GetValue(); }
      set { _content1ActionIdProperty.SetValue(value); }
    }

    public AbstractProperty ContentIndexProperty
    {
      get { return _contentIndexProperty; }
    }

    public int ContentIndex
    {
      get { return (int)_contentIndexProperty.GetValue(); }
      protected set { _contentIndexProperty.SetValue(value); }
    }

    public AbstractProperty SelectedItemProperty
    {
      get { return _selectedItemProperty; }
    }

    public ListItem SelectedItem
    {
      get { return (ListItem)_selectedItemProperty.GetValue(); }
      set { _selectedItemProperty.SetValue(value); }
    }

    public void SetSelectedItem(object sender, SelectionChangedEventArgs e)
    {
      ListItem item = e.FirstAddedItem as ListItem;
      if (item != null)
        SelectedItem = item;
    }

    public void SetSelectedHomeTile(object item)
    {
      UpdateSelectedFanArtItem(item as ListItem);
    }

    public void ClearSelectedHomeTile()
    {
      UpdateSelectedFanArtItem(null);
    }

    public void CloseTopmostDialog(MouseButtons buttons, float x, float y)
    {
      ServiceRegistration.Get<IScreenManager>().CloseTopmostDialog();
    }

    #endregion

    private void OnSelectedItemChanged(AbstractProperty property, object oldValue)
    {
      ListItem item = SelectedItem;
      if (item == null)
        return;
      WorkflowAction action;
      if (!TryGetAction(item, out action))
        action = null;
      EnqueueUpdate(action);
    }

    private void EnqueueUpdate(WorkflowAction action)
    {
      _updateEvent.EnqueueEvent(this, new ActionEventArgs { Action = action });
    }

    private void OnUpdate(object sender, EventArgs e)
    {
      UpdateContent(((ActionEventArgs)e).Action);
    }

    protected void UpdateContent(WorkflowAction action)
    {
      string nextContentActionId = action?.ActionId.ToString();
      AbstractProperty nextContentActionIdProperty;
      int nextContentIndex;
      int currentContentIndex = ContentIndex;
      if (currentContentIndex == 0)
      {
        if (Content0ActionId == nextContentActionId)
          return;
        nextContentIndex = 1;
        nextContentActionIdProperty = _content1ActionIdProperty;
      }
      else
      {
        if (Content1ActionId == nextContentActionId)
          return;
        nextContentIndex = 0;
        nextContentActionIdProperty = _content0ActionIdProperty;
      }
      nextContentActionIdProperty.SetValue(nextContentActionId);
      ContentIndex = nextContentIndex;
    }

    protected void UpdateSelectedFanArtItem(ListItem item)
    {
      //if (item == null)
        //return;
      var fm = GetFanArtBackgroundModel();
      fm.SelectedItem = item;
    }

    protected static FanArtBackgroundModel GetFanArtBackgroundModel()
    {
      return (FanArtBackgroundModel)ServiceRegistration.Get<IWorkflowManager>().GetModel(FanArtBackgroundModel.FANART_MODEL_ID);
    }

    protected static MediaListModel GetMediaListModel()
    {
      return (MediaListModel)ServiceRegistration.Get<IWorkflowManager>().GetModel(MediaListModel.MEDIA_LIST_MODEL_ID);
    }

    protected static bool TryGetAction(ListItem item, out WorkflowAction action)
    {
      action = item.AdditionalProperties[Consts.KEY_ITEM_ACTION] as WorkflowAction;
      return action != null;
    }
  }
}
