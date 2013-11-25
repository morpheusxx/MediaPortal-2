﻿#region Copyright (C) 2007-2013 Team MediaPortal

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
// #define DEBUG_LAYOUT

using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Common;
using MediaPortal.Common.General;
#if DEBUG_LAYOUT
using MediaPortal.Common.Logging;
#endif
using MediaPortal.Common.Messaging;
using MediaPortal.Plugins.SlimTv.Client.Helpers;
using MediaPortal.Plugins.SlimTv.Client.Messaging;
using MediaPortal.Plugins.SlimTv.Client.Models;
using MediaPortal.Plugins.SlimTv.Interfaces.Items;
using MediaPortal.UI.Control.InputManager;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.Presentation.Workflow;
using MediaPortal.UI.SkinEngine.Controls.Panels;
using MediaPortal.UI.SkinEngine.Controls.Visuals;
using MediaPortal.UI.SkinEngine.Controls.Visuals.Templates;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.UI.SkinEngine.ScreenManagement;
using MediaPortal.Utilities.DeepCopy;

namespace MediaPortal.Plugins.SlimTv.Client.Controls
{
  public class EpgGrid : Grid
  {
    #region Fields

    protected static double _visibleHours = SlimTvMultiChannelGuideModel.VisibleHours;
    protected static int _numberOfColumns = 75; // Used to align programs in Grid. For example: 2.5h == 150 min. 150 min / 75 = 2 min per column.
    protected int _numberOfRows = 6; // TODO: property
    protected double _perCellTime = _visibleHours * 60 / _numberOfColumns; // Time in minutes per grid cell.

    protected AsynchronousMessageQueue _messageQueue = null;
    protected AbstractProperty _headerWidthProperty;
    protected AbstractProperty _programTemplateProperty;
    protected AbstractProperty _headerTemplateProperty;
    protected bool _childrenCreated = false;
    protected int _channelViewOffset;
    protected double _actualWidth = 0.0d;
    protected double _actualHeight = 0.0d;
    protected int _groupIndex = -1;
    protected readonly object _syncObj = new object();

    #endregion

    #region Constructor / Dispose

    public EpgGrid()
    {
      _headerWidthProperty = new SProperty(typeof(Double), 200d);
      _programTemplateProperty = new SProperty(typeof(ControlTemplate), null);
      _headerTemplateProperty = new SProperty(typeof(ControlTemplate), null);
      Attach();
      SubscribeToMessages();
    }

    private void Attach()
    {
    }

    private void Detach()
    {
    }

    void SubscribeToMessages()
    {
      AsynchronousMessageQueue messageQueue;
      lock (_syncObj)
      {
        if (_messageQueue != null)
          return;
        _messageQueue = new AsynchronousMessageQueue(this, new[] { SlimTvClientMessaging.CHANNEL });
        _messageQueue.MessageReceived += OnMessageReceived;
        messageQueue = _messageQueue;
      }
      messageQueue.Start();
    }

    void UnsubscribeFromMessages()
    {
      AsynchronousMessageQueue messageQueue;
      lock (_syncObj)
      {
        if (_messageQueue == null)
          return;
        messageQueue = _messageQueue;
        _messageQueue = null;
      }
      messageQueue.Shutdown();
    }

    protected void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      // Do not handle messages if control is not running. This is a workaround to avoid updating controls that are not used on screen.
      // The EpgGrid is instantiated twice: via ScreenManager.LoadScreen and Control.OnTemplateChanged as copy!?
      if (ElementState != ElementState.Running)
        return;

      if (message.ChannelName == SlimTvClientMessaging.CHANNEL)
      {
        SlimTvClientMessaging.MessageType messageType = (SlimTvClientMessaging.MessageType)message.MessageType;
        switch (messageType)
        {
          case SlimTvClientMessaging.MessageType.GroupChanged:
            OnGroupChanged();
            break;
          case SlimTvClientMessaging.MessageType.ProgramsChanged:
            OnProgramsChanged();
            break;
        }
      }
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();

      base.DeepCopy(source, copyManager);

      EpgGrid c = (EpgGrid)source;
      HeaderTemplate = copyManager.GetCopy(c.HeaderTemplate);
      ProgramTemplate = copyManager.GetCopy(c.ProgramTemplate);

      Attach();
    }

    public override void Dispose()
    {
      Detach();
      UnsubscribeFromMessages();
      MPF.TryCleanupAndDispose(HeaderTemplate);
      MPF.TryCleanupAndDispose(ProgramTemplate);
      base.Dispose();
    }

    private void OnGroupChanged()
    {
      // Group changed, recreate all.
      RecreateAndArrangeChildren();
      ArrangeOverride();
    }

    private void OnProgramsChanged()
    {
      // Programs changed, only update.
      CreateVisibleChildren(true);
    }

    #endregion

    #region Public Properties

    public ItemsList ChannelsPrograms
    {
      get
      {
        return SlimTvMultiChannelGuideModel.ChannelList;
      }
    }

    public AbstractProperty ProgramTemplateProperty
    {
      get { return _programTemplateProperty; }
    }

    public ControlTemplate ProgramTemplate
    {
      get { return (ControlTemplate)_programTemplateProperty.GetValue(); }
      set { _programTemplateProperty.SetValue(value); }
    }

    public AbstractProperty HeaderTemplateProperty
    {
      get { return _headerTemplateProperty; }
    }

    public ControlTemplate HeaderTemplate
    {
      get { return (ControlTemplate)_headerTemplateProperty.GetValue(); }
      set { _headerTemplateProperty.SetValue(value); }
    }

    public AbstractProperty HeaderWidthProperty
    {
      get { return _headerWidthProperty; }
    }

    public double HeaderWidth
    {
      get { return (double)_headerWidthProperty.GetValue(); }
      set { _headerWidthProperty.SetValue(value); }
    }

    #endregion

    #region Layout overrides

    protected override void ArrangeOverride()
    {
      PrepareColumnAndRowLayout();
      base.ArrangeOverride();
    }

    private void PrepareColumnAndRowLayout()
    {
      // Recreate columns and rows only after dimensions changed.
      if (_actualWidth == ActualWidth && _actualHeight == ActualHeight)
        return;
      _actualWidth = ActualWidth;
      _actualHeight = ActualHeight;

      ColumnDefinitions.Clear();
      RowDefinitions.Clear();

      double headerWidth = HeaderWidth;
      ColumnDefinition rowHeaderColumn = new ColumnDefinition { Width = new GridLength(GridUnitType.Pixel, headerWidth) };
      ColumnDefinitions.Add(rowHeaderColumn);

      double rowHeight = ActualHeight / _numberOfRows;
      double colWidth = (ActualWidth - headerWidth) / _numberOfColumns;
      for (int c = 0; c < _numberOfColumns; c++)
      {
        ColumnDefinition cd = new ColumnDefinition { Width = new GridLength(GridUnitType.Pixel, colWidth) };
        ColumnDefinitions.Add(cd);
      }
      for (int r = 0; r < _numberOfRows; r++)
      {
        RowDefinition cd = new RowDefinition { Height = new GridLength(GridUnitType.Pixel, rowHeight) };
        RowDefinitions.Add(cd);
      }

      RecreateAndArrangeChildren();
    }

    private void RecreateAndArrangeChildren(bool keepViewOffset = false)
    {
      if (!keepViewOffset)
        _channelViewOffset = 0;
      _childrenCreated = false;
      CreateVisibleChildren(false);
    }

    private void CreateVisibleChildren(bool updateOnly)
    {
      lock (Children.SyncRoot)
      {
        if (!updateOnly && _childrenCreated)
          return;

        _childrenCreated = true;

        if (!updateOnly)
          Children.Clear();

        if (ChannelsPrograms == null)
          return;

        int rowIndex = 0;
        int channelIndex = _channelViewOffset;
        while (channelIndex < ChannelsPrograms.Count && rowIndex < _numberOfRows)
        {
          if (!CreateOrUpdateRow(updateOnly, ref channelIndex, rowIndex++))
            break;
        }
      }
    }

    private bool CreateOrUpdateRow(bool updateOnly, ref int channelIndex, int rowIndex)
    {
      ChannelProgramListItem channel = ChannelsPrograms[channelIndex] as ChannelProgramListItem;
      if (channel == null)
        return false;

      // Default: take viewport from model
      DateTime viewportStart = SlimTvMultiChannelGuideModel.GuideStartTime;
      DateTime viewportEnd = SlimTvMultiChannelGuideModel.GuideEndTime;

      int colIndex = 0;
      if (!updateOnly)
      {
        Control btnHeader = CreateControl(channel);
        SetGrid(btnHeader, colIndex, rowIndex, 1);

        // Deep copy the styles to each program button.
        btnHeader.Template = MpfCopyManager.DeepCopyCutLVPs(HeaderTemplate);
        Children.Add(btnHeader);
      }

      int colSpan = 0;
      DateTime? lastEndTime = viewportStart;

#if DEBUG_LAYOUT
      // Debug layouting:
      if (rowIndex == 0)
        ServiceRegistration.Get<ILogger>().Debug("EPG: Viewport: {0}-{1} PerCell: {2} min", viewportStart.ToShortTimeString(), viewportEnd.ToShortTimeString(), _perCellTime);
#endif
      if (updateOnly)
      {
        // Remove all programs outside of viewport.
        DateTime start = viewportStart;
        DateTime end = viewportEnd;
        var removeList = GetRowItems(rowIndex).Where(el =>
        {
          ProgramListItem p = (ProgramListItem)el.Context;
          return p.Program.EndTime <= start || p.Program.StartTime >= end || channel.Channel.ChannelId != ((IProgram)p.AdditionalProperties["PROGRAM"]).ChannelId;
        }).ToList();
        removeList.ForEach(Children.Remove);
      }

      colIndex = 1; // After header
      int programIndex = 0;
      while (programIndex < channel.Programs.Count && colIndex < _numberOfColumns)
      {
        ProgramListItem program = channel.Programs[programIndex] as ProgramListItem;
        if (program == null || program.Program.StartTime > viewportEnd)
          break;

        if (program.Program.EndTime < viewportStart)
        {
          programIndex++;
          continue;
        }

        CalculateProgamPosition(program, viewportStart, viewportEnd, ref colIndex, ref colSpan, ref lastEndTime);

        Control btnEpg = GetOrCreateControl(program, rowIndex);
        SetGrid(btnEpg, colIndex, rowIndex, colSpan);

        programIndex++;
        colIndex += colSpan; // Skip spanned columns.
      }

      channelIndex++;
      return true;
    }

    /// <summary>
    /// Calculates to position (Column) and size (ColumnSpan) for the given <paramref name="program"/> by considering the avaiable viewport (<paramref name="viewportStart"/>, <paramref name="viewportEnd"/>).
    /// </summary>
    /// <param name="program">Program.</param>
    /// <param name="viewportStart">Viewport from.</param>
    /// <param name="viewportEnd">Viewport to.</param>
    /// <param name="colIndex">Returns Column.</param>
    /// <param name="colSpan">Returns ColumnSpan.</param>
    /// <param name="lastEndTime">Last program's end time.</param>
    private void CalculateProgamPosition(ProgramListItem program, DateTime viewportStart, DateTime viewportEnd, ref int colIndex, ref int colSpan, ref DateTime? lastEndTime)
    {
      if (program.Program.EndTime < viewportStart || program.Program.StartTime > viewportEnd)
        return;

      DateTime programViewStart = program.Program.StartTime < viewportStart ? viewportStart : program.Program.StartTime;

      double minutesSinceStart = (programViewStart - viewportStart).TotalMinutes;
      if (lastEndTime != null)
      {
        int newColIndex = (int)Math.Round(minutesSinceStart / _perCellTime) + 1; // Header offset
        if (lastEndTime != program.Program.StartTime)
          colIndex = Math.Max(colIndex, newColIndex); // colIndex is already set to new position. Calculation is only done to support gaps in programs.

        lastEndTime = program.Program.EndTime;
      }

      colSpan = (int)Math.Round((program.Program.EndTime - programViewStart).TotalMinutes / _perCellTime);

      if (colIndex + colSpan > _numberOfColumns + 1)
        colSpan = _numberOfColumns - colIndex + 1;

      if (colSpan == 0)
        colSpan = 1;

#if DEBUG_LAYOUT
        // Debug layouting:
      ServiceRegistration.Get<ILogger>().Debug("EPG: {0,2}-{1,2}: {3}-{4}: {2}", colIndex, colSpan, program.Program.Title, program.Program.StartTime.ToShortTimeString(), program.Program.EndTime.ToShortTimeString());
#endif
    }

    /// <summary>
    /// Tries to find an existing control for given <paramref name="program"/> in the Grid row with index <paramref name="rowIndex"/>.
    /// If no control was found, this method creates a new control and adds it to the Grid.
    /// </summary>
    /// <param name="program">Program.</param>
    /// <param name="rowIndex">RowIndex.</param>
    /// <returns>Control.</returns>
    private Control GetOrCreateControl(ProgramListItem program, int rowIndex)
    {
      Control control = GetRowItems(rowIndex).FirstOrDefault(el => ((ProgramListItem)el.Context).Program.ProgramId == program.Program.ProgramId);
      if (control != null)
        return control;

      control = CreateControl(program);
      // Deep copy the styles to each program button.
      control.Template = MpfCopyManager.DeepCopyCutLVPs(ProgramTemplate);
      Children.Add(control);
      return control;
    }

    /// <summary>
    /// Creates a control in the given <paramref name="context"/>.
    /// </summary>
    /// <param name="context">ListItem, can be either "Channel" or "Program"</param>
    /// <returns>Control.</returns>
    private Control CreateControl(ListItem context)
    {
      Control btnEpg = new Control
      {
        LogicalParent = this,
        Context = context,
      };
      return btnEpg;
    }

    /// <summary>
    /// Sets Grid positioning attached properties.
    /// </summary>
    /// <param name="gridControl">Control in Grid.</param>
    /// <param name="colIndex">"Grid.Column"</param>
    /// <param name="rowIndex">"Grid.Row"</param>
    /// <param name="colSpan">"Grid.ColumnSpan"</param>
    private static void SetGrid(Control gridControl, int colIndex, int rowIndex, int colSpan)
    {
      SetRow(gridControl, rowIndex);
      SetColumn(gridControl, colIndex);
      SetColumnSpan(gridControl, colSpan);
    }

    /// <summary>
    /// Returns all programs from Grid for row with index <paramref name="rowIndex"/>.
    /// </summary>
    /// <param name="rowIndex">RowIndex.</param>
    /// <returns>Controls.</returns>
    private IEnumerable<Control> GetRowItems(int rowIndex)
    {
      return Children.OfType<Control>().Where(el => el.Context is ProgramListItem && GetRow(el) == rowIndex);
    }

    #endregion

    #region Focus handling

    public override void OnKeyPressed(ref Key key)
    {
      base.OnKeyPressed(ref key);

      if (key == Key.None)
        // Key event was handeled by child
        return;

      if (!CheckFocusInScope())
        return;

      if (key == Key.Down && OnDown())
        key = Key.None;
      else if (key == Key.Up && OnUp())
        key = Key.None;
      else if (key == Key.Left && OnLeft())
        key = Key.None;
      else if (key == Key.Right && OnRight())
        key = Key.None;
      //else if (key == Key.Home && OnHome())
      //  key = Key.None;
      //else if (key == Key.End && OnEnd())
      //  key = Key.None;
      //else if (key == Key.PageDown && OnPageDown())
      //  key = Key.None;
      //else if (key == Key.PageUp && OnPageUp())
      //  key = Key.None;
    }

    private bool IsViewPortAtTop
    {
      get
      {
        return ChannelsPrograms == null || ChannelsPrograms.Count == 0 || _channelViewOffset == 0;
      }
    }

    private bool IsViewPortAtBottom
    {
      get
      {
        return ChannelsPrograms == null || ChannelsPrograms.Count == 0 || _channelViewOffset >= ChannelsPrograms.Count - 1 - _numberOfRows;
      }
    }

    private static SlimTvMultiChannelGuideModel SlimTvMultiChannelGuideModel
    {
      get
      {
        SlimTvMultiChannelGuideModel model = (SlimTvMultiChannelGuideModel)ServiceRegistration.Get<IWorkflowManager>().GetModel(new Guid(SlimTvMultiChannelGuideModel.MODEL_ID_STR));
        return model;
      }
    }

    /// <summary>
    /// Checks if the currently focused control is contained in the EpgGrid. We only need to handle focus changes inside the EpgGrid, but not in any other control.
    /// </summary>
    bool CheckFocusInScope()
    {
      Screen screen = Screen;
      Visual focusPath = screen == null ? null : screen.FocusedElement;
      while (focusPath != null)
      {
        if (focusPath == this)
          // Focused control is located in our focus scope
          return true;
        focusPath = focusPath.VisualParent;
      }
      return false;
    }

    private bool OnDown()
    {
      if (!MoveFocus1(MoveFocusDirection.Down))
      {
        if (IsViewPortAtBottom)
          return false;

        _channelViewOffset++;
        UpdateViewportVertical(-1);
        return MoveFocus1(MoveFocusDirection.Down); // After we created a new row, try to set focus again
      }
      return true;
    }

    private bool OnUp()
    {
      if (!MoveFocus1(MoveFocusDirection.Up))
      {
        if (IsViewPortAtTop)
          return false;

        _channelViewOffset--;
        UpdateViewportVertical(+1);
        return MoveFocus1(MoveFocusDirection.Up); // After we created a new row, try to set focus again
      }
      return true;
    }

    private bool OnRight()
    {
      if (!MoveFocus1(MoveFocusDirection.Right))
      {
        SlimTvMultiChannelGuideModel.ScrollForward();
        UpdateViewportHorizontal();
      }
      return true;
    }

    private bool OnLeft()
    {
      if (!MoveFocus1(MoveFocusDirection.Left))
      {
        SlimTvMultiChannelGuideModel.ScrollBackward();
        UpdateViewportHorizontal();
      }
      return true;
    }

    private void UpdateViewportHorizontal()
    {
      CreateVisibleChildren(true);
      ArrangeOverride();
    }

    private void UpdateViewportVertical(int moveOffset)
    {
      lock (Children.SyncRoot)
      {
        List<FrameworkElement> removeList = new List<FrameworkElement>();
        foreach (FrameworkElement element in Children)
        {
          int row = GetRow(element);
          int targetRow = row + moveOffset;
          if (targetRow >= 0 && targetRow < _numberOfRows)
            SetRow(element, targetRow);
          else
            removeList.Add(element);
        }
        removeList.ForEach(Children.Remove);

        int rowIndex = moveOffset > 0 ? 0 : _numberOfRows - 1;
        int channelIndex = _channelViewOffset + rowIndex;
        CreateOrUpdateRow(false, ref channelIndex, rowIndex);
      }
      ArrangeOverride();
    }

    #endregion
  }
}
