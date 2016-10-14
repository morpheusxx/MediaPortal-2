#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
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
using System.Globalization;
using MediaPortal.Common;
using MediaPortal.Common.Localization;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Settings;
using MediaPortal.UI.Players.Video.Settings;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.SkinEngine.SkinManagement;
using MediaPortalWrapper;

namespace MediaPortal.UI.Players.InputStreamPlayer
{
  public class PlayerBuilder : IPlayerBuilder
  {
    #region Protected fields

    protected string _pluginDirectory = null;

    #endregion

    #region IPlayerBuilder implementation

    public IPlayer GetPlayer(MediaItem mediaItem)
    {
      string mimeType;
      string title;
      if (!mediaItem.GetPlayData(out mimeType, out title))
        return null;
      IResourceLocator locator = mediaItem.GetResourceLocator();
      if (mimeType == "xxx/inputstream")
      {
        InputStreamPlayer player = new InputStreamPlayer();
        try
        {
          InitOnline(player);
          player.SetMediaItem(locator, title);
        }
        catch (Exception ex)
        {
          ServiceRegistration.Get<ILogger>().Error("Error playing media item '{0}'", ex, locator);
          player.Dispose();
          return null;
        }
        return player;
      }
      return null;
    }

    public void InitOnline(InputStreamPlayer player)
    {
      string streamUrl = "http://s3.lvlt.dash.us.aiv-cdn.net/d/1$AOAGZA014O5RE,18A35628/videoquality$1080p/prod/65c3/a3e2/e5f7/4c03-9e12-3abc2687696b/430c2983-0b7d-40e1-ac55-cf05c6fc6f97_corrected.mpd";
      string licUrl = "https://atv-ext.amazon.com/cdp/catalog/GetPlaybackResources?asin=B003CWTIO2&deviceTypeID=AOAGZA014O5RE&firmware=1&customerID=A2P1L2KM4EMNQ&deviceID=794318f3b7764ddd31f86682754f7cec57d172b4476a79ceb84dc67b&marketplaceID=ATVPDKIKX0DER&token=a04fe77cb5b638e160bfc1a861eed28f&format=json&version=1&resourceUsage=ImmediateConsumption&consumptionType=Streaming&deviceDrmOverride=CENC&deviceStreamingTechnologyOverride=DASH&deviceProtocolOverride=Http&audioTrackId=all&videoMaterialType=Feature&desiredResources=Widevine2License";
      Dictionary<string, string> addonProperties = new Dictionary<string, string>
      {
        { InputStream.KEY_INPUTSTREAM_ADDON, InputStream.INPUTSTREAM_ADDON_MPD },
        { InputStream.KEY_INPUTSTREAM_LIC_TYPE, "com.widevine.alpha" },
        { InputStream.KEY_INPUTSTREAM_LIC_URL, licUrl }
      };

      var videoSettings = ServiceRegistration.Get<ISettingsManager>().Load<VideoSettings>();
      var regionSettings = ServiceRegistration.Get<ISettingsManager>().Load<RegionSettings>();
      CultureInfo culture = CultureInfo.CurrentUICulture;
      try
      {
        if (!string.IsNullOrEmpty(regionSettings.Culture))
          culture = CultureInfo.CreateSpecificCulture(regionSettings.Culture);
      }
      catch { }

      // Prefer video in screen resolution
      var height = SkinContext.CurrentDisplayMode.Height;
      var width  = SkinContext.CurrentDisplayMode.Width;

      InputStream.StreamPreferences preferences = new InputStream.StreamPreferences
      {
        Width = width,
        Height = height,
        ThreeLetterLangCode = culture.ThreeLetterISOLanguageName,
        PreferMultiChannel = videoSettings.PreferMultiChannelAudio
      };

      InputStream onlineSource = new InputStream(streamUrl, addonProperties, preferences);

      player.InitStream(onlineSource);
    }

    #endregion
  }
}
