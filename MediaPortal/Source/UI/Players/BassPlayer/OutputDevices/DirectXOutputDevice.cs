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
using System.Collections.Generic;
using System.Threading;
using MediaPortal.Common;
using MediaPortal.Common.Settings;
using MediaPortal.UI.Settings;
using MediaPortal.Extensions.BassLibraries;
using MediaPortal.UI.Players.BassPlayer.Utils;
using ManagedBass;

namespace MediaPortal.UI.Players.BassPlayer.OutputDevices
{
  /// <summary>
  /// Represents the user-selected DirectX outputdevice.
  /// </summary>
  internal class DirectXOutputDevice : AbstractOutputDevice
  {
    #region Fields

    private readonly StreamProcedure _streamWriteProcDelegate;
    private BassStream _outputStream = null;

    #endregion

    public DirectXOutputDevice(Controller controller)
      : base(controller)
    {
      _streamWriteProcDelegate = OutputStreamWriteProc;
      _deviceNo = GetDeviceNo();

      // TODO: move to SetStream?
      DeviceInitFlags flags = DeviceInitFlags.Default;

      // Because all deviceinfo is saved in a static dictionary,
      // we need to determine the latency only once.
      if (!_deviceInfos.ContainsKey(_deviceNo))
        flags |= DeviceInitFlags.Latency;

      bool result = Bass.Init(
          _deviceNo,
          44100, //Only relevant for -> pre-XP (VxD drivers)
          flags,
          IntPtr.Zero);

      Errors? bassInitErrorCode = result ? null : new Errors?(Bass.LastError);

      // If the GetDeviceNo() method returned BassConstants.BassDefaultDevice, we must request the actual device number
      // of the choosen default device
      _deviceNo = Bass.CurrentDevice;

      if (bassInitErrorCode.HasValue)
      {
        if (bassInitErrorCode.Value == Errors.Already)
        {
          Bass.CurrentDevice = _deviceNo;
          if (Bass.LastError != Errors.OK)
            throw new BassLibraryException("BASS_SetDevice");
          bassInitErrorCode = null;
        }
      }

      if (bassInitErrorCode.HasValue)
        throw new BassLibraryException("BASS_Init", bassInitErrorCode.Value);

      CollectDeviceInfo(_deviceNo);

      int ms = Convert.ToInt32(Controller.GetSettings().DirectSoundBufferSizeMilliSecs);

      if (!Bass.Configure(Configuration.PlaybackBufferLength, ms))
        throw new BassLibraryException("BASS_SetConfig");

      // Enable update thread while the output device is active
      if (!Bass.Configure(Configuration.UpdatePeriod, ms / 4))
        throw new BassLibraryException("BASS_SetConfig");
    }

    #region IDisposable Members

    public override void Dispose()
    {
      base.Dispose();

      Log.Debug("Disposing output stream");

      _outputStream?.Dispose();
      _outputStream = null;

      Log.Debug("Resetting global Bass environment");

      if (!Bass.Configure(Configuration.UpdatePeriod, 0))
        throw new BassLibraryException("BASS_SetConfig");

      Bass.CurrentDevice = _deviceNo;
      if (Bass.LastError != Errors.OK)
        throw new BassLibraryException("BASS_SetDevice");

      if (!Bass.Free())
        throw new BassLibraryException("BASS_Free");

      Bass.CurrentDevice = BassConstants.BassNoSoundDevice;
      if (Bass.LastError != Errors.OK)
        throw new BassLibraryException("BASS_SetDevice");
    }

    #endregion

    #region IOutputDevice Members

    public override void SetInputStream(BassStream stream, bool passThrough)
    {
      if (_deviceState != DeviceState.Stopped)
        throw new BassPlayerException("Device state is not 'DeviceState.Stopped'");

      _inputStream = stream;

      Log.Debug("Creating output stream");

      const BassFlags flags = BassFlags.Float;
      int handle = Bass.CreateStream(
          _inputStream.SampleRate,
          _inputStream.Channels,
          flags,
          _streamWriteProcDelegate,
          IntPtr.Zero);

      if (handle == BassConstants.BassInvalidHandle)
        throw new BassLibraryException("BASS_StreamCreate");

      _outputStream = BassStream.Create(handle);

      if (passThrough)
        _fader = new BassStreamFader(_inputStream, TimeSpan.FromMilliseconds(Controller.GetSettings().FadeDurationMilliSecs));

      ResetState();
    }

    public override void Start()
    {
      if (_deviceState == DeviceState.Started)
        return;

      Log.Debug("Starting output");
      _deviceState = DeviceState.Started;

      if (!Bass.ChannelPlay(_outputStream.Handle, false))
        throw new BassLibraryException("BASS_ChannelPlay");
    }

    public override void Stop()
    {
      if (_deviceState == DeviceState.Stopped)
        return;

      Log.Debug("Stopping output");
      _deviceState = DeviceState.Stopped;

      if (!Bass.ChannelStop(_outputStream.Handle))
        throw new BassLibraryException("BASS_ChannelStop");
    }

    public override void ClearBuffers()
    {
      Bass.ChannelSetPosition(_outputStream.Handle, 0L);
    }

    public override int Volume
    {
      get
      {
        return _volume;
      }
      set
      {
        // value is from 0 to 100 in a linear scale, internal _volume is from 0 to 10000 in a linear scale
        _volume = value;
        var bassVolume = _volume * 100;
        Bass.Configure(Configuration.GlobalMusicVolume, bassVolume);
        Bass.Configure(Configuration.GlobalSampleVolume, bassVolume);
        Bass.Configure(Configuration.GlobalStreamVolume, bassVolume);
      }
    }

    public override bool Mute
    {
      get { return _isMuted; }
      set
      {
        if (value)
        {
          Bass.Configure(Configuration.GlobalMusicVolume, 0);
          Bass.Configure(Configuration.GlobalSampleVolume, 0);
          Bass.Configure(Configuration.GlobalStreamVolume, 0);
        }
        else
        {
          var bassVolume = _volume * 100;
          Bass.Configure(Configuration.GlobalMusicVolume, bassVolume);
          Bass.Configure(Configuration.GlobalSampleVolume, bassVolume);
          Bass.Configure(Configuration.GlobalStreamVolume, bassVolume);
        }
        _isMuted = value;
      }
    }

    #endregion

    #region Private members

    /// <summary>
    /// Retrieves information on a device and adds it to the static deviceinfo dictionary do it can be reused later. 
    /// </summary>
    /// <param name="deviceNo">Device number to retrieve information on.</param>
    private void CollectDeviceInfo(int deviceNo)
    {
      // Device info is saved in a dictionary so it can be reused lateron.
      if (!_deviceInfos.ContainsKey(deviceNo))
      {
        Log.Debug("Collecting device info");

        ManagedBass.DeviceInfo bassDeviceInfo;
        if (!Bass.GetDeviceInfo(deviceNo, out bassDeviceInfo))
          throw new BassLibraryException("BASS_GetDeviceInfo");

        BassInfo bassInfo;
        if (!Bass.GetInfo(out bassInfo))
          throw new BassLibraryException("BASS_GetInfo");

        DeviceInfo deviceInfo = new DeviceInfo
        {
          Name = bassDeviceInfo.Name,
          Driver = bassDeviceInfo.Driver,
          Channels = bassInfo.SpeakerCount,
          MinRate = bassInfo.MinSampleRate,
          MaxRate = bassInfo.MaxSampleRate,
          Latency = TimeSpan.FromMilliseconds(bassInfo.Latency)
        };

        lock (_deviceInfos)
          _deviceInfos.Add(deviceNo, deviceInfo);
      }
      Log.Debug("DirectSound device info: {0}", _deviceInfos[_deviceNo].ToString());
    }

    /// <summary>
    /// Gets the device number for the selected DirectSound device.
    /// </summary>
    /// <returns>Number of the device to be used for the BASS player.</returns>
    private static int GetDeviceNo()
    {
      // On Windows the device of the BASS player settings is used, on the other platforms the common audio output setting
      string deviceName = OperatingSystem.IsWindows() ?
          Controller.GetSettings().DirectSoundDevice : ServiceRegistration.Get<ISettingsManager>().Load<AudioOutputSettings>().AudioDevice;
      int deviceNo = BassConstants.BassDefaultDevice;

      List<string> deviceNames = new List<string>();
      List<int> deviceNumbers = new List<int>();
      for (int i = 0; i < Bass.DeviceCount; i++)
      {
        ManagedBass.DeviceInfo bassDeviceInfo;
        if (Bass.GetDeviceInfo(i, out bassDeviceInfo) && bassDeviceInfo.IsEnabled)
        {
          deviceNames.Add(bassDeviceInfo.Name);
          deviceNumbers.Add(i);
        }
      }

      if (String.IsNullOrEmpty(deviceName))
        Log.Info("Initializing default audio device");
      else
      {
        int index = AudioDeviceMatcher.Find(deviceName, deviceNames);
        if (index >= 0)
        {
          deviceNo = deviceNumbers[index];
          Log.Info("Initializing audio device '{0}' (device no {1})", deviceNames[index], deviceNo);
        }
        else
          Log.Warn("Specified audio device '{0}' does not exist or is ambiguous. Initializing default audio device. Available devices: {1}", deviceName,
              string.Join("; ", deviceNames));
      }
      return deviceNo;
    }

    /// <summary>
    /// Blocks the current thread until our output device has finished and then calls
    /// <see cref="PlaybackProcessor.HandleOutputStreamEnded"/>.
    /// </summary>
    protected internal override void WaitAndHandleOutputEnd_Sync()
    {
      DateTime timeout = DateTime.Now + CurrentDeviceInfo.Latency + TimeSpan.FromSeconds(1);
      BassStream stream = _outputStream;
      if (stream != null)
        while (Bass.ChannelIsActive(stream.Handle) != PlaybackState.Stopped && DateTime.Now < timeout)
          Thread.Sleep(10);
      base.WaitAndHandleOutputEnd_Sync();
    }

    /// <summary>
    /// Callback function for the outputstream.
    /// </summary>
    /// <param name="streamHandle">Bass stream handle that requests sample data.</param>
    /// <param name="buffer">Buffer to write the sampledata in.</param>
    /// <param name="requestedBytes">Requested number of bytes.</param>
    /// <param name="userData"></param>
    /// <returns>Number of bytes read.</returns>
    private int OutputStreamWriteProc(int streamHandle, IntPtr buffer, int requestedBytes, IntPtr userData)
    {
      return WriteOutputStream(buffer, requestedBytes);
    }

    #endregion
  }
}
