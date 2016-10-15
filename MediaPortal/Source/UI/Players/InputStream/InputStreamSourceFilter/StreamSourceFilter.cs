﻿using System;
using System.Globalization;
using System.Linq;
using DirectShow;
using DirectShow.BaseClasses;
using DirectShow.Helper;
using MediaPortalWrapper.Streams;
using System.Runtime.InteropServices;
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
    public StreamSourceFilter(InputStream stream)
      : base("InputStreamSourceFilter")
    {
      ((StreamFileParser)m_Parsers[0]).SetSource(stream);
      m_sFileName = "http://localhost/InputStream";
      //Load a dummy file
      Load(m_sFileName, null);
    }
  }

  //public class DemuxPacketData : PacketData
  //{
  //  private readonly AbstractStream _source;
  //  private readonly DemuxPacket _packet;

  //  public DemuxPacketData()
  //  {
  //  }

  //  public DemuxPacketData(AbstractStream source, DemuxPacket packet)
  //  {
  //    _source = source;
  //    _packet = packet;
  //  }

  //  public override void Dispose()
  //  {
  //    // TODO: freeing the packet here is too early and causes AccessViolation
  //    //_source.Free(_packet);
  //  }
  //}

  public class StreamFileParser : FileParser
  {
    protected InputStream _stream;

    public void SetSource(InputStream stream)
    {
      _stream = stream;
    }

    protected override HRESULT CheckFile()
    {
      //We loaded a dummy file, just return OK
      return S_OK;
    }

    public static CultureInfo FromISOName(string name)
    {
      return CultureInfo
          .GetCultures(CultureTypes.NeutralCultures)
          .FirstOrDefault(c => string.Equals(c.ThreeLetterISOLanguageName, name, StringComparison.OrdinalIgnoreCase));
    }

    public static int TryGetLCID(string name)
    {
      var culture = FromISOName(name);
      return culture != null ? culture.LCID : 0;
    }

    protected override HRESULT LoadTracks()
    {
      //Initialise the tracks, these create our output pins
      AMMediaType mediaType;
      if (MediaTypeBuilder.TryGetType(_stream.VideoStream, out mediaType))
        m_Tracks.Add(new MediaTypedDemuxTrack(this, DemuxTrack.TrackType.Video, mediaType));

      // TODO: this is intended for IAMStreamSelect, but adding all audio tracks here creates an filter output pin for each track
      foreach (InputstreamInfo audioStream in _stream.AudioStreams/*.Where(s => s.StreamId == _stream.AudioStream.StreamId)*/)
      {
        if (MediaTypeBuilder.TryGetType(audioStream, out mediaType))
        {
          var track = new MediaTypedDemuxTrack(this, DemuxTrack.TrackType.Audio, mediaType);
          track.LCID = TryGetLCID(audioStream.Language);
          track.Active = track.Enabled = audioStream.StreamId == _stream.AudioStream.StreamId;
          m_Tracks.Add(track);
        }
      }

      m_rtDuration = ToDS(_stream.Functions.GetTotalTime());
      return S_OK;
    }

    /// <summary>
    /// Converts DirectShow hundreds nanoseconds to milliseconds.
    /// </summary>
    /// <param name="dsTime">DirectShow time</param>
    /// <returns>Milliseconds</returns>
    public int ToMS(long dsTime)
    {
      return (int)(dsTime / 10000);
    }

    /// <summary>
    /// Converts milliseconds to DirectShow hundreds nanoseconds.
    /// </summary>
    /// <param name="msTime">Milliseconds</param>
    /// <returns>DirectShow time</returns>
    public long ToDS(int msTime)
    {
      return (long)msTime * 10000;
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
        //PacketData packet = new DemuxPacketData(_stream, demuxPacket);
        PacketData packet = new PacketData();
        byte[] buffer = new byte[demuxPacket.Size];
        Marshal.Copy(demuxPacket.Data, buffer, 0, buffer.Length);
        packet.Buffer = buffer;
        packet.Size = buffer.Length;

        if (demuxPacket.StreamId == _stream.VideoStream.StreamId)
        {
          // DVD Time to DirectShow (10^7 to 10^8)
          packet.Start = (long)(demuxPacket.Dts * 10);
          packet.Stop = (long)(demuxPacket.Duration * 10);
          //Queue video packet
          m_Tracks[0].AddToCache(ref packet);
        }
        else
        {
          //Queue audio packet, audio doesn't need timestamps
          m_Tracks[1].AddToCache(ref packet);
        }
      }

      // TODO: freeing packet here works, but there seems to be another leak
      _stream.Free(demuxPacket);
      return S_OK;
    }

    public override HRESULT SeekToTime(long time)
    {
      double startPts = 0d;
      _stream.Functions.DemuxSeekTime(ToMS(time), false, ref startPts);
      return base.SeekToTime(time);
    }
  }

  /// <summary>
  /// Generic DemuxTrack that has a specified media type
  /// </summary>
  class MediaTypedDemuxTrack : DemuxTrack
  {
    private readonly AMMediaType _pmt;

    public MediaTypedDemuxTrack(FileParser parser, TrackType type, AMMediaType pmt)
      : base(parser, type)
    {
      _pmt = pmt;
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
