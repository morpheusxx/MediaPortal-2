#region Copyright (C) 2007-2021 Team MediaPortal

/*
    Copyright (C) 2007-2021 Team MediaPortal
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
using System.Threading;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.PluginManager;
using MediaPortal.Common.Runtime;
using MediaPortal.UI.Control.InputManager;
using MediaPortal.UI.Presentation.Actions;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.SkinEngine;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UI.SkinEngine.SkinManagement;
using SkiaSharp;

namespace MediaPortal.Plugins.StatisticsRenderer
{
  /// <summary>
  /// Draws render statistics (frame rate, render time, glitches) over the GUI. Toggled by F10; F11 switches the render strategy and
  /// F12 the render pipeline.
  /// </summary>
  public class StatisticsRenderer : IPluginStateTracker, IDisposable
  {
    #region Constants and structs

    private const int RENDER_OFFSET_LEFT = 20;
    private const int RENDER_OFFSET_TOP = 200;
    private const int TEXT_SIZE = 20;
    private const int MAX_STAT_VALUES = 500;
    struct Stats
    {
      public float Fps;
      public float MsFrameRenderTime;
      public int Glitch;
    }

    #endregion

    #region Fields

    private readonly RingBuffer<Stats> _stats = new RingBuffer<Stats>(MAX_STAT_VALUES, true);

    private int _tearingPos;
    private SKFont _font;

    private TimeSpan _totalGuiRenderDuration;
    private TimeSpan _guiRenderDuration;
    private int _totalFrameCount = 0;
    private int _frameCount = 0;
    private DateTime _frameRenderingStartTime;
    private DateTime _lastFrameEndTime = DateTime.MinValue;
    private int _fpsCounter;
    private int _glitchCounter;
    private DateTime _fpsTimer;
    private string _perfLogString = string.Empty;
    private volatile bool _statsEnabled;

    private float _fps;
    protected ManualResetEvent _renderFinished = new ManualResetEvent(true);

    protected AsynchronousMessageQueue _messageQueue;
    protected object _syncObj = new object();

    #endregion

    #region Members

    /// <summary>
    /// Allocates resources and attaches to render events.
    /// </summary>
    private void EnableStats()
    {
      _font = new SKFont(SKTypeface.Default, TEXT_SIZE);
      _fpsTimer = DateTime.Now;
      _lastFrameEndTime = DateTime.MinValue;

      Log("Screen {0}x{1} at refresh rate {2} Hz", SkinContext.CurrentDisplayMode.Width, SkinContext.CurrentDisplayMode.Height, SkinContext.CurrentDisplayMode.RefreshRate);

      Log("Attach render events");
      SkinContext.DeviceSceneBegin += BeginScene;
      SkinContext.DeviceSceneEnd += EndScene;
      _statsEnabled = true;
    }

    private static void TryDispose<TE>(ref TE disposable)
    {
      IDisposable disp = disposable as IDisposable;
      if (disp != null)
        disp.Dispose();
      disposable = default(TE);
    }

    /// <summary>
    /// Detaches renderer events and frees resources.
    /// </summary>
    private void DisableStats()
    {
      _statsEnabled = false; // Avoids new stats render cycles after our waiting for the render event
      SkinContext.DeviceSceneBegin -= BeginScene;
      SkinContext.DeviceSceneEnd -= EndScene;
      _renderFinished.WaitOne(TimeSpan.FromSeconds(2)); // Wait for the last render cycle to finish
      TryDispose(ref _font);
    }

    private static void ToggleRenderMode()
    {
      Log("Toggling render mode...");
      SkinContext.NextRenderStrategy();
      Log("Render mode is now '" + SkinContext.RenderStrategy.Name + "'");
    }

    private static void ToggleRenderPipeline()
    {
      Log("Toggling render pipeline...");
      SkinContext.NextRenderPipeline();
      Log("Render pipeline is now '" + SkinContext.RenderPipeline.GetType().Name + "'");
    }

    private void ToggleStatsRendering()
    {
      if (_statsEnabled)
      {
        Log("Disabling render statistics rendering");
        DisableStats();
      }
      else
      {
        Log("Enabling render statistics rendering");
        EnableStats();
      }
    }

    #endregion

    #region Render event handlers

    public void BeginScene(object sender, EventArgs args)
    {
      if (!_statsEnabled)
        return;

      _frameRenderingStartTime = DateTime.Now;
    }

    public void EndScene(object sender, EventArgs args)
    {
      if (!_statsEnabled)
        return;
      _renderFinished.Reset();
      try
      {
        DateTime now = DateTime.Now;
        TimeSpan guiDur = now - _frameRenderingStartTime;
        _totalGuiRenderDuration += guiDur;
        _guiRenderDuration += guiDur;
        _totalFrameCount++;
        _frameCount++;

        // A frame is a glitch if it took (nearly) two refresh intervals since the last frame
        int glitch = 0;
        int refreshRate = Math.Max(SkinContext.CurrentDisplayMode.RefreshRate, 1);
        if (_lastFrameEndTime != DateTime.MinValue && (now - _lastFrameEndTime).TotalMilliseconds > 1.5 * 1000.0 / refreshRate)
        {
          glitch = 1;
          _glitchCounter++;
        }
        _lastFrameEndTime = now;

        _fpsCounter++;
        TimeSpan ts = now - _fpsTimer;
        float secs = (float) ts.TotalSeconds;
        _fps = _fpsCounter / secs;

        Stats currentFrameStats = new Stats
          {
            Fps = _fps,
            MsFrameRenderTime = (float) guiDur.TotalMilliseconds,
            Glitch = glitch
          };

        if (ts.TotalSeconds >= 1.0f)
        {
          float totalAvgGuiTime = (float) _totalGuiRenderDuration.TotalMilliseconds / _totalFrameCount;
          float avgGuiTime = (float) _guiRenderDuration.TotalMilliseconds / _frameCount;

          _perfLogString = string.Format(
              "Render: {0:0.00} fps [red], {1} frames last meas., avg GUI time {2:0.00}, last sec: {3:0.00} [blue], " +
              "glitches last sec: {5} [ylw]\r\nRender mode enabled: {4}",
              _fps, _fpsCounter, totalAvgGuiTime, avgGuiTime, SkinContext.RenderStrategy.Name, _glitchCounter);
          _perfLogString += "\r\n" + GetScreenInfo();
          _perfLogString += "\r\n" + GetRenderPipelineInfo();
          _perfLogString += "\r\n" + GetPlayerInfos();
          _fpsCounter = 0;
          _frameCount = 0;
          _glitchCounter = 0;
          _guiRenderDuration = TimeSpan.Zero;
          _fpsTimer = DateTime.Now;
        }

        _stats.Push(currentFrameStats);
        SKCanvas canvas = SkinContext.Canvas;
        if (canvas != null)
        {
          canvas.Save();
          canvas.ResetMatrix();
          DrawLines(canvas);
          DrawTearingTest(canvas);
          DrawText(canvas, _perfLogString);
          canvas.Restore();
        }
      }
      finally
      {
        _renderFinished.Set();
      }
    }

    #endregion

    #region Rendering

    private void DrawTearingTest(SKCanvas canvas)
    {
      int width = SkinContext.BackBufferWidth;
      int height = SkinContext.BackBufferHeight;
      const int tearWidth = 4;
      int left = _tearingPos;
      if (left + tearWidth >= width)
        left = 0;

      using (SKPaint paint = new SKPaint { Color = new SKColor(255, 255, 255, 255) })
        canvas.DrawRect(SKRect.Create(left, 0, tearWidth, height), paint);

      left = (left + tearWidth + 15) % width;
      if (left + tearWidth >= width)
        left = 0;
      using (SKPaint paint = new SKPaint { Color = new SKColor(100, 100, 100, 255) })
        canvas.DrawRect(SKRect.Create(left, 0, tearWidth, height), paint);

      _tearingPos = (_tearingPos + 7) % width;
    }

    private void DrawText(SKCanvas canvas, string text)
    {
      if (string.IsNullOrEmpty(text) || _font == null)
        return;
      using (SKPaint paint = new SKPaint { Color = SKColors.Red, IsAntialias = true })
      {
        float y = TEXT_SIZE;
        foreach (string line in text.Split(new[] { "\r\n" }, StringSplitOptions.None))
        {
          canvas.DrawText(line, RENDER_OFFSET_LEFT, y, _font, paint);
          y += TEXT_SIZE;
        }
      }
    }

    private void DrawLines(SKCanvas canvas)
    {
      using (SKPaint paint = new SKPaint { StrokeWidth = 2.5f, IsAntialias = true, Style = SKPaintStyle.Stroke })
      {
        paint.Color = new SKColor(204, 204, 204, 204);
        canvas.DrawLine(RENDER_OFFSET_LEFT, RENDER_OFFSET_TOP + 30, RENDER_OFFSET_LEFT + MAX_STAT_VALUES, RENDER_OFFSET_TOP + 30, paint);

        int x = 0;
        float lastFps = 0, lastRender = 0, lastGlitch = 0;
        foreach (Stats stats in _stats.ReadAll(_totalFrameCount))
        {
          float px = RENDER_OFFSET_LEFT + x;
          float fps = RENDER_OFFSET_TOP - stats.Fps;
          float render = RENDER_OFFSET_TOP + 30 + 2 * stats.MsFrameRenderTime;
          float glitch = RENDER_OFFSET_TOP + stats.Glitch * -5; // scale to see glitches better, mirror them
          if (x > 0)
          {
            paint.Color = SKColors.Red;
            canvas.DrawLine(px - 1, lastFps, px, fps, paint);
            paint.Color = SKColors.Blue;
            canvas.DrawLine(px - 1, lastRender, px, render, paint);
            paint.Color = SKColors.Yellow;
            canvas.DrawLine(px - 1, lastGlitch, px, glitch, paint);
          }
          lastFps = fps;
          lastRender = render;
          lastGlitch = glitch;
          x++;
        }
      }
    }

    #endregion

    #region Helpers

    private string GetScreenInfo()
    {
      return string.Format("Screen Res: {0}x{1} @ {2}Hz", SkinContext.BackBufferWidth, SkinContext.BackBufferHeight, SkinContext.CurrentDisplayMode.RefreshRate);
    }

    private string GetRenderPipelineInfo()
    {
      return string.Format("Render Pipeline: {0}", SkinContext.RenderPipeline.GetType().Name);
    }

    private static string GetPlayerInfos()
    {
      string playerInfos = string.Empty;
      IPlayerManager pm = ServiceRegistration.Get<IPlayerManager>();
      int index = 0;
      pm.ForEach(psc =>
        {
          IVideoFrameSource player = psc.CurrentPlayer as IVideoFrameSource;
          if (player == null || player.Texture == null)
            return;
          playerInfos += String.Format("{0}Player {1}: Resolution {2}x{3}", string.IsNullOrEmpty(playerInfos) ? "" : "\r\n", index++, player.Texture.Width, player.Texture.Height);
        });
      return playerInfos;
    }

    private static void Log(string format, params object[] args)
    {
      ServiceRegistration.Get<ILogger>().Debug("StatisticsRenderer: " + format, args);
    }

    #endregion

    #region IDisposable implementation

    public void Dispose()
    {
      DisableStats();
    }

    #endregion

    private void RegisterKeyBindings()
    {
      IInputManager manager = ServiceRegistration.Get<IInputManager>(false);
      if (manager != null)
      {
        Log("Registering KeyBindings on IInputManager");
        manager.AddKeyBinding(Key.F10, new VoidKeyActionDlgt(ToggleStatsRendering));
        manager.AddKeyBinding(Key.F11, new VoidKeyActionDlgt(ToggleRenderMode));
        manager.AddKeyBinding(Key.F12, new VoidKeyActionDlgt(ToggleRenderPipeline));
      }
    }

    private static void UnregisterKeyBindings()
    {
      Log("Unregistering KeyBindings from IInputManager");
      IInputManager manager = ServiceRegistration.Get<IInputManager>(false);
      if (manager != null)
      {
        manager.RemoveKeyBinding(Key.F10);
        manager.RemoveKeyBinding(Key.F11);
        manager.RemoveKeyBinding(Key.F12);
      }
    }

    protected void DropMessageQueue()
    {
      lock (_syncObj)
      {
        if (_messageQueue != null)
          _messageQueue.Terminate();
        _messageQueue = null;
      }
    }

    public void Activated(PluginRuntime pluginRuntime)
    {
      ISystemStateService sss = ServiceRegistration.Get<ISystemStateService>();
      if (sss.CurrentState == SystemState.Running)
      {
        RegisterKeyBindings();
      }
      else
      {
        _messageQueue = new AsynchronousMessageQueue(typeof(StatisticsRenderer), new string[]
          {
              SystemMessaging.CHANNEL
          });
        _messageQueue.MessageReceived += OnMessageReceived;
        _messageQueue.Start();
      }
    }

    void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (message.ChannelName == SystemMessaging.CHANNEL)
      {
        SystemMessaging.MessageType messageType = (SystemMessaging.MessageType) message.MessageType;
        if (messageType == SystemMessaging.MessageType.SystemStateChanged)
        {
          SystemState newState = (SystemState) message.MessageData[SystemMessaging.NEW_STATE];
          if (newState == SystemState.Running)
          {
            RegisterKeyBindings();
          }
          if (newState == SystemState.ShuttingDown)
          {
            DisableStats();
            UnregisterKeyBindings();
            DropMessageQueue();
          }
        }
      }
    }

    public bool RequestEnd()
    {
      return true;
    }

    public void Stop()
    {
    }

    public void Continue()
    {
    }

    public void Shutdown()
    {
      DisableStats();
      UnregisterKeyBindings();
      DropMessageQueue();
    }
  }
}
