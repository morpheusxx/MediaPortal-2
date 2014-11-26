﻿#region Copyright (C) 2007-2012 Team MediaPortal

/*
    Copyright (C) 2007-2012 Team MediaPortal
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
using System.Net;
using MediaPortal.Common;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Extensions.MediaServer.DLNA;
using MediaPortal.Extensions.MediaServer.ResourceAccess;

namespace MediaPortal.Extensions.MediaServer.Objects.MediaLibrary
{
  public class MediaLibraryResource : IDirectoryResource
  {
    private MediaItem Item { get; set; }

    public MediaLibraryResource(MediaItem item)
    {
      Item = item;
    }

    public static string GetLocalIp()
    {
      var localIp = Dns.GetHostName();
      var host = Dns.GetHostEntry(localIp);
      foreach (var ip in host.AddressList)
      {
        if (ip.AddressFamily.ToString() == "InterNetwork")
        {
          localIp = ip.ToString();
        }
      }
      return localIp;
    }

    public static string GetBaseResourceURL()
    {
      var rs = ServiceRegistration.Get<IResourceServer>();
      return "http://" + GetLocalIp() + ":" + rs.PortIPv4;
    }

    public void Initialise()
    {

      var url = GetBaseResourceURL() + DlnaResourceAccessUtils.GetResourceUrl(Item.MediaItemId);

      var dlnaProtocolInfo = DlnaProtocolInfoFactory.GetProfileInfo(Item);
      if (dlnaProtocolInfo != null)
        ProtocolInfo = dlnaProtocolInfo.ToString();

      MediaItemAspect videoAspect;
      if (Item.Aspects.TryGetValue(VideoAspect.ASPECT_ID, out videoAspect))
      {
        Resolution = videoAspect.GetAttributeValue(VideoAspect.ATTR_WIDTH)
                     + "x"
                     + videoAspect.GetAttributeValue(VideoAspect.ATTR_HEIGHT);

        var vidBitRate = Convert.ToInt32(videoAspect.GetAttributeValue(VideoAspect.ATTR_VIDEOBITRATE));
        var audBitRate = Convert.ToInt32(videoAspect.GetAttributeValue(VideoAspect.ATTR_AUDIOBITRATE));
        // TODO: normalize bitrates (video: bit, audio: kbit)
        SetBitrate(vidBitRate + audBitRate, 1);
        SetDuration(Convert.ToInt32(videoAspect.GetAttributeValue(VideoAspect.ATTR_DURATION)));
      }
      MediaItemAspect audioAspect;
      if (Item.Aspects.TryGetValue(AudioAspect.ASPECT_ID, out audioAspect))
      {
        SetDuration(Convert.ToInt32(audioAspect.GetAttributeValue(AudioAspect.ATTR_DURATION)));
        SetBitrate(Convert.ToInt32(audioAspect.GetAttributeValue(AudioAspect.ATTR_BITRATE)));
      }
      MediaItemAspect imageAspect;
      if (Item.Aspects.TryGetValue(ImageAspect.ASPECT_ID, out imageAspect))
      {
        Resolution = imageAspect.GetAttributeValue(ImageAspect.ATTR_WIDTH)
                     + "x"
                     + imageAspect.GetAttributeValue(ImageAspect.ATTR_HEIGHT);
      }

      Uri = url;
    }

    public string Uri { get; set; }

    public ulong Size { get; set; }

    public string Duration { get; set; }

    public uint BitRate { get; set; }

    public uint SampleFrequency { get; set; }

    public uint BitsPerSample { get; set; }

    public uint NumberOfAudioChannels { get; set; }

    public string Resolution { get; set; }

    public uint ColorDepth { get; set; }

    public string ProtocolInfo { get; set; }

    public string Protection { get; set; }

    public string ImportUri { get; set; }

    public string DlnaIfoFileUrl { get; set; }

    protected void SetDuration(int durationInSeconds)
    {
      Duration = TimeSpan.FromSeconds(durationInSeconds).ToString();
    }
    protected void SetBitrate(int kbitPerSecond, int factor = 1000)
    {
      // TODO: it seems like kbits are only treated by factor 1000
      BitRate = (uint)((uint)kbitPerSecond * factor / 8);
    }
  }
}