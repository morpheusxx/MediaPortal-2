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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.Runtime;
using MediaPortal.Common.Settings;
using MediaPortal.UI.Control.InputManager;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.Presentation.Screens;
using MediaPortal.UI.Services.Players.VideoPlayerSynchronizationStrategies;
using MediaPortal.UI.Settings;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.Gpu;
using MediaPortal.UI.SkinEngine.InputManagement;
using MediaPortal.UI.SkinEngine.ScreenManagement;
using MediaPortal.UI.SkinEngine.Settings;
using MediaPortal.UI.SkinEngine.Utils;
using MediaPortal.UI.SkinEngine.SkinManagement;
using MediaPortal.Utilities.Process;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;
using Key = MediaPortal.UI.Control.InputManager.Key;
using MouseButtons = MediaPortal.UI.Control.InputManager.MouseButtons;
using Screen = MediaPortal.UI.SkinEngine.ScreenManagement.Screen;
using SilkMouseButton = Silk.NET.Input.MouseButton;

namespace MediaPortal.UI.SkinEngine.GUI
{
  public delegate void SwitchModeDelegate(ScreenMode mode);

  /// <summary>
  /// The main window of the SkinEngine. It owns the platform window (created by Silk.NET) with its OpenGL context, translates the
  /// input events to the <see cref="IInputManager"/> and runs the main loop, which renders the UI via the <see cref="GraphicsDevice"/>.
  /// </summary>
  /// <remarks>
  /// All window and rendering work is done in the thread which created the window (see <see cref="RunMainLoop"/>), other threads can
  /// use <see cref="Invoke"/>.
  /// </remarks>
  public class MainWindow : IScreenControl, IRenderHost, IDisposable
  {
    private const string SCREEN_SAVER_SCREEN = "ScreenSaver";
    private const int MULTI_CLICK_TIME_MS = 500;
    private const int MULTI_CLICK_DISTANCE_PX = 6;

    private readonly ScreenManager _screenManager;
    private readonly ConcurrentQueue<Action> _invokeQueue = new ConcurrentQueue<Action>();
    private readonly int _windowThreadId;

    private IWindow _window;
    private IInputContext _input;
    private IKeyboard _keyboard;
    private IMouse _mouse;
    private string _rendererDescription = "";

    private bool _renderEnabled;
    private bool _closeRequested;
    private bool _hasFocus = true;
    private bool _forceOnTop;
    private bool _disableTopMost;
    private bool _mouseHidden;
    private Point _previousMousePosition = new Point(-1, -1);

    // Window mode handling
    private ScreenMode _mode = ScreenMode.NormalWindowed;
    private ScreenMode _previousMode = ScreenMode.NormalWindowed;
    private Size _previousWindowClientSize;
    private Point _previousWindowLocation;
    private WindowState _previousWindowState = WindowState.Normal;
    private int _multiSampleCount;

    // Multi click detection
    private SilkMouseButton _lastClickButton;
    private int _lastClickTime;
    private Point _lastClickPosition;
    private int _clickCount;

    // Screen saver and suspend level
    protected bool _isScreenSaverEnabled = true;
    protected bool _isScreenSaverActive = false;
    protected ScreenSaverController _screenSaverController = null;
    protected SuspendLevel _applicationSuspendLevel = SuspendLevel.None;
    protected SuspendLevel _playerSuspendLevel = SuspendLevel.None;
    protected TimeSpan _screenSaverTimeOut;
    private IVideoPlayerSynchronizationStrategy _videoPlayerSynchronizationStrategy = null;

    public MainWindow(ScreenManager screenManager)
    {
      _screenManager = screenManager;
      _windowThreadId = Environment.CurrentManagedThreadId;

      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Registering MainWindow as IScreenControl service");
      ServiceRegistration.Set<IScreenControl>(this);

      StartupSettings startupSettings = ServiceRegistration.Get<ISettingsManager>().Load<StartupSettings>();
      AppSettings appSettings = ServiceRegistration.Get<ISettingsManager>().Load<AppSettings>();

      IList<IMonitor> monitors = GetMonitors();
      // Default screen is the primary one, or the one which was selected in the startup settings
      int validScreenNum = 0;
      if (startupSettings.StartupScreenNum >= 0 && startupSettings.StartupScreenNum < monitors.Count)
        validScreenNum = startupSettings.StartupScreenNum;
      IMonitor preferredMonitor = monitors.Count > 0 ? monitors[validScreenNum] : null;
      Rectangle<int> screenBounds = preferredMonitor != null ? preferredMonitor.Bounds : new Rectangle<int>(0, 0, 1920, 1080);

      Size desiredWindowedSize;
      Point desiredLocation;
      if (appSettings.WindowPosition.HasValue && appSettings.WindowSize.HasValue)
      {
        desiredWindowedSize = appSettings.WindowSize.Value;
        desiredLocation = ValidatePosition(appSettings.WindowPosition.Value, new Size(screenBounds.Size.X, screenBounds.Size.Y), ref desiredWindowedSize);
      }
      else
      {
        desiredLocation = new Point(screenBounds.Origin.X, screenBounds.Origin.Y);
        desiredWindowedSize = new Size(SkinContext.SkinResources.SkinWidth, SkinContext.SkinResources.SkinHeight);
      }
      _previousWindowLocation = desiredLocation;
      _previousWindowClientSize = desiredWindowedSize;
      _previousMode = ScreenMode.NormalWindowed;

      _multiSampleCount = ToSampleCount(appSettings.MultisampleType);

      WindowOptions options = WindowOptions.Default;
      options.Title = "MediaPortal 2";
      options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
      options.VSync = false;
      options.PreferredStencilBufferBits = 8;
      options.PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8);
      options.Samples = _multiSampleCount;
      options.IsVisible = false;
      options.ShouldSwapAutomatically = false;
      options.FramesPerSecond = 0;
      options.UpdatesPerSecond = 0;
      if (appSettings.ScreenMode == ScreenMode.FullScreen)
      {
        options.Position = screenBounds.Origin;
        options.Size = screenBounds.Size;
        options.WindowBorder = WindowBorder.Hidden;
        _mode = ScreenMode.FullScreen;
      }
      else
      {
        options.Position = new Vector2D<int>(desiredLocation.X, desiredLocation.Y);
        options.Size = new Vector2D<int>(desiredWindowedSize.Width, desiredWindowedSize.Height);
        options.WindowBorder = WindowBorder.Resizable;
        _mode = appSettings.ScreenMode;
        _forceOnTop = _mode == ScreenMode.WindowedOnTop;
      }

      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Creating window {0}x{1}, mode {2}", options.Size.X, options.Size.Y, _mode);
      _window = Window.Create(options);
      _window.Initialize();
      if (_window.GLContext == null)
        throw new InvalidOperationException("The window has no OpenGL context");
      _window.MakeCurrent();

      using (Silk.NET.OpenGL.GL gl = Silk.NET.OpenGL.GL.GetApi(_window))
        _rendererDescription = string.Format("{0}, OpenGL {1}", gl.GetStringS(Silk.NET.OpenGL.StringName.Renderer), gl.GetStringS(Silk.NET.OpenGL.StringName.Version));

      HookWindowEvents();
      HookInput();

      SkinContext.WindowSize = new Size(_window.FramebufferSize.X, _window.FramebufferSize.Y);

      // GraphicsDevice has to be initialized after the window was sized correctly
      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Initialize graphics device");
      GraphicsDevice.Initialize_MainThread(this);

      // Read and apply ScreenSaver settings
      _screenSaverTimeOut = TimeSpan.FromMinutes(appSettings.ScreenSaverTimeoutMin);
      _isScreenSaverEnabled = appSettings.ScreenSaverEnabled;

      _applicationSuspendLevel = appSettings.SuspendLevel;
      UpdateSystemSuspendLevel_MainThread();

      VideoPlayerSynchronizationStrategy = new SynchronizeToPrimaryPlayer();

      ClipboardManager.Instance.GetTextAccessor = GetClipboardText;
      ClipboardManager.Instance.SetTextAccessor = SetClipboardText;
    }

    #region Helpers

    internal static IList<IMonitor> GetMonitors()
    {
      try
      {
        return Silk.NET.Windowing.Monitor.GetMonitors(null).ToList();
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Warn("SkinEngine MainWindow: Error enumerating the monitors", ex);
        return new List<IMonitor>();
      }
    }

    private static int ToSampleCount(MultisampleType type)
    {
      int value = (int) type;
      return value >= 2 ? value : 0;
    }

    private Point ValidatePosition(Point value, Size availableSize, ref Size desiredWindowedSize)
    {
      if (desiredWindowedSize.Width > availableSize.Width || desiredWindowedSize.Height > availableSize.Height || desiredWindowedSize.Width == 0 || desiredWindowedSize.Height == 0)
        desiredWindowedSize = availableSize;

      var res = value;
      if (res.X < 0)
        res.X = 0;
      if (res.X + desiredWindowedSize.Width > availableSize.Width)
        res.X = availableSize.Width - desiredWindowedSize.Width;
      if (res.Y < 0)
        res.Y = 0;
      if (res.Y + desiredWindowedSize.Height > availableSize.Height)
        res.Y = availableSize.Height - desiredWindowedSize.Height;
      return res;
    }

    private string GetClipboardText()
    {
      string result = null;
      Invoke(() => result = _keyboard == null ? null : _keyboard.ClipboardText);
      return result;
    }

    private void SetClipboardText(string text)
    {
      Invoke(() =>
        {
          if (_keyboard != null)
            _keyboard.ClipboardText = text;
        });
    }

    /// <summary>
    /// Returns the monitor the window is currently shown on.
    /// </summary>
    private IMonitor CurrentMonitor
    {
      get
      {
        IMonitor monitor = _window == null ? null : _window.Monitor;
        return monitor ?? GetMonitors().FirstOrDefault();
      }
    }

    private int CurrentMonitorIndex
    {
      get
      {
        IMonitor current = CurrentMonitor;
        if (current == null)
          return 0;
        IList<IMonitor> all = GetMonitors();
        for (int i = 0; i < all.Count; i++)
          if (all[i].Bounds.Origin == current.Bounds.Origin)
            return i;
        return 0;
      }
    }

    #endregion

    #region Thread handling

    /// <summary>
    /// Returns <c>true</c> if the calling thread is the thread of the window.
    /// </summary>
    public bool IsRenderThread
    {
      get { return Environment.CurrentManagedThreadId == _windowThreadId; }
    }

    /// <summary>
    /// Executes the given action in the window thread and waits until it was executed. If the caller is the window thread,
    /// the action is executed directly.
    /// </summary>
    public void Invoke(Action action)
    {
      if (IsRenderThread)
      {
        action();
        return;
      }
      using (ManualResetEventSlim finished = new ManualResetEventSlim(false))
      {
        Exception error = null;
        _invokeQueue.Enqueue(() =>
          {
            try
            {
              action();
            }
            catch (Exception ex)
            {
              error = ex;
            }
            finally
            {
              finished.Set();
            }
          });
        // Don't block forever if the main loop isn't running (anymore)
        while (!finished.Wait(100))
          if (_closeRequested && !_invokeQueue.Any())
            return;
        if (error != null)
          throw new InvalidOperationException("Error executing action in window thread", error);
      }
    }

    /// <summary>
    /// Executes the given action asynchronously in the window thread.
    /// </summary>
    public void BeginInvoke(Action action)
    {
      _invokeQueue.Enqueue(action);
    }

    private void ProcessInvokeQueue()
    {
      Action action;
      while (_invokeQueue.TryDequeue(out action))
        action();
    }

    #endregion

    #region Main loop

    /// <summary>
    /// Runs the main loop of the window: processes the window events, schedules the rendering and presents the frames.
    /// The method returns when the window was closed. Must be called in the thread which created the window.
    /// </summary>
    public void RunMainLoop()
    {
      ILogger logger = ServiceRegistration.Get<ILogger>();
      logger.Debug("SkinEngine MainWindow: Starting main loop");
      try
      {
        while (!_closeRequested && !_window.IsClosing)
        {
          _window.DoEvents();
          ProcessInvokeQueue();
          if (_closeRequested || _window.IsClosing)
            break;
          OnApplicationIdle();

          bool shouldWait = true;
          if (_renderEnabled && GraphicsDevice.DeviceOk && _window.FramebufferSize.X > 0 && _window.FramebufferSize.Y > 0 &&
              _window.WindowState != WindowState.Minimized)
          {
            _window.MakeCurrent();
            shouldWait = GraphicsDevice.Render(true);
            if (!shouldWait)
              _window.GLContext.SwapBuffers();
          }
          if (shouldWait || !_hasFocus)
            // Nothing was rendered, the device was lost or we don't have focus - reduce the render rate
            Thread.Sleep(10);
          if (!GraphicsDevice.DeviceOk)
            ReclaimDevice();
        }
      }
      catch (Exception ex)
      {
        logger.Error("SkinEngine MainWindow: Main loop crashed", ex);
      }
      logger.Debug("SkinEngine MainWindow: Main loop was stopped");
      OnClosing();
    }

    private void ReclaimDevice()
    {
      if (_hasFocus && GraphicsDevice.ReclaimDevice())
        StartUI();
    }

    public void StopUI()
    {
      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Stopping UI");
      _renderEnabled = false;
    }

    public void StartUI()
    {
      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Starting UI");
      _renderEnabled = true;
    }

    internal void StopRenderThread()
    {
      StopUI();
    }

    private bool _closed;

    private void OnClosing()
    {
      if (_closed)
        return;
      _closed = true;
      ILogger logger = ServiceRegistration.Get<ILogger>();
      try
      {
        logger.Debug("SkinEngine MainWindow: Stopping");
        StoreClientBounds();
        StopUI();
        UIResourcesHelper.ReleaseUIResources();
      }
      catch (Exception ex)
      {
        logger.Error("SkinEngine MainWindow: Error occured in closing handler", ex);
      }
      logger.Debug("SkinEngine MainWindow: Closing");
    }

    #endregion

    #region Window events

    private void HookWindowEvents()
    {
      _window.FramebufferResize += OnFramebufferResize;
      _window.FocusChanged += OnFocusChanged;
      _window.Closing += () => _closeRequested = true;
      _window.StateChanged += OnWindowStateChanged;
    }

    private bool _adaptToSizeEnabled = true;

    /// <summary>
    /// Raised in the window thread when the window got the input focus.
    /// </summary>
    public event EventHandler Activated;

    /// <summary>
    /// Raised in the window thread when the window lost the input focus.
    /// </summary>
    public event EventHandler Deactivated;

    /// <summary>
    /// Brings the window to the front and gives it the input focus.
    /// </summary>
    public void Activate()
    {
      Invoke(() =>
        {
          _window.IsVisible = true;
          if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = _mode == ScreenMode.NormalWindowed ? WindowState.Normal : WindowState.Maximized;
          IntPtr handle = MainWindowHandle;
          if (handle != IntPtr.Zero && OperatingSystem.IsWindows())
            NativeWindowMethods.SetForegroundWindow(handle);
          CheckTopMost(true);
        });
    }

    /// <summary>
    /// Gets the information if the window has the input focus.
    /// </summary>
    public bool IsActive
    {
      get { return _hasFocus; }
    }

    /// <summary>
    /// Gets the bounds of the monitor which the window is shown on, in desktop coordinates.
    /// </summary>
    public System.Drawing.Rectangle CurrentMonitorBounds
    {
      get
      {
        IMonitor monitor = CurrentMonitor;
        if (monitor == null)
          return new System.Drawing.Rectangle(0, 0, FramebufferWidth, FramebufferHeight);
        Rectangle<int> bounds = monitor.Bounds;
        return new System.Drawing.Rectangle(bounds.Origin.X, bounds.Origin.Y, bounds.Size.X, bounds.Size.Y);
      }
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
      if (size.X <= 0 || size.Y <= 0)
        return;
      if (!_adaptToSizeEnabled || GraphicsDevice.GrContext == null)
        return;
      AdaptToSize();
    }

    private void OnWindowStateChanged(WindowState state)
    {
      if (state != WindowState.Minimized && state != _previousWindowState && _mode != ScreenMode.FullScreen && _previousMode != ScreenMode.FullScreen)
      {
        AdaptToSize();
        _previousWindowState = state;
      }
    }

    private void OnFocusChanged(bool focused)
    {
      _hasFocus = focused;
      CheckTopMost();
      EventHandler handler = focused ? Activated : Deactivated;
      if (handler != null)
        handler(this, EventArgs.Empty);
      try
      {
        IInputManager inputManager = ServiceRegistration.Get<IInputManager>();
        if (focused)
          inputManager.ApplicationActivated();
        else
          inputManager.ApplicationDeactivated();
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occurred in focus handler", ex);
      }
    }

    protected void AdaptToSize()
    {
      StoreClientBounds();
      GraphicsDevice.OnFramebufferResized();
      SkinContext.WindowSize = new Size(_window.FramebufferSize.X, _window.FramebufferSize.Y);
    }

    protected void StoreClientBounds()
    {
      if (_mode == ScreenMode.FullScreen || _window == null)
        return;
      // We must store the window state to be able to correctly restore the clients bounds if the user was in maximized mode
      // when switching to fullscreen
      _previousWindowState = _window.WindowState;
      if (_window.WindowState != WindowState.Normal)
        return;
      // Only store size and position if we are in windowed mode and not maximized
      AppSettings appSettings = ServiceRegistration.Get<ISettingsManager>().Load<AppSettings>();
      appSettings.WindowPosition = _previousWindowLocation = new Point(_window.Position.X, _window.Position.Y);
      appSettings.WindowSize = _previousWindowClientSize = new Size(_window.Size.X, _window.Size.Y);
      ServiceRegistration.Get<ISettingsManager>().Save(appSettings);
    }

    /// <summary>
    /// Sets the TopMost property setting according to the current fullscreen setting and activation mode.
    /// </summary>
    protected void CheckTopMost(bool force = false)
    {
      if (_window == null)
        return;
#if DEBUG
      _window.TopMost = _forceOnTop;
#else
      _window.TopMost = !_disableTopMost && (_forceOnTop || IsFullScreen && (force || _hasFocus));
#endif
    }

    #endregion

    #region Input

    private void HookInput()
    {
      _input = _window.CreateInput();
      _keyboard = _input.Keyboards.FirstOrDefault();
      _mouse = _input.Mice.FirstOrDefault();
      if (_keyboard != null)
      {
        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyChar += OnKeyChar;
      }
      if (_mouse != null)
      {
        _mouse.MouseDown += OnMouseDown;
        _mouse.MouseUp += OnMouseUp;
        _mouse.MouseMove += OnMouseMove;
        _mouse.Scroll += OnMouseScroll;
        _mouse.Click += OnMouseClick;
      }
    }

    private bool IsKeyPressed(Silk.NET.Input.Key key)
    {
      return _keyboard != null && _keyboard.IsKeyPressed(key);
    }

    private void OnKeyDown(IKeyboard keyboard, Silk.NET.Input.Key silkKey, int scanCode)
    {
      try
      {
        bool alt = IsKeyPressed(Silk.NET.Input.Key.AltLeft) || IsKeyPressed(Silk.NET.Input.Key.AltRight);
        bool shift = IsKeyPressed(Silk.NET.Input.Key.ShiftLeft) || IsKeyPressed(Silk.NET.Input.Key.ShiftRight);
        bool control = IsKeyPressed(Silk.NET.Input.Key.ControlLeft) || IsKeyPressed(Silk.NET.Input.Key.ControlRight);
        bool super = IsKeyPressed(Silk.NET.Input.Key.SuperLeft) || IsKeyPressed(Silk.NET.Input.Key.SuperRight);

        // We'll handle special keys here
        Key key = InputMapper.MapSpecialKey(silkKey, alt, shift, control, super);
        if (key == Key.Close)
        {
          Shutdown();
          return;
        }
        if (key != Key.None)
          ServiceRegistration.Get<IInputManager>().KeyPress(key);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occured in KeyDown handler", ex);
      }
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
      try
      {
        // We'll handle printable keys here
        Key key = InputMapper.MapPrintableKeys(c);
        if (key != Key.None)
          ServiceRegistration.Get<IInputManager>().KeyPress(key);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occured in KeyPress handler", ex);
      }
    }

    private static MouseButtons ToMouseButtons(SilkMouseButton button)
    {
      switch (button)
      {
        case SilkMouseButton.Left:
          return MouseButtons.Left;
        case SilkMouseButton.Right:
          return MouseButtons.Right;
        case SilkMouseButton.Middle:
          return MouseButtons.Middle;
        case SilkMouseButton.Button4:
          return MouseButtons.XButton1;
        case SilkMouseButton.Button5:
          return MouseButtons.XButton2;
        default:
          return MouseButtons.None;
      }
    }

    /// <summary>
    /// Converts a position in window coordinates to framebuffer pixel coordinates (they differ on high DPI displays).
    /// </summary>
    private Point ToFramebuffer(System.Numerics.Vector2 position)
    {
      Vector2D<int> window = _window.Size;
      Vector2D<int> fb = _window.FramebufferSize;
      float scaleX = window.X > 0 ? fb.X / (float) window.X : 1f;
      float scaleY = window.Y > 0 ? fb.Y / (float) window.Y : 1f;
      return new Point((int) (position.X * scaleX), (int) (position.Y * scaleY));
    }

    private void OnMouseDown(IMouse mouse, SilkMouseButton button)
    {
      try
      {
        Point position = ToFramebuffer(mouse.Position);
        int now = Environment.TickCount;
        if (button == _lastClickButton && now - _lastClickTime <= MULTI_CLICK_TIME_MS &&
            Math.Abs(position.X - _lastClickPosition.X) <= MULTI_CLICK_DISTANCE_PX && Math.Abs(position.Y - _lastClickPosition.Y) <= MULTI_CLICK_DISTANCE_PX)
          _clickCount++;
        else
          _clickCount = 1;
        _lastClickButton = button;
        _lastClickTime = now;
        _lastClickPosition = position;
        ServiceRegistration.Get<IInputManager>().MouseDown(ToMouseButtons(button), _clickCount);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occured in MouseDown handler", ex);
      }
    }

    private void OnMouseUp(IMouse mouse, SilkMouseButton button)
    {
      try
      {
        ServiceRegistration.Get<IInputManager>().MouseUp(ToMouseButtons(button), _clickCount);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occured in MouseUp handler", ex);
      }
    }

    private void OnMouseClick(IMouse mouse, SilkMouseButton button, System.Numerics.Vector2 position)
    {
      try
      {
        ServiceRegistration.Get<IInputManager>().MouseClick(ToMouseButtons(button));
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occured in MouseClick handler", ex);
      }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
      if (!_renderEnabled)
        return;
      int delta = (int) (wheel.Y * 120);
      if (delta == 0)
        return;
      ServiceRegistration.Get<IInputManager>().MouseWheel(delta);
    }

    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
      try
      {
        if (!_renderEnabled)
          return;
        Point p = ToFramebuffer(position);
        if (p.X == _previousMousePosition.X && p.Y == _previousMousePosition.Y)
          return;
        if (_previousMousePosition.X < 0 && _previousMousePosition.Y < 0)
        {
          _previousMousePosition = p;
          return;
        }
        _previousMousePosition = p;
        ServiceRegistration.Get<IInputManager>().MouseMove(p.X, p.Y);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occured in MouseMove handler", ex);
      }
    }

    protected void ShowMouseCursor(bool show)
    {
      if (show != _mouseHidden)
        return;
      _mouseHidden = !show;
      if (_mouse != null)
        _mouse.Cursor.CursorMode = _mouseHidden ? CursorMode.Hidden : CursorMode.Normal;
    }

    #endregion

    #region Idle handling (screen saver)

    private void OnApplicationIdle()
    {
      try
      {
        // Screen saver
        IInputManager inputManager = ServiceRegistration.Get<IInputManager>();
        // Remember old state, calls to IScreenManager are only required on state changes
        bool wasScreenSaverActive = _isScreenSaverActive;
        if (_isScreenSaverEnabled)
        {
          IPlayerContextManager playerContextManager = ServiceRegistration.Get<IPlayerContextManager>();
          IPlayer primaryPlayer = playerContextManager[PlayerContextIndex.PRIMARY];
          IMediaPlaybackControl mbc = primaryPlayer as IMediaPlaybackControl;
          bool preventScreenSaver = ((primaryPlayer is IVideoPlayer || primaryPlayer is IImagePlayer) && (mbc == null || !mbc.IsPaused)) ||
              playerContextManager.IsFullscreenContentWorkflowStateActive;

          _isScreenSaverActive = !preventScreenSaver &&
              SkinContext.FrameRenderingStartTime - inputManager.LastMouseUsageTime > _screenSaverTimeOut &&
              SkinContext.FrameRenderingStartTime - inputManager.LastInputTime > _screenSaverTimeOut;

          if (_screenSaverController != null)
          {
            bool? activeOverride = _screenSaverController.IsScreenSaverActiveOverride;
            if (activeOverride.HasValue)
              _isScreenSaverActive = activeOverride.Value;
          }
        }
        else
          _isScreenSaverActive = false;
        if (wasScreenSaverActive != _isScreenSaverActive)
        {
          IScreenManager superLayerManager = ServiceRegistration.Get<IScreenManager>();
          superLayerManager.SetSuperLayer(_isScreenSaverActive ? SCREEN_SAVER_SCREEN : null);
        }

        // If we are in fullscreen mode, we may control the mouse cursor, else reset it to visible state, if state was switched
        ShowMouseCursor(!IsFullScreen || inputManager.IsMouseUsed);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("SkinEngine MainWindow: Error occured in Idle handler", ex);
      }
    }

    #endregion

    #region Video player state and suspend level

    /// <summary>
    /// Updates the local state corresponding to the current video player, given in parameter <paramref name="videoPlayer"/>.
    /// This method will check if the given player is suspended (i.e. it is paused). It will also update the thread state so that the system
    /// won't shut down while playing a video.
    /// </summary>
    private void UpdateVideoPlayerState(IVideoPlayer videoPlayer)
    {
      IMediaPlaybackControl player = videoPlayer as IMediaPlaybackControl;
      bool videoPlayerSuspended = player == null || player.IsPaused;
      if (videoPlayer != null)
        PlayerSuspendLevel = videoPlayerSuspended ? SuspendLevel.None : SuspendLevel.DisplayRequired;
      else
        HandleNonVideoPlayers();
    }

    private void HandleNonVideoPlayers()
    {
      IPlayerContextManager playerContextManager = ServiceRegistration.Get<IPlayerContextManager>();
      IPlayer primaryPlayer = playerContextManager[PlayerContextIndex.PRIMARY];
      IMediaPlaybackControl mbc = primaryPlayer as IMediaPlaybackControl;
      bool isPlaying = mbc == null || !mbc.IsPaused;
      if (primaryPlayer == null)
      {
        PlayerSuspendLevel = SuspendLevel.None;
        return;
      }
      if (primaryPlayer is IAudioPlayer)
      {
        // For audio players we allow turning off the screen, but avoiding suspend.
        PlayerSuspendLevel = isPlaying ? SuspendLevel.AvoidSuspend : SuspendLevel.None;
        return;
      }
      if (primaryPlayer is IImagePlayer)
      {
        // For image players we avoiding both suspend and display off.
        PlayerSuspendLevel = isPlaying ? SuspendLevel.DisplayRequired : SuspendLevel.None;
        return;
      }
    }

    private void SynchronizeToVideoPlayerFramerate(IVideoPlayer videoPlayer)
    {
      // The video is rendered by the player into a texture, so the render loop doesn't need to be synchronized to it any more.
    }

    public SuspendLevel PlayerSuspendLevel
    {
      get { return _playerSuspendLevel; }
      set
      {
        if (_playerSuspendLevel == value)
          return;
        _playerSuspendLevel = value;
        UpdateSystemSuspendLevel();
      }
    }

    protected void UpdateSystemSuspendLevel()
    {
      Invoke(UpdateSystemSuspendLevel_MainThread);
    }

    protected void UpdateSystemSuspendLevel_MainThread()
    {
      // We'll use the maximum suspend level from main suspend level and player suspend level
      SuspendLevel value = _applicationSuspendLevel;
      if (_playerSuspendLevel > value)
        value = _playerSuspendLevel;
      ServiceRegistration.Get<ISystemStateService>().SetCurrentSuspendLevel(value, true);
    }

    #endregion

    #region IScreenControl implementation

    public void Start()
    {
      _window.IsVisible = true;
      CheckTopMost(true);
      StartUI();
      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Running");
    }

    public void Shutdown()
    {
      VideoPlayerSynchronizationStrategy = null; // Stops the strategy component
      _closeRequested = true;
    }

    public void Restart()
    {
      // Do not block UI when calling other process via named pipe
      Task.Run(() =>
      {
        try
        {
          if (OperatingSystem.IsWindows())
          {
            if (!IpcClient.GetRunningApps().Contains("ClientLauncher"))
            {
              string clPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Tools", "MP2-ClientLauncher", "MP2-ClientLauncher.exe");
              Process.Start(clPath);
              int waitCounter = 50;
              // Wait until process is running and the pipe was created.
              while (waitCounter-- > 0 && !IpcClient.GetRunningApps().Contains("ClientLauncher"))
              {
                Thread.Sleep(100);
              }
            }
            // Sends a command to tray launcher that we want to be restarted.
            using (var client = new IpcClient("ClientLauncher"))
            {
              client.Connect();
              client.RequestRestart();
            }
          }
          else
          {
            // Start a new instance of the client. It waits until the running instance has released the single instance lock.
            Process.Start(Environment.ProcessPath);
            Shutdown();
          }
        }
        catch (Exception ex)
        {
          ServiceRegistration.Get<ILogger>().Error("Error trying to send restart request.", ex);
        }
      });
    }

    public void Minimize()
    {
      Invoke(() => _window.WindowState = WindowState.Minimized);
      Screen screen = _screenManager.FocusedScreen;
      if (screen != null)
        screen.RemoveCurrentFocus();
    }

    public void Restore()
    {
      Invoke(() => _window.WindowState = _mode == ScreenMode.NormalWindowed ? WindowState.Normal : WindowState.Maximized);
    }

    public void ConfigureScreenSaver(bool screenSaverEnabled, double screenSaverTimeoutMin)
    {
      AppSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<AppSettings>();
      settings.ScreenSaverTimeoutMin = screenSaverTimeoutMin;
      settings.ScreenSaverEnabled = screenSaverEnabled;
      ServiceRegistration.Get<ISettingsManager>().Save(settings);
      _screenSaverTimeOut = TimeSpan.FromMinutes(screenSaverTimeoutMin);
      _isScreenSaverEnabled = screenSaverEnabled;
    }

    public ScreenSaverController GetScreenSaverController()
    {
      if (_screenSaverController != null)
      {
        ServiceRegistration.Get<ILogger>().Warn("SkinEngine MainWindow: ScreenSaverControl is already registered, prevent creation of another ScreenSaverControl");
        return null;
      }
      return _screenSaverController = new ScreenSaverController(() => { _screenSaverController = null; });
    }

    public void SwitchMode(ScreenMode mode)
    {
      Invoke(() => SwitchMode_MainThread(mode));
    }

    private void SwitchMode_MainThread(ScreenMode mode)
    {
      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Switching mode to {0}", mode);
      bool newFullscreen = mode == ScreenMode.FullScreen;
      AppSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<AppSettings>();

      // Already done, no need to do it twice.
      if (mode == _mode)
        return;

      _previousMode = _mode;
      settings.ScreenMode = mode;
      ServiceRegistration.Get<ISettingsManager>().Save(settings);

      StopUI();
      _adaptToSizeEnabled = false;
      try
      {
        if (newFullscreen)
        {
          StoreClientBounds();
          SwitchToFullscreen(CurrentMonitor);
        }
        else
          SwitchToWindowedSize(mode, _previousWindowLocation, _previousWindowClientSize, _previousWindowState == WindowState.Maximized);
      }
      finally
      {
        _adaptToSizeEnabled = true;
      }
      _window.DoEvents();
      AdaptToSize();
      CheckTopMost();
      StartUI();
    }

    protected void SwitchToFullscreen(IMonitor monitor)
    {
      _window.WindowState = WindowState.Normal;
      _window.WindowBorder = WindowBorder.Hidden;
      _mode = ScreenMode.FullScreen;
      if (monitor == null)
        return;
      Rectangle<int> bounds = monitor.Bounds;
      ServiceRegistration.Get<ILogger>().Info("SkinEngine MainWindow: Set screen size to {0}x{1} (Mode: {2})", bounds.Size.X, bounds.Size.Y, _mode);
      _window.Position = bounds.Origin;
      _window.Size = bounds.Size;
    }

    protected void SwitchToWindowedSize(ScreenMode mode, Point location, Size clientSize, bool maximize)
    {
      _forceOnTop = mode == ScreenMode.WindowedOnTop;
      _window.WindowBorder = WindowBorder.Resizable;
      _window.WindowState = WindowState.Normal;
      _window.Position = new Vector2D<int>(location.X, location.Y);
      _window.Size = new Vector2D<int>(clientSize.Width, clientSize.Height);
      // We must restore the window state after having set the size/location to make the window remember the non-maximized bounds
      _window.WindowState = maximize ? WindowState.Maximized : WindowState.Normal;
      _mode = mode;
    }

    public bool DisableTopMost
    {
      get { return _disableTopMost; }
      set
      {
        _disableTopMost = value;
        Invoke(() => CheckTopMost());
      }
    }

    public bool IsFullScreen
    {
      get { return _mode == ScreenMode.FullScreen; }
    }

    public ScreenMode CurrentScreenMode
    {
      get { return _mode; }
    }

    public bool IsScreenSaverActive
    {
      get { return _isScreenSaverActive; }
    }

    public bool IsScreenSaverEnabled
    {
      get { return _isScreenSaverEnabled; }
    }

    public double ScreenSaverTimeoutMin
    {
      get { return _screenSaverTimeOut.TotalMinutes; }
    }

    /// <summary>
    /// Gets the native window handle (HWND). Only available on Windows, returns <see cref="IntPtr.Zero"/> on other platforms.
    /// </summary>
    public IntPtr MainWindowHandle
    {
      get
      {
        try
        {
          INativeWindow native = _window == null ? null : _window.Native;
          if (native != null && native.Win32.HasValue)
            return native.Win32.Value.Hwnd;
        }
        catch (Exception ex)
        {
          ServiceRegistration.Get<ILogger>().Warn("SkinEngine MainWindow: Unable to get the native window handle", ex);
        }
        return IntPtr.Zero;
      }
    }

    public SuspendLevel ApplicationSuspendLevel
    {
      get { return _applicationSuspendLevel; }
      set
      {
        if (_applicationSuspendLevel == value)
          return;
        _applicationSuspendLevel = value;
        ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
        AppSettings settings = settingsManager.Load<AppSettings>();
        settings.SuspendLevel = _applicationSuspendLevel;
        settingsManager.Save(settings);
        UpdateSystemSuspendLevel();
      }
    }

    public IVideoPlayerSynchronizationStrategy VideoPlayerSynchronizationStrategy
    {
      get { return _videoPlayerSynchronizationStrategy; }
      set
      {
        if (_videoPlayerSynchronizationStrategy != null)
        {
          _videoPlayerSynchronizationStrategy.Stop();
          _videoPlayerSynchronizationStrategy.UpdateVideoPlayerState -= UpdateVideoPlayerState;
          _videoPlayerSynchronizationStrategy.SynchronizeToVideoPlayerFramerate -= SynchronizeToVideoPlayerFramerate;
        }
        _videoPlayerSynchronizationStrategy = value;
        if (_videoPlayerSynchronizationStrategy != null)
        {
          _videoPlayerSynchronizationStrategy.UpdateVideoPlayerState += UpdateVideoPlayerState;
          _videoPlayerSynchronizationStrategy.SynchronizeToVideoPlayerFramerate += SynchronizeToVideoPlayerFramerate;
          _videoPlayerSynchronizationStrategy.Start();
        }
      }
    }

    #endregion

    #region IRenderHost implementation

    GRContext IRenderHost.CreateGrContext()
    {
      IGLContext context = _window.GLContext;
      IntPtr GetProc(string name)
      {
        try
        {
          return context.GetProcAddress(name);
        }
        catch
        {
          return IntPtr.Zero;
        }
      }
      GRGlInterface glInterface = GRGlInterface.Create(GetProc);
      if (glInterface == null)
        return null;
      return GRContext.CreateGl(glInterface);
    }

    public IntPtr GetProcAddress(string name)
    {
      try
      {
        return _window.GLContext.TryGetProcAddress(name, out IntPtr result) ? result : IntPtr.Zero;
      }
      catch
      {
        return IntPtr.Zero;
      }
    }

    public int FramebufferWidth
    {
      get { return _window.FramebufferSize.X; }
    }

    public int FramebufferHeight
    {
      get { return _window.FramebufferSize.Y; }
    }

    public int DesktopWidth
    {
      get
      {
        IMonitor monitor = CurrentMonitor;
        return monitor == null ? FramebufferWidth : monitor.Bounds.Size.X;
      }
    }

    public int DesktopHeight
    {
      get
      {
        IMonitor monitor = CurrentMonitor;
        return monitor == null ? FramebufferHeight : monitor.Bounds.Size.Y;
      }
    }

    public double RefreshRate
    {
      get
      {
        IMonitor monitor = CurrentMonitor;
        int rate = monitor == null || !monitor.VideoMode.RefreshRate.HasValue ? 0 : monitor.VideoMode.RefreshRate.Value;
        return rate > 0 ? rate : 60;
      }
    }

    public string RendererDescription
    {
      get { return _rendererDescription; }
    }

    public bool VSync
    {
      set { _window.VSync = value; }
    }

    public int MultiSampleCount
    {
      get { return _multiSampleCount; }
    }

    #endregion

    public void DisposeGraphics()
    {
      ServiceRegistration.Get<ILogger>().Debug("SkinEngine MainWindow: Dispose graphics device");
      GraphicsDevice.Dispose();
    }

    public void Dispose()
    {
      ClipboardManager.Instance.GetTextAccessor = null;
      ClipboardManager.Instance.SetTextAccessor = null;
      if (_input != null)
        _input.Dispose();
      _input = null;
      if (_window != null)
        _window.Dispose();
      _window = null;
    }
  }
}
