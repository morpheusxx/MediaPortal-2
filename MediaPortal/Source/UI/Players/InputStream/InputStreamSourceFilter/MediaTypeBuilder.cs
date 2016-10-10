﻿using DirectShow;
using InputStreamSourceFilter.H264;
using MediaPortalWrapper.Streams;
using System;
using System.Runtime.InteropServices;
using MediaPortalWrapper.NativeWrappers;

namespace InputStreamSourceFilter
{
  public class MediaTypeBuilder
  {
    const int FOURCC_H264 = 0x34363248;
    const int FOURCC_AVC1 = 0x31435641;
    static readonly Guid MEDIASUBTYPE_AVC1 = new Guid(0x31435641, 0x0000, 0x0010, 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71);
    static readonly Guid MEDIASUBTYPE_DOLBY_DDPLUS = new Guid("a7fb87af-2d02-42fb-a4d4-05cd93843bdd");
    
    /// <summary>
    /// AnnexB formatted h264 bitstream
    /// </summary>
    /// <param name="streamInfo"></param>
    /// <returns></returns>
    public static AMMediaType H264_AnnexB(InputstreamInfo streamInfo)
    {
      H264CodecData codecData = new H264CodecData(streamInfo.ExtraData);
      SPSUnit spsUnit = new SPSUnit(codecData.SPS);
      int width = spsUnit.Width();
      int height = spsUnit.Height();

      VideoInfoHeader2 vi = new VideoInfoHeader2();
      vi.SrcRect.right = width;
      vi.SrcRect.bottom = height;
      vi.TargetRect.right = width;
      vi.TargetRect.bottom = height;

      int hcf = HCF(width, height);
      vi.PictAspectRatioX = width / hcf;
      vi.PictAspectRatioY = height / hcf;

      vi.BmiHeader.Width = width;
      vi.BmiHeader.Height = height;
      vi.BmiHeader.Planes = 1;
      vi.BmiHeader.Compression = FOURCC_H264;

      AMMediaType amt = new AMMediaType();
      amt.majorType = MediaType.Video;
      amt.subType = MediaSubType.H264;
      amt.temporalCompression = true;
      amt.fixedSizeSamples = false;
      amt.sampleSize = 1;
      amt.SetFormat(vi);
      return amt;
    }

    /// <summary>
    /// AVC1 formatted H264 bitstream
    /// </summary>
    /// <param name="streamInfo"></param>
    /// <returns></returns>
    public static AMMediaType H264_AVC1(InputstreamInfo streamInfo)
    {
      H264CodecData codecData = new H264CodecData(streamInfo.ExtraData);
      SPSUnit spsUnit = new SPSUnit(codecData.SPS);
      int width = spsUnit.Width();
      int height = spsUnit.Height();

      Mpeg2VideoInfo vi = new Mpeg2VideoInfo();
      vi.hdr.SrcRect.right = width;
      vi.hdr.SrcRect.bottom = height;
      vi.hdr.TargetRect.right = width;
      vi.hdr.TargetRect.bottom = height;

      int hcf = HCF(width, height);
      vi.hdr.PictAspectRatioX = width / hcf;
      vi.hdr.PictAspectRatioY = height / hcf;

      vi.hdr.BmiHeader.Width = width;
      vi.hdr.BmiHeader.Height = height;
      vi.hdr.BmiHeader.Planes = 1;
      vi.hdr.BmiHeader.Compression = FOURCC_AVC1;

      vi.dwProfile = (uint)codecData.Profile;
      vi.dwLevel = (uint)codecData.Level;
      vi.dwFlags = (uint)codecData.NALSizeMinusOne + 1;

      byte[] extraData = NaluParser.CreateAVC1ParameterSet(codecData.SPS, codecData.PPS, 2);
      vi.cbSequenceHeader = (uint)extraData.Length;

      AMMediaType amt = new AMMediaType();
      amt.majorType = MediaType.Video;
      amt.subType = MEDIASUBTYPE_AVC1;
      amt.temporalCompression = true;
      amt.fixedSizeSamples = false;
      amt.sampleSize = 1;
      SetFormat(vi, extraData, amt);
      return amt;
    }

    /// <summary>
    /// Currently hardcoded EAC3 media type from sample data.
    /// This needs to be made more generic, it should be possible if we have a properly populated InputStreamInfo
    /// </summary>
    /// <param name="streamInfo"></param>
    /// <returns></returns>
    public static AMMediaType E_AC3(InputstreamInfo streamInfo)
    {
      WaveFormatEx wf = new WaveFormatEx();
      wf.wFormatTag = 8192;
      wf.nChannels = (ushort)streamInfo.Channels; // 6
      wf.nSamplesPerSec = (int)streamInfo.SampleRate; // 48000;
      wf.nAvgBytesPerSec = streamInfo.Bandwidth/8; // 32000;
      wf.nBlockAlign = 24;
      wf.wBitsPerSample = 32;
      wf.cbSize = 0;

      AMMediaType amt = new AMMediaType();
      amt.majorType = MediaType.Audio;
      amt.subType = MEDIASUBTYPE_DOLBY_DDPLUS;
      amt.temporalCompression = false;
      amt.fixedSizeSamples = true;
      amt.sampleSize = streamInfo.Bandwidth; // 256000;
      amt.SetFormat(wf);
      return amt;
    }

    /// <summary>
    /// Sets AMMediaType format data with Mpeg2VideoInfo and optional extra data.
    /// </summary>
    /// <param name="vi"></param>
    /// <param name="extraData"></param>
    /// <param name="amt"></param>
    static void SetFormat(Mpeg2VideoInfo vi, byte[] extraData, AMMediaType amt)
    {
      int cb = Marshal.SizeOf(vi);
      int add = extraData == null || extraData.Length < 4 ? 0 : extraData.Length - 4;
      IntPtr ptr = Marshal.AllocCoTaskMem(cb + add);
      try
      {
        Marshal.StructureToPtr(vi, ptr, false);
        if (extraData != null)
          Marshal.Copy(extraData, 0, ptr + cb - 4, extraData.Length);
        amt.SetFormat(ptr, cb + add);
        amt.formatType = FormatType.Mpeg2Video;
      }
      finally
      {
        Marshal.FreeCoTaskMem(ptr);
      }
    }

    /// <summary>
    /// Finds the Highest Common Factor of 2 ints
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns></returns>
    static int HCF(int x, int y)
    {
      return y == 0 ? x : HCF(y, x % y);
    }
  }
}
