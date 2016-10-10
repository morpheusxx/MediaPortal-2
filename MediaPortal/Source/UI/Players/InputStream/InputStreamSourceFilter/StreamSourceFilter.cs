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
    public StreamSourceFilter(AbstractStream stream, InputstreamInfo videoInfo, InputstreamInfo audioInfo)
      : base("InputStreamSourceFilter")
    {
      ((StreamFileParser)m_Parsers[0]).SetSource(stream, videoInfo, audioInfo);
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
    protected AbstractStream _stream;
    InputstreamInfo _videoInfo;
    InputstreamInfo _audioInfo;
    //This is a hack at the moment, I think it will require different handling for seeking
    protected long _lastVideoTime = 0;

    public void SetSource(AbstractStream stream, InputstreamInfo videoInfo, InputstreamInfo audioInfo)
    {
      _stream = stream;
      _videoInfo = videoInfo;
      _audioInfo = audioInfo;
    }

    protected override HRESULT CheckFile()
    {
      //We loaded a dummy file, just return OK
      return S_OK;
    }

    protected override HRESULT LoadTracks()
    {
      //Initialise the tracks, these create our output pins
      m_Tracks.Add(new MediaTypedDemuxTrack(this, DemuxTrack.TrackType.Video, MediaTypeBuilder.H264_AVC1(_videoInfo)));
      m_Tracks.Add(new MediaTypedDemuxTrack(this, DemuxTrack.TrackType.Audio, MediaTypeBuilder.E_AC3(_audioInfo)));
      return S_OK;
    }

    //This is called by a separate thread repeatedly to fill each tracks packet cache
    public override HRESULT ProcessDemuxPackets()
    {
      DemuxPacket demuxPacket = _stream.Read();
      //EOS?
      if (demuxPacket.StreamId == 0)
        return S_FALSE;

      if (demuxPacket.StreamId == _videoInfo.StreamId || demuxPacket.StreamId == _audioInfo.StreamId)
      {
        //Create the packet and add the data
        //PacketData packet = new DemuxPacketData(_stream, demuxPacket);
        PacketData packet = new PacketData();
        byte[] buffer = new byte[demuxPacket.Size];
        Marshal.Copy(demuxPacket.Data, buffer, 0, buffer.Length);
        packet.Buffer = buffer;
        packet.Size = buffer.Length;

        if (demuxPacket.StreamId == _videoInfo.StreamId)
        {
          //Just set start to straight after last packet, might need adjusting to get seeking working
          packet.Start = _lastVideoTime;
          //demuxPacket.Duration seems to actually be stop time, so calculate actual duration (stop - start)
          _lastVideoTime += (long)((demuxPacket.Duration - demuxPacket.Dts) * 10);
          packet.Stop = _lastVideoTime;
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

    public override HRESULT SeekToTime(long _time)
    {
      InputStream stream = _stream as InputStream;
      if (stream != null)
      {
        double startPts = 0d;
        stream.Functions.DemuxSeekTime((int)_time, false, ref startPts);
        _lastVideoTime = (long)startPts;
        return S_OK;
      }
      return base.SeekToTime(_time);
    }
  }

  /// <summary>
  /// Generic DemuxTrack that has a specified media type
  /// </summary>
  class MediaTypedDemuxTrack : DemuxTrack
  {
    readonly AMMediaType _pmt;

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
