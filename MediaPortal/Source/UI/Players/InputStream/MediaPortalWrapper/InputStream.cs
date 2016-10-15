using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MediaPortalWrapper.NativeWrappers;
using MediaPortalWrapper.Streams;
using MediaPortalWrapper.Utils;

namespace MediaPortalWrapper
{
  public class InputStream : AbstractStream
  {
    public const string KEY_INPUTSTREAM_ADDON = "inputstreamaddon";
    public const string KEY_INPUTSTREAM_LIC_TYPE = "inputstream.mpd.license_type";
    public const string KEY_INPUTSTREAM_LIC_URL = "inputstream.mpd.license_key";

    public const string INPUTSTREAM_ADDON_MPD = "inputstream.mpd";
    public const string INPUTSTREAM_ADDON_SMOOTH = "inputstream.smoothstream";

    public struct StreamPreferences
    {
      public int? Width;
      public int? Height;
      public string ThreeLetterLangCode;
      public bool PreferMultiChannel;
    }

    public InputstreamInfo VideoStream
    {
      get { return _inputstreamInfos.Values.FirstOrDefault(i => _enabledStreams.Contains((int)i.StreamId) && i.StreamType == StreamType.Video); }
    }

    public InputstreamInfo AudioStream
    {
      get { return _inputstreamInfos.Values.FirstOrDefault(i => _enabledStreams.Contains((int)i.StreamId) && i.StreamType == StreamType.Audio); }
    }

    public List<InputstreamInfo> AudioStreams
    {
      get { return _inputstreamInfos.Values.Where(i => i.StreamType == StreamType.Audio).ToList(); }
    }

    public InputStreamAddonFunctions Functions { get { lock (_syncObj) return _addonFunctions; } }
    public InputstreamCapabilities Caps { get { return _caps; } }

    private readonly DllAddonWrapper<InputStreamAddonFunctions> _wrapper;
    private Dictionary<uint, InputstreamInfo> _inputstreamInfos;
    private readonly InputStreamAddonFunctions _addonFunctions;
    private readonly StreamPreferences _preferences;
    private List<int> _enabledStreams;
    private readonly Dictionary<DemuxPacket, IntPtr> _packets = new Dictionary<DemuxPacket, IntPtr>();
    private readonly InputstreamCapabilities _caps;

    public InputStream(string streamUrl, Dictionary<string, string> addonProperties, StreamPreferences preferences)
    {
      string addonName;
      if (!addonProperties.TryGetValue(KEY_INPUTSTREAM_ADDON, out addonName))
        throw new ArgumentException("Missing inputstreamaddon key", "addonProperties");

      _preferences = preferences;
      _wrapper = new DllAddonWrapper<InputStreamAddonFunctions>();
      // TODO: fix path structure for temporary folders
      var pluginRoot = Path.GetDirectoryName(GetType().Assembly.Location);
      // Add to windows DLL search path to find widevine dll
      var res = NativeMethods.SetDllDirectory(pluginRoot);
      var addonDllPath = Path.Combine(pluginRoot, string.Format("{0}\\{0}.dll", addonName));
      _wrapper.Init(addonDllPath);
      var cb = new AddonCB { LibPath = pluginRoot };

      var status = _wrapper.Create(ref cb, IntPtr.Zero);
      if (status != AddonStatus.Ok)
        throw new Exception("Failed to create addon.");

      _addonFunctions = _wrapper.Addon;

      var inputStreamConfig = new InputStreamConfig
      {
        Url = streamUrl,
        LibFolder = Path.Combine(cb.LibPath, "cdm"),
        ProfileFolder = cb.LibPath,
        Properties = new ListItemProperty[InputStreamConfig.MAX_INFO_COUNT]
      };

      int idx = 0;
      foreach (var addonProperty in addonProperties)
      {
        if (addonProperty.Key == KEY_INPUTSTREAM_ADDON)
          continue;

        inputStreamConfig.Properties[idx++] = new ListItemProperty(addonProperty.Key, addonProperty.Value);
      }
      inputStreamConfig.CountInfoValues = (uint)idx;

      if (preferences.Width.HasValue && preferences.Height.HasValue)
        Functions.SetVideoResolution(preferences.Width.Value, preferences.Height.Value);

      Functions.Open(ref inputStreamConfig);

      IntPtr capsPtr = Functions.GetCapabilities();
      if (capsPtr != IntPtr.Zero)
        _caps = Marshal.PtrToStructure<InputstreamCapabilities>(capsPtr);

      OnStreamChange();
    }

    public override void Dispose()
    {
      Functions.Close();
      _wrapper.Dispose();
    }


    private void OnStreamChange()
    {
      UpdateStreams();

      GetPreferredStreams(_inputstreamInfos, _preferences);

      // Tell the inputstream to enable selected stream IDs
      EnableStreams();
    }

    public void EnableStream(int streamId, bool isEnabled)
    {
      bool changed = false;
      // Keep list in sync
      if (isEnabled && !_enabledStreams.Contains(streamId))
      {
        _enabledStreams.Add(streamId);
        changed = true;
      }
      if (!isEnabled && _enabledStreams.Contains(streamId))
      {
        _enabledStreams.Remove(streamId);
        changed = true;
      }

      if (changed)
        Functions.EnableStream(streamId, isEnabled);
    }

    private void EnableStreams()
    {
      foreach (var inputstreamInfo in _inputstreamInfos)
        Functions.EnableStream((int)inputstreamInfo.Key, _enabledStreams.Contains((int)inputstreamInfo.Key));
    }

    private void UpdateStreams()
    {
      InputstreamIds ids = Functions.GetStreamIds();

      List<InputstreamInfo> streamInfos = new List<InputstreamInfo>();
      unsafe
      {
        for (int i = 0; i < ids.StreamCount; i++)
        {
          var info = Functions.GetStream((int)ids.StreamIds[i]);
          streamInfos.Add(info);
          Logger.Log("Stream {1}:", i, info);

          //byte[] extraData = info.ExtraData;
          //Logger.Log(" - ExtraData: {0}", BitConverter.ToString(extraData));
        }
      }

      _inputstreamInfos = streamInfos.ToDictionary(s => s.StreamId);
    }

    private void GetPreferredStreams(Dictionary<uint, InputstreamInfo> inputstreamInfos, StreamPreferences preferences)
    {
      List<int> selectedIds = new List<int>();
      // Video
      var videoStreamId = inputstreamInfos.Values.FirstOrDefault(i => i.StreamType == StreamType.Video).StreamId;
      if (videoStreamId != 0)
        selectedIds.Add((int)videoStreamId);

      // Audio, prefer language then multichannel
      var audioStreams = inputstreamInfos.Values.Where(i => i.StreamType == StreamType.Audio).ToList();
      var langStreams = audioStreams.Where(i => i.Language == preferences.ThreeLetterLangCode).ToList();

      // Prefer matching language, then all languages
      foreach (var streams in new[] { langStreams, audioStreams })
      {
        var matchingStreams = preferences.PreferMultiChannel ?
          streams.OrderByDescending(i => i.Channels).ThenBy(i => i.CodecInternalName) :
          streams.OrderBy(i => i.Channels).ThenBy(i => i.CodecInternalName);

        var audioStream = matchingStreams.Any() ? matchingStreams.First().StreamId : 0;
        if (audioStream != 0)
        {
          selectedIds.Add((int)audioStream);
          break;
        }
      }
      _enabledStreams = selectedIds.ToList();
    }

    public override void Write(DemuxPacket packet)
    {
      throw new NotImplementedException();
    }

    public override DemuxPacket Read()
    {
      IntPtr demuxPacketPtr = Functions.DemuxRead();
      // If there is no more data, DemuxRead returns 0
      if (demuxPacketPtr == IntPtr.Zero)
        return new DemuxPacket { StreamId = 0 }; // EOS indicator

      DemuxPacket demuxPacket = Marshal.PtrToStructure<DemuxPacket>(demuxPacketPtr);

      if (demuxPacket.StreamId == Constants.DMX_SPECIALID_STREAMCHANGE || demuxPacket.StreamId == Constants.DMX_SPECIALID_STREAMINFO)
      {
        OnStreamChange();
        // Directly read next packet
        return Read();
      }

      lock (_syncObj)
        _packets[demuxPacket] = demuxPacketPtr;
      return demuxPacket;
    }

    public override void Free(DemuxPacket packet)
    {
      lock (_syncObj)
      {
        IntPtr demuxPacketPtr;
        if (_packets.TryGetValue(packet, out demuxPacketPtr))
        {
          DemuxPacketHelper.FreeDemuxPacket(demuxPacketPtr);
          _packets.Remove(packet);
        }
      }
    }
  }
}
