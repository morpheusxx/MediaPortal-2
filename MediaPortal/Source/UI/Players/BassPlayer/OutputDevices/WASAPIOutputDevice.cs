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
using System.Linq;
using MediaPortal.Extensions.BassLibraries;
using MediaPortal.UI.Players.BassPlayer.Utils;
using MediaPortal.UI.Presentation.Players;
using ManagedBass;
using ManagedBass.Mix;
using ManagedBass.Wasapi;

namespace MediaPortal.UI.Players.BassPlayer.OutputDevices
{
  /// <summary>
  /// Represents the user-selected WASAPI outputdevice.
  /// </summary>
  internal class WASAPIOutputDevice : AbstractOutputDevice, IAudioPlayerAnalyze
  {
    #region Fields

    protected const BassFlags MIXER_FLAGS = BassFlags.MixerNonStop | BassFlags.Float | BassFlags.MixerNoRampin | BassFlags.Decode;

    protected readonly WasapiProcedure _streamWriteProcDelegate;
    protected readonly SyncProcedure _onPlaybackEndDelegate;
    protected WasapiInitFlags _flags;
    protected BassStream _mixer;
    protected int _mixerHandle;
    protected readonly int _maxFFT = (int)(DataFlags.Available | DataFlags.FFT4096);
    WasapiVolumeTypes _bassWASAPIVolScope = WasapiVolumeTypes.Session;

    #endregion

    public WASAPIOutputDevice(Controller controller)
      : base(controller)
    {
      _streamWriteProcDelegate = OutputStreamWriteProc;
      _onPlaybackEndDelegate = OnPlaybackEnd;
      _deviceNo = GetDeviceNo();
    }

    #region IDisposable Members

    public override void Dispose()
    {
      base.Dispose();

      Log.Debug("Disposing mixer stream");

      if (_mixer != null)
      {
        _mixer.Dispose();
        _mixer = null;
      }

      Log.Debug("Resetting global Bass environment");

      if (!Bass.Configure(Configuration.UpdatePeriod, 0))
        throw new BassLibraryException("BASS_SetConfig");

      if (!BassWasapi.Free())
        throw new BassLibraryException("BASS_WASAPI_Free");
    }

    #endregion

    #region IOutputDevice Members

    /// <summary>
    /// If the device is used in shared mode, we use a mixer. In this case it will be the stream to read from.
    /// </summary>
    protected override BassStream ReadStream
    {
      get { return _mixer ?? _inputStream; }
    }

    public override void SetInputStream(BassStream stream, bool passThrough)
    {
      if (_deviceState != DeviceState.Stopped)
        throw new BassPlayerException("Device state is not 'DeviceState.Stopped'");

      _inputStream = stream;
      _flags = WasapiInitFlags.AutoFormat | WasapiInitFlags.Buffer;

      // If Exclusive mode is used, check, if that would be supported, otherwise init in shared mode
      bool isExclusive = Controller.GetSettings().WASAPIExclusiveMode;

      if (isExclusive)
      {
        _flags |= WasapiInitFlags.Exclusive;

        WasapiFormat wasapiFormat = BassWasapi.CheckFormat(_deviceNo,
                                                                           _inputStream.SampleRate,
                                                                           _inputStream.Channels,
                                                                           WasapiInitFlags.Exclusive);
        if (wasapiFormat == WasapiFormat.Unknown)
        {
          Log.Info("BASS: WASAPI exclusive mode not directly supported for samplerate of {0} and {1} channels", _inputStream.SampleRate, _inputStream.Channels);
          isExclusive = false;
        }
      }

    retry:
      if (!isExclusive)
      {
        Log.Debug("BASS: Init WASAPI shared mode with Event driven system enabled.");
        _flags &= ~WasapiInitFlags.Exclusive;
        _flags |= WasapiInitFlags.Shared | WasapiInitFlags.EventDriven;
      }

      Log.Debug("BASS: Try to init WASAPI with a samplerate of {0} and {1} channels", _inputStream.SampleRate, _inputStream.Channels);

      
      float buffer = (float)Controller.GetSettings().PlaybackBufferSizeMilliSecs / 1000;

      bool result = BassWasapi.Init(_deviceNo, _inputStream.SampleRate, _inputStream.Channels, _flags, buffer, 0f, _streamWriteProcDelegate, IntPtr.Zero);

      Errors? bassInitErrorCode = result ? null : new Errors?(Bass.LastError);

      if (bassInitErrorCode.HasValue)
      {
        if (bassInitErrorCode.Value == Errors.Already)
        {
          BassWasapi.CurrentDevice = _deviceNo;
          if (Bass.LastError != Errors.OK)
            throw new BassLibraryException("BASS_WASAPI_SetDevice");
        }
        else if (isExclusive)
        {
          // Allow one retry in shared mode
          Log.Warn("BASS: Failed to initialize WASAPI exclusive mode for samplerate of {0} and {1} channels. Trying fallback to shared mode.", _inputStream.SampleRate, _inputStream.Channels);
          isExclusive = false;
          goto retry;
        }
        else
          throw new BassLibraryException("BASS_WASAPI_Init");
      }

      // If the GetDeviceNo() method returned BassConstants.BassDefaultDevice, we must request the actual device number
      // of the choosen default device
      _deviceNo = BassWasapi.CurrentDevice;

      CollectDeviceInfo(_deviceNo);
      WasapiInfo wasapiInfo = BassWasapi.Info;

      Log.Debug("BASS: ---------------------------------------------");
      Log.Debug("BASS: Buffer Length: {0}", wasapiInfo.BufferLength);
      Log.Debug("BASS: Channels: {0}", wasapiInfo.Channels);
      Log.Debug("BASS: Frequency: {0}", wasapiInfo.Frequency);
      Log.Debug("BASS: Format: {0}", wasapiInfo.Format.ToString());
      Log.Debug("BASS: InitFlags: {0}", wasapiInfo.InitFlags.ToString());
      Log.Debug("BASS: Exclusive: {0}", wasapiInfo.IsExclusive.ToString());
      Log.Debug("BASS: ---------------------------------------------");
      Log.Info("BASS: WASAPI Device successfully initialised");

      // For shared mode we require a mixer to change the sampling rates of input stream to device output stream.
      if (!wasapiInfo.IsExclusive)
      {
        // Recreate Mixer with new value
        Log.Debug("BASS: Creating new {0} channel mixer for frequency {1}", wasapiInfo.Channels, wasapiInfo.Frequency);
        _mixerHandle = BassMix.CreateMixerStream(wasapiInfo.Frequency, wasapiInfo.Channels, MIXER_FLAGS);
        if (_mixerHandle == BassConstants.BassInvalidHandle)
          throw new BassLibraryException("BASS_Mixer_StreamCreate");
        _mixer = BassStream.Create(_mixerHandle);
        AttachStream();
      }

      int ms = Convert.ToInt32(Controller.GetSettings().DirectSoundBufferSizeMilliSecs);

      if (!Bass.Configure(Configuration.PlaybackBufferLength, ms))
        throw new BassLibraryException("BASS_SetConfig");

      // Enable update thread while the output device is active
      if (!Bass.Configure(Configuration.UpdatePeriod, ms / 4))
        throw new BassLibraryException("BASS_SetConfig");

      if (passThrough)
        _fader = new BassStreamFader(_inputStream, TimeSpan.FromMilliseconds(Controller.GetSettings().FadeDurationMilliSecs));

      // Init volume/mute mode
      _bassWASAPIVolScope = isExclusive ? WasapiVolumeTypes.Device : WasapiVolumeTypes.Session;

      ResetState();
    }

    /// <summary>
    /// Attach a stream to the Mixer
    /// </summary>
    /// <returns></returns>
    public bool AttachStream()
    {
      try
      {
        Bass.ChannelLock(_mixerHandle, true);

        RegisterStreamFreedEvent(_inputStream);

        bool result = BassMix.MixerAddChannel(_mixerHandle, _inputStream.Handle,
                                          BassFlags.MixerNoRampin | BassFlags.MixerBuffer |
                                          BassFlags.MixerMatrix | BassFlags.MixerDownMix |
                                          BassFlags.AutoFree
                                          );
        if (!result)
          throw new BassLibraryException("BASS_Mixer_StreamAddChannel");

        return true;
      }
      finally
      {
        Bass.ChannelLock(_mixerHandle, false);
      }
    }

    /// <summary>
    /// Register the Stream Freed Event
    /// </summary>
    /// <returns></returns>
    private void RegisterStreamFreedEvent(BassStream stream)
    {
      int syncHandle = Bass.ChannelSetSync(stream.Handle, SyncFlags.Free | SyncFlags.Mixtime, 0, _onPlaybackEndDelegate, IntPtr.Zero);

      if (syncHandle == 0)
        Log.Debug("BASS: RegisterStreamFreedEvent of stream {0} failed with error {1}", stream.Handle, Enum.GetName(typeof(Errors), Bass.LastError));
      else
        stream.SyncProcHandles.Add(syncHandle);
    }

    private void OnPlaybackEnd(int handle, int channel, int data, IntPtr user)
    {
      // Our input stream is finished, wait for device to end playback
      HandleOutputStreamAboutToEnd();
    }

    /// <summary>
    /// Detect the supported output formats for WASAPI
    /// </summary>
    private void GetDeviceFormats(int deviceNo, DeviceInfo deviceInfo)
    {
      int[] channels = { 1, 2, 3, 4, 5, 6, 7, 8 };
      int[] sampleRates = { 22050, 32000, 44100, 48000, 88200, 96000, 192000 };

      deviceInfo.MaxRate = sampleRates.First();
      deviceInfo.MinRate = sampleRates.Last();
      foreach (WasapiInitFlags init in new[] { WasapiInitFlags.Exclusive, WasapiInitFlags.Shared })
      {
        Log.Debug("Check {0} mode", init);
        foreach (var sampleRate in sampleRates)
        {
          foreach (var channel in channels)
          {
            WasapiFormat format = BassWasapi.CheckFormat(deviceNo, sampleRate, channel, init);
            if (format != WasapiFormat.Unknown)
            {
              Log.Debug("- {0,-6} Hz / {1} ch: {2}", sampleRate, channel, format);
              if (channel > deviceInfo.Channels)
                deviceInfo.Channels = channel;
              if (sampleRate > deviceInfo.MaxRate)
                deviceInfo.MaxRate = sampleRate;
              if (sampleRate < deviceInfo.MinRate)
                deviceInfo.MinRate = sampleRate;
            }
          }
        }
      }
    }

    public override void Start()
    {
      if (_deviceState == DeviceState.Started)
        return;

      Log.Debug("Starting output");
      _deviceState = DeviceState.Started;

      if (!BassWasapi.Start())
        throw new BassLibraryException("BASS_WASAPI_Start");
    }

    public override void Stop()
    {
      if (_deviceState == DeviceState.Stopped)
        return;

      Log.Debug("Stopping output");
      _deviceState = DeviceState.Stopped;

      if (!BassWasapi.Stop(true))
        throw new BassLibraryException("BASS_WASAPI_Stop");
    }


    public override int Volume
    {
      get
      {
        return _volume;
      }
      set
      {
        // value is from 0 to 100 in a linear scale, internal _volume is from 0.0 to 1.0 in a linear scale
        _volume = value;
        var bassVolume = _volume / 100f;
        BassWasapi.SetVolume(WasapiVolumeTypes.LinearCurve, bassVolume);
      }
    }

    public override bool Mute
    {
      get
      {
        return BassWasapi.GetMute(_bassWASAPIVolScope);
      }
      set
      {
        BassWasapi.SetMute(_bassWASAPIVolScope, value);
        if (!value)
        {
          var bassVolume = _volume / 100f;
          BassWasapi.SetVolume(WasapiVolumeTypes.LinearCurve, bassVolume);
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
    protected void CollectDeviceInfo(int deviceNo)
    {
      // Device info is saved in a dictionary so it can be reused lateron.
      if (!_deviceInfos.ContainsKey(deviceNo))
      {
        Log.Debug("Collecting device info");

        WasapiDeviceInfo bassDeviceInfo;
        if (!BassWasapi.GetDeviceInfo(deviceNo, out bassDeviceInfo))
          throw new BassLibraryException("BASS_WASAPI_GetDeviceInfo");

        WasapiInfo bassInfo;
        if (!BassWasapi.GetInfo(out bassInfo))
          throw new BassLibraryException("BASS_WASAPI_GetInfo");

        DeviceInfo deviceInfo = new DeviceInfo
          {
            Name = bassDeviceInfo.Name,
            Driver = "WASAPI",
            Channels = bassInfo.Channels,
            MinRate = bassInfo.Frequency,
            MaxRate = bassInfo.Frequency,
          };

        GetDeviceFormats(deviceNo, deviceInfo);
        lock (_deviceInfos)
          _deviceInfos.Add(deviceNo, deviceInfo);
      }
      Log.Debug("WASAPI device info: {0}", _deviceInfos[_deviceNo].ToString());
    }

    /// <summary>
    /// Checks if the given <paramref name="deviceInfo"/> represents a valid, currently available output device.
    /// </summary>
    /// <param name="deviceInfo">Device info</param>
    /// <returns><c>true</c> if valid.</returns>
    public static bool IsValidDevice(WasapiDeviceInfo deviceInfo)
    {
      return !(deviceInfo.IsDisabled || deviceInfo.IsUnplugged || deviceInfo.IsInput || deviceInfo.IsLoopback);
    }

    /// <summary>
    /// Gets the device number for the selected DirectSound device.
    /// </summary>
    /// <returns>Number of the device to be used for the BASS player.</returns>
    protected static int GetDeviceNo()
    {
      string deviceName = Controller.GetSettings().WASAPIDevice;
      int deviceNo = BassConstants.BassDefaultDevice;

      if (String.IsNullOrEmpty(deviceName))
        Log.Info("Initializing default WASAPI device");
      else
      {
        bool found = false;
        for (int i = 0; i < BassWasapi.DeviceCount; i++)
        {
          WasapiDeviceInfo deviceInfo;
          if (!BassWasapi.GetDeviceInfo(i, out deviceInfo))
            continue;

          // Skip input devices, they have same name as output devices.
          if (!IsValidDevice(deviceInfo))
            continue;

          if (deviceInfo.Name == deviceName)
          {
            deviceNo = i;
            found = true;
            break;
          }
        }
        if (found)
          Log.Info("Initializing WASAPI device '{0}' (device no {1})", deviceName, deviceNo);
        else
          Log.Warn("Specified WASAPI device '{0}' does not exist. Initializing default WASAPI device", deviceName);
      }
      return deviceNo;
    }

    /// <summary>
    /// Callback function for the outputstream.
    /// </summary>
    /// <param name="buffer">Bass stream handle that requests sample data.</param>
    /// <param name="requestedBytes">Requested number of bytes.</param>
    /// <param name="userData"></param>
    /// <returns>Number of bytes read.</returns>
    protected int OutputStreamWriteProc(IntPtr buffer, int requestedBytes, IntPtr userData)
    {
      return WriteOutputStream(buffer, requestedBytes);
    }

    #endregion

    #region IAudioPlayerAnalyze Member

    public bool GetWaveData32(int length, out float[] waveData32)
    {
      waveData32 = null;
      if (!BassWasapi.IsStarted)
        return false;
      waveData32 = new float[length];
      return BassWasapi.GetData(waveData32, length) == (int)Errors.OK;
    }

    public bool GetFFTData(float[] fftDataBuffer)
    {
      if (!BassWasapi.IsStarted)
        return false;
      return BassWasapi.GetData(fftDataBuffer, _maxFFT) > 0;
    }

    public bool GetFFTFrequencyIndex(int frequency, out int frequencyIndex)
    {
      frequencyIndex = 0;
      if (_inputStream == null)
        return false;
      frequencyIndex = BassMath.FFTFrequency2Index(frequency, 4096, _inputStream.SampleRate);
      return true;
    }

    public bool GetChannelLevel(out double dbLevelL, out double dbLevelR)
    {
      dbLevelL = 0f;
      dbLevelR = 0f;
      if (!BassWasapi.IsStarted)
        return false;
      int level = BassWasapi.GetLevel();
      dbLevelL = BassMath.LevelToDB(BassMath.LowWord32(level), 65535); // the left level
      dbLevelR = BassMath.LevelToDB(BassMath.HighWord32(level), 65535); // the right level
      return true;
    }

    #endregion
  }
}
