using System;
using System.Collections.Generic;
using System.Linq;
using DirectShow;
using DirectShow.BaseClasses;
using DirectShow.Helper;
using MediaPortalWrapper.Streams;
using System.Runtime.InteropServices;
using InputStreamSourceFilter.Extensions;
using MediaPortalWrapper;
using MediaPortalWrapper.NativeWrappers;

namespace InputStreamSourceFilter
{
  /// <summary>
  /// Source Filter that parses an AbstractStream containing elementary video and audio streams.
  /// Currently the supported media types are hard coded to the media types of the sample data.
  /// </summary>
  public class StreamSourceFilter : BaseSourceFilterTemplate<StreamFileParser>
  {
    protected StreamFileParser _streamParser;

    public StreamSourceFilter(InputStream stream)
      : base("InputStreamSourceFilter")
    {
      _streamParser = (StreamFileParser)m_Parsers[0];
      _streamParser.SetSource(stream);
      m_sFileName = "http://localhost/InputStream";
      //Load a dummy file
      Load(m_sFileName, null);
    }

    protected override HRESULT InitializeOutputPins()
    {
      if (m_pFileParser == null) return E_UNEXPECTED;
      int nAdded = 0;
      int nCount = m_pFileParser.Count;
      int[] indexes = new int[(int)DemuxTrack.TrackType.Subtitles + 1];
      bool[] useIndexes = new bool[indexes.Length];
      for (int i = 0; i < indexes.Length; i++)
      {
        indexes[i] = 0;
        useIndexes[i] = m_pFileParser.GetTracksCountByType((DemuxTrack.TrackType)i) > 1;
      }
      for (int i = 0; i < nCount; i++)
      {
        DemuxTrack track = m_pFileParser[i];
        if (track != null && track.IsTrackValid)
        {
          string name = track.Name;
          if (string.IsNullOrEmpty(name))
          {
            name = "Unknown";
            switch (track.Type)
            {
              case DemuxTrack.TrackType.Video:
                name = "Video";
                break;
              case DemuxTrack.TrackType.Audio:
                name = "Audio";
                break;
              case DemuxTrack.TrackType.Subtitles:
                name = "Subtitles";
                break;
              case DemuxTrack.TrackType.SubPicture:
                name = "Sub Picture";
                break;
            }
            int type = (int)track.Type;
            if (type >= 0 && type < indexes.Length)
            {
              if (useIndexes[type])
              {
                name += " " + indexes[type]++;
              }
            }
          }
          if (track.Enabled)
          {
            AddPin(new SplitterOutputPin(track, name, this));
            nAdded++;
          }
        }
      }
      return nAdded == 0 ? VFW_E_NO_ACCEPTABLE_TYPES : NOERROR;
    }

    #region IAMStreamSelect Members

    public override int Count(out int pcStreams)
    {
      pcStreams = 0;
      if (m_pFileParser == null) return VFW_E_NOT_CONNECTED;
      pcStreams = _streamParser.SelectableTracks.Count;
      return NOERROR;
    }

    public override int Info(int lIndex, IntPtr ppmt, IntPtr pdwFlags, IntPtr plcid, IntPtr pdwGroup, IntPtr ppszName, IntPtr ppObject, IntPtr ppUnk)
    {
      if (lIndex >= _streamParser.SelectableTracks.Count)
        return S_FALSE;

      var selected = _streamParser.SelectableTracks[lIndex];

      if (ppmt != IntPtr.Zero)
      {
        AMMediaType mt = new AMMediaType();
        if (S_OK == selected.GetMediaType(0, ref mt))
        {
          IntPtr pmt = Marshal.AllocCoTaskMem(Marshal.SizeOf(mt));
          Marshal.StructureToPtr(mt, pmt, true);
          Marshal.WriteIntPtr(ppmt, pmt);
        }
        else
        {
          Marshal.WriteIntPtr(ppmt, IntPtr.Zero);
        }
      }
      if (pdwFlags != IntPtr.Zero)
      {
        if (!selected.Enabled)
        {
          Marshal.WriteInt32(pdwFlags, 0);
        }
        else
        {
          Marshal.WriteInt32(pdwFlags, (int)AMStreamSelectInfoFlags.Enabled);
        }
      }
      if (plcid != IntPtr.Zero)
      {
        int lcid = selected.LCID;
        if (lcid == 0)
        {
          lcid = LOCALE_NEUTRAL;
        }
        Marshal.WriteInt32(plcid, lcid);
      }
      if (pdwGroup != IntPtr.Zero)
      {
        if (selected.Type == DemuxTrack.TrackType.Audio)
          Marshal.WriteInt32(pdwGroup, 1);
        else if (selected.Type == DemuxTrack.TrackType.Subtitles)
          Marshal.WriteInt32(pdwGroup, 2);
        else
          Marshal.WriteInt32(pdwGroup, 0);
      }
      if (ppszName != IntPtr.Zero)
      {
        string name = selected.Name;
        if (string.IsNullOrEmpty(name))
        {
          name = "Audio #" + lIndex;
        }
        Marshal.WriteIntPtr(ppszName, Marshal.StringToCoTaskMemUni(name));
      }
      if (ppObject != IntPtr.Zero)
      {
        Marshal.WriteIntPtr(ppObject, Marshal.GetIUnknownForObject(selected));
      }
      if (ppUnk != IntPtr.Zero)
      {
        Marshal.WriteIntPtr(ppUnk, IntPtr.Zero);
      }
      return NOERROR;
    }

    public override int Enable(int lIndex, AMStreamSelectEnableFlags dwFlags)
    {
      for (int index = 0; index < _streamParser.SelectableTracks.Count; index++)
      {
        var track = _streamParser.SelectableTracks[index];

        bool isEnabled = (
                           index == lIndex && dwFlags == AMStreamSelectEnableFlags.Enable || // the current index should be enabled
                           dwFlags == AMStreamSelectEnableFlags.EnableAll // all should be enabled
                         ) && dwFlags != AMStreamSelectEnableFlags.DisableAll; // must not be "Disable All"

        _streamParser.InputStream.EnableStream(track.StreamId, isEnabled);
      }

      if (IsActive && dwFlags != AMStreamSelectEnableFlags.DisableAll)
      {
        try
        {
          IMediaSeeking seeking = (IMediaSeeking)FilterGraph;
          if (seeking != null)
          {
            long current;
            seeking.GetCurrentPosition(out current);
            // Only seek during playback, not on initial selection
            if (current != 0)
            {
              current -= UNITS / 10;
              seeking.SetPositions(current, AMSeekingSeekingFlags.AbsolutePositioning, null, AMSeekingSeekingFlags.NoPositioning);
              current += UNITS / 10;
              seeking.SetPositions(current, AMSeekingSeekingFlags.AbsolutePositioning, null, AMSeekingSeekingFlags.NoPositioning);
            }
          }
        }
        catch
        {
        }
      }
      return NOERROR;
    }

    #endregion

  }

  public class StreamFileParser : FileParser
  {
    protected InputStream _stream;
    protected Dictionary<int, MediaTypedDemuxTrack> _trackMap = new Dictionary<int, MediaTypedDemuxTrack>();

    public List<MediaTypedDemuxTrack> Tracks { get { return m_Tracks.OfType<MediaTypedDemuxTrack>().ToList(); } }
    public List<MediaTypedDemuxTrack> SelectableTracks { get { return Tracks.Where(t => t.Type == DemuxTrack.TrackType.Audio || t.Type == DemuxTrack.TrackType.Subtitles).ToList(); } }
    public InputStream InputStream { get { return _stream; } }

    public void SetSource(InputStream stream)
    {
      _stream = stream;
    }

    protected override HRESULT CheckFile()
    {
      //We loaded a dummy file, just return OK
      return S_OK;
    }

    protected void AddIndexedTrack(MediaTypedDemuxTrack track)
    {
      m_Tracks.Add(track);
      _trackMap[track.StreamId] = track;
    }

    protected override HRESULT LoadTracks()
    {
      //Initialise the tracks, these create our output pins
      AMMediaType mediaType;
      if (MediaTypeBuilder.TryGetType(_stream.VideoStream, out mediaType))
        AddIndexedTrack(new MediaTypedDemuxTrack(this, DemuxTrack.TrackType.Video, mediaType, (int)_stream.VideoStream.StreamId));

      foreach (InputstreamInfo audioStream in _stream.AudioStreams)
      {
        if (MediaTypeBuilder.TryGetType(audioStream, out mediaType))
        {
          var track = new MediaTypedDemuxTrack(this, DemuxTrack.TrackType.Audio, mediaType, (int)audioStream.StreamId)
          {
            LCID = audioStream.Language.TryGetLCID(),
            Enabled = audioStream.StreamId == _stream.AudioStream.StreamId,
            Active = audioStream.StreamId == _stream.AudioStream.StreamId
          };
          AddIndexedTrack(track);
        }
      }

      m_rtDuration = _stream.Functions.GetTotalTime().ToDS();
      return S_OK;
    }

    //This is called by a separate thread repeatedly to fill each tracks packet cache
    public override HRESULT ProcessDemuxPackets()
    {
      DemuxPacket demuxPacket = _stream.Read();
      //EOS
      if (demuxPacket.StreamId == 0)
        return S_FALSE;

      if (demuxPacket.StreamId == _stream.VideoStream.StreamId || demuxPacket.StreamId == _stream.AudioStream.StreamId)
      {
        //Create the packet and add the data
        PacketData packet = new PacketData();
        byte[] buffer = new byte[demuxPacket.Size];
        Marshal.Copy(demuxPacket.Data, buffer, 0, buffer.Length);
        packet.Buffer = buffer;
        packet.Size = buffer.Length;

        MediaTypedDemuxTrack track;
        if (_trackMap.TryGetValue(demuxPacket.StreamId, out track))
        {
          if (track.Type == DemuxTrack.TrackType.Video)
          {
            // Set video timestamps
            packet.Start = demuxPacket.Dts.ToDS();
            packet.Stop = demuxPacket.Duration.ToDS();
          }
          // Queue samples
          track.AddToCache(ref packet);
        }
      }

      _stream.Free(demuxPacket);
      return S_OK;
    }

    public override HRESULT SeekToTime(long time)
    {
      double startPts = 0d;
      _stream.Functions.DemuxSeekTime(time.ToMS(), false, ref startPts);
      return base.SeekToTime(time);
    }
  }

  /// <summary>
  /// Generic DemuxTrack that has a specified media type
  /// </summary>
  public class MediaTypedDemuxTrack : DemuxTrack
  {
    private readonly AMMediaType _pmt;
    // Contains the underlying input stream ID.
    private readonly int _streamId;

    public int StreamId { get { return _streamId; } }

    public MediaTypedDemuxTrack(FileParser parser, TrackType type, AMMediaType pmt, int streamId)
      : base(parser, type)
    {
      _pmt = pmt;
      _streamId = streamId;
    }

    public override HRESULT GetMediaType(int iPosition, ref AMMediaType pmt)
    {
      if (iPosition == 0)
      {
        pmt.Set(_pmt);
        return NOERROR;
      }
      return VFW_S_NO_MORE_ITEMS;
    }
  }
}
