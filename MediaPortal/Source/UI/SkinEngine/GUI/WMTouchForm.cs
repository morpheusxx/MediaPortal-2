// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Control.InputManager;

namespace MediaPortal.UI.SkinEngine.GUI
{
  // Base class for multi-touch aware form.
  // Receives touch notifications through Windows messages and converts them
  // to touch events TouchDown, TouchUp and Touchmove.
  public class WMTouchForm : Form
  {
    ///////////////////////////////////////////////////////////////////////
    // Public interface

    // Constructor
    [SecurityPermission(SecurityAction.Demand)]
    public WMTouchForm()
    {
      // Setup handlers
      Load += OnLoadHandler;
      _touchInputSize = Marshal.SizeOf(new TouchInput());
    }

    ///////////////////////////////////////////////////////////////////////
    // Protected members, for derived classes.

    // Touch event handlers
    protected event EventHandler<TouchDownEvent> TouchDown;   // touch down event handler
    protected event EventHandler<TouchUpEvent> TouchUp;     // touch up event handler
    protected event EventHandler<TouchMoveEvent> TouchMove;   // touch move event handler

    ///////////////////////////////////////////////////////////////////////
    // Private class definitions, structures, attributes and native fn's
    // Touch event window message constants [winuser.h]
    private const int WM_TOUCHMOVE = 0x0240;
    private const int WM_TOUCHDOWN = 0x0241;
    private const int WM_TOUCHUP = 0x0242;

    // Touch API defined structures [winuser.h]
    [StructLayout(LayoutKind.Sequential)]
    private struct TouchInput
    {
      public int X;
      public int Y;
      public IntPtr Source;
      public int dwID;
      public TouchEventFlags dwFlags;
      public TouchInputMask dwMask;
      public int dwTime;
      public IntPtr dwExtraInfo;
      public int cxContact;
      public int cyContact;
    }

    // Currently touch/multitouch access is done through unmanaged code
    // We must p/invoke into user32 [winuser.h]
    [DllImport("user32", CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterTouchWindow(IntPtr hWnd, ulong ulFlags);

    [DllImport("user32", CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTouchInputInfo(IntPtr hTouchInput, int cInputs, [In, Out] TouchInput[] pInputs, int cbSize);

    [DllImport("user32", CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern void CloseTouchInputHandle(IntPtr lParam);

    // Attributes
    private readonly int _touchInputSize;        // size of TouchInput structure

    ///////////////////////////////////////////////////////////////////////
    // Private methods

    // OnLoad window event handler: Registers the form for multi-touch input.
    // in:
    //      sender      object that has sent the event
    //      e           event arguments
    private void OnLoadHandler(Object sender, EventArgs e)
    {
      const ulong ulFlags = 0;
      if (!RegisterTouchWindow(Handle, ulFlags))
      {
        ServiceRegistration.Get<ILogger>().Error("Could not register window for handling touch events");
      }
    }

    // Window procedure. Receives WM_ messages.
    // Translates WM_TOUCH window messages to touch events.
    // Normally, touch events are sufficient for a derived class,
    // but the window procedure can be overriden, if needed.
    // in:
    //      m       message
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    protected override void WndProc(ref Message m)
    {
      // Decode and handle WM_TOUCH* message.
      bool handled;
      switch (m.Msg)
      {
        case WM_TOUCHDOWN:
        case WM_TOUCHMOVE:
        case WM_TOUCHUP:
          handled = DecodeTouch(ref m);
          break;
        default:
          handled = false;
          break;
      }

      // Call parent WndProc for default message processing.
      base.WndProc(ref m);

      if (handled)
        m.Result = new IntPtr(1);
    }

    // Extracts lower 16-bit word from an 32-bit int.
    // in:
    //      number      int
    // returns:
    //      lower word
    private static int LoWord(int number)
    {
      return number & 0xffff;
    }

    // Decodes and handles WM_TOUCH* messages.
    // Unpacks message arguments and invokes appropriate touch events.
    // in:
    //      m           window message
    // returns:
    //      flag whether the message has been handled
    private bool DecodeTouch(ref Message m)
    {
      // More than one touchinput may be associated with a touch message,
      // so an array is needed to get all event information.
      int inputCount = LoWord(m.WParam.ToInt32()); // Number of touch inputs, actual per-contact messages

      TouchInput[] inputs = new TouchInput[inputCount];

      // Unpack message parameters into the array of TouchInput structures, each
      // representing a message for one single contact.
      if (!GetTouchInputInfo(m.LParam, inputCount, inputs, _touchInputSize))
      {
        // Get touch info failed.
        return false;
      }

      // For each contact, dispatch the message to the appropriate message
      // handler.
      // Note that for WM_TOUCHDOWN you can get down & move notifications
      // and for WM_TOUCHUP you can get up & move notifications
      // WM_TOUCHMOVE will only contain move notifications
      // and up & down notifications will never come in the same message
      bool handled = false; // // Flag, is message handled
      for (int i = 0; i < inputCount; i++)
      {
        TouchInput ti = inputs[i];
        // Assign a handler to this message.
        if (ti.dwFlags.HasFlag(TouchEventFlags.Down) && TouchDown != null)
        {
          TouchDown(this, GetEvent<TouchDownEvent>(ti));
          handled = true;
        }
        else if (ti.dwFlags.HasFlag(TouchEventFlags.Up) && TouchUp != null)
        {
          TouchUp(this, GetEvent<TouchUpEvent>(ti));
          handled = true;
        }
        else if (ti.dwFlags.HasFlag(TouchEventFlags.Move) && TouchMove != null)
        {
          TouchMove(this, GetEvent<TouchMoveEvent>(ti));
          handled = true;
        }
      }

      CloseTouchInputHandle(m.LParam);

      return handled;
    }

    // Convert the raw touchinput message into a touchevent.
    private TE GetEvent<TE>(TouchInput ti) where TE : TouchEvent, new()
    {
      // TOUCHINFO point coordinates and contact size is in 1/100 of a pixel; convert it to pixels.
      // Also convert screen to client coordinates.
      TE te = new TE { ContactY = ti.cyContact / 100, ContactX = ti.cxContact / 100, Id = ti.dwID, Time = ti.dwTime, Mask = ti.dwMask, Flags = ti.dwFlags };
      Point pt = PointToClient(new Point(ti.X / 100, ti.Y / 100));
      te.LocationX = pt.X;
      te.LocationY = pt.Y;
      return te;
    }
  }
}
