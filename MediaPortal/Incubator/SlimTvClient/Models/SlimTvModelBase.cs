#region Copyright (C) 2007-2013 Team MediaPortal

/*
    Copyright (C) 2007-2013 Team MediaPortal
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
using MediaPortal.Common;
using MediaPortal.Common.Commands;
using MediaPortal.Plugins.SlimTv.Client.Messaging;
using MediaPortal.Plugins.SlimTv.Interfaces;
using MediaPortal.Plugins.SlimTv.Interfaces.Items;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.Presentation.Models;
using MediaPortal.UI.Presentation.Screens;
using MediaPortal.UI.Presentation.Workflow;

namespace MediaPortal.Plugins.SlimTv.Client.Models
{
  /// <summary>
  /// <see cref="SlimTvModelBase"/> provides basic features for all derived models, i.e. channel group and channel selection.
  /// </summary>
  public abstract class SlimTvModelBase : BaseTimerControlledModel, IWorkflowModel
  {
    #region Protected fields

    protected ITvHandler _tvHandler;
    protected ItemsList _channelGroupList;
    protected IList<IChannelGroup> _channelGroups;
    protected IList<IChannel> _channels;
    protected int _webChannelGroupIndex;
    protected int _webChannelIndex;

    protected IList<IProgram> _programs;
    protected bool _isInitialized;

    #endregion

    #region Constructor

    protected SlimTvModelBase()
      : this(5000)
    { }

    protected SlimTvModelBase(long updateInterval)
      : base(true, updateInterval)
    { }

    #endregion

    #region GUI properties and methods

    /// <summary>
    /// Gets the list of all available channel groups.
    /// </summary>
    public ItemsList ChannelGroupList
    {
      get { return _channelGroupList; }
    }

    /// <summary>
    /// Skips group index to next one.
    /// </summary>
    public void NextGroup()
    {
      SetGroup(++_webChannelGroupIndex);
    }

    /// <summary>
    /// Skips group index to previous one.
    /// </summary>
    public void PrevGroup()
    {
      SetGroup(--_webChannelGroupIndex);
    }

    /// <summary>
    /// Sets the current channel group and updates the channel list.
    /// </summary>
    /// <param name="newIndex">Index of group to select.</param>
    public void SetGroup(int newIndex)
    {
      if (_channelGroups == null)
        return;

      if (newIndex >= _channelGroups.Count)
        newIndex = 0;

      if (newIndex < 0)
        newIndex = _channelGroups.Count - 1;

      _webChannelGroupIndex = newIndex;
      FillChannelGroupList();
      UpdateChannels();
      SlimTvClientMessaging.SendSlimTvClientMessage(SlimTvClientMessaging.MessageType.GroupChanged);
    }

    /// <summary>
    /// Opens the group selection dialog.
    /// </summary>
    public void SelectGroup()
    {
      ServiceRegistration.Get<IScreenManager>().ShowDialog("DialogChooseGroup");
    }

    /// <summary>
    /// Skips group index to next one.
    /// </summary>
    public void NextChannel()
    {
      if (_channels == null)
        return;

      _webChannelIndex++;
      SetChannel(_webChannelIndex);
    }

    /// <summary>
    /// Skips group index to previous one.
    /// </summary>
    public void PrevChannel()
    {
      if (_channelGroups == null)
        return;

      _webChannelIndex--;
      SetChannel(_webChannelIndex);
    }

    /// <summary>
    /// Sets the current channel based on the given <paramref name="webChannelIndex"/>.
    /// </summary>
    /// <param name="webChannelIndex">New channel index.</param>
    protected virtual void SetChannel(int webChannelIndex)
    {
      if (webChannelIndex < 0)
        webChannelIndex = _channels.Count - 1;

      if (webChannelIndex >= _channels.Count)
        webChannelIndex = 0;

      _webChannelIndex = webChannelIndex;

      UpdateCurrentChannel();
      UpdatePrograms();
    }


    public void ExecProgramAction(ListItem item)
    {
      if (item == null)
        return;
      if (item.Command != null)
        item.Command.Execute();
    }

    #endregion

    #region Members

    #region Inits and Updates

    protected virtual void InitModel()
    {
      if (_tvHandler == null)
      {
        ITvHandler tvHandler = ServiceRegistration.Get<ITvHandler>();
        tvHandler.Initialize();
        if (tvHandler.ChannelAndGroupInfo == null)
          return;
        _tvHandler = tvHandler;
      }
      _tvHandler.ChannelAndGroupInfo.GetChannelGroups(out _channelGroups);

      GetCurrentChannelGroup();
      FillChannelGroupList();
      GetCurrentChannel();
      UpdateChannels();
      UpdatePrograms();
    }

    protected void FillChannelGroupList()
    {
      if (_channelGroups == null)
        return;

      _channelGroupList = new ItemsList();
      for (int idx = 0; idx < _channelGroups.Count; idx++)
      {
        IChannelGroup group = _channelGroups[idx];
        int selIdx = idx;
        ListItem channelGroupItem = new ListItem(UiComponents.Media.General.Consts.KEY_NAME, group.Name)
        {
          Command = new MethodDelegateCommand(() => SetGroup(selIdx)),
          Selected = selIdx == _webChannelGroupIndex
        };
        _channelGroupList.Add(channelGroupItem);
      }
      _channelGroupList.FireChange();
    }

    protected void GetCurrentChannelGroup()
    {
      _webChannelGroupIndex = 0;
      if (_channelGroups != null && _tvHandler.ChannelAndGroupInfo != null && _tvHandler.ChannelAndGroupInfo.SelectedChannelGroupId != 0)
        for (int idx = 0; idx < _channelGroups.Count; idx++)
          if (_channelGroups[idx].ChannelGroupId == _tvHandler.ChannelAndGroupInfo.SelectedChannelGroupId)
          {
            _webChannelGroupIndex = idx;
            break;
          }
    }

    protected void GetCurrentChannel()
    {
      _webChannelIndex = 0;
      if (_channels != null && _tvHandler.ChannelAndGroupInfo != null && _tvHandler.ChannelAndGroupInfo.SelectedChannelId != 0)
        for (int idx = 0; idx < _channels.Count; idx++)
          if (_channels[idx].ChannelId == _tvHandler.ChannelAndGroupInfo.SelectedChannelId)
          {
            _webChannelIndex = idx;
            break;
          }
    }

    protected void SetCurrentChannelGroup()
    {
      if (_tvHandler.ChannelAndGroupInfo != null)
        _tvHandler.ChannelAndGroupInfo.SelectedChannelGroupId = _channelGroups[_webChannelGroupIndex].ChannelGroupId;
    }

    protected void SetCurrentChannel()
    {
      if (_tvHandler.ChannelAndGroupInfo != null)
        _tvHandler.ChannelAndGroupInfo.SelectedChannelId = _channels[_webChannelIndex].ChannelId;
    }

    #endregion

    #region Channel, groups and programs

    protected abstract void UpdateCurrentChannel();
    protected abstract void UpdatePrograms();

    protected virtual void UpdateChannels()
    {
      if (_webChannelGroupIndex < _channelGroups.Count)
      {
        IChannelGroup group = _channelGroups[_webChannelGroupIndex];
        _tvHandler.ChannelAndGroupInfo.GetChannels(group, out _channels);

        _webChannelIndex = 0;
        SetCurrentChannelGroup();
        UpdateCurrentChannel();
        UpdatePrograms();
      }
    }

    #endregion

    #endregion

    #region IWorkflowModel implementation

    public abstract Guid ModelId { get; }

    public virtual bool CanEnterState(NavigationContext oldContext, NavigationContext newContext)
    {
      InitModel();
      return _tvHandler != null;
    }

    public virtual void EnterModelContext(NavigationContext oldContext, NavigationContext newContext)
    {
    }

    public virtual void ExitModelContext(NavigationContext oldContext, NavigationContext newContext)
    {
    }

    public virtual void ChangeModelContext(NavigationContext oldContext, NavigationContext newContext, bool push)
    {
    }

    public virtual void Deactivate(NavigationContext oldContext, NavigationContext newContext)
    {
    }

    public virtual void Reactivate(NavigationContext oldContext, NavigationContext newContext)
    {
    }

    public virtual void UpdateMenuActions(NavigationContext context, IDictionary<Guid, WorkflowAction> actions)
    {
    }

    public ScreenUpdateMode UpdateScreen(NavigationContext context, ref string screen)
    {
      return ScreenUpdateMode.AutoWorkflowManager;
    }

    #endregion
  }
}