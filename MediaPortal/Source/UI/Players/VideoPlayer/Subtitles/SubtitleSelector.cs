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
using System.Diagnostics;
using System.Runtime.InteropServices;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.UI.Players.Video.Interfaces;

namespace MediaPortal.UI.Players.Video.Subtitles
{
  public enum SubtitleType
  {
    Teletext = 0,
    Bitmap = 1,
    None
  }

  // TODO: Have an AUTO subtitle option!

  public class SubtitleOption
  {
    public SubtitleType Type;
    public TeletextPageEntry Entry; // only for teletext
    public int BitmapIndex; // index among bitmap subs, only for bitmap subs :)
    public string Language;
    public bool IsAuto;

    public override string ToString()
    {
      switch (Type)
      {
        case SubtitleType.Bitmap:
          return "Bitmap Lang " + Language;
        case SubtitleType.Teletext:
          return "Teletext Lang\t" + Entry.Language + "\tpage: " + Entry.Page;
        case SubtitleType.None:
          return "None";
        default:
          return "???";
      }
    }

    public override int GetHashCode()
    {
      return base.GetHashCode();
    }

    public override bool Equals(object o)
    {
      if (!(o is SubtitleOption))
        return false;
      SubtitleOption other = (SubtitleOption)o;
      if (other.Type != Type) return false;
      if (other.BitmapIndex != BitmapIndex) return false;
      if (!other.Language.Equals(Language)) return false;
      if ((Entry != null && !Entry.Equals(other.Entry)) || Entry == null && other.Entry != null) return false;
      return true;
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  struct SubtitleStream
  {
    public int Pid;
    public int SubtitleType;
    public byte Lang0, Lang1, Lang2;
    public byte TermChar;
  }

  class SubtitleSelector
  {
    private readonly SubtitleOption _autoSelectOption;
    private delegate int SubtitleStreamEventCallback(int count, IntPtr pOpts, ref int bitmapindex);
    private SubtitleStreamEventCallback _subStreamCallback;
    private readonly object _syncLock = new object();

    private readonly ISubtitleStream _dvbStreams;
    private readonly SubtitleRenderer _subRender;
    private readonly List<string> _preferedLanguages;
    private int _lastSubtitleIndex;
    private SubtitleOption _currentOption;
    private readonly Dictionary<int, TeletextPageEntry> _pageEntries;

    private readonly List<SubtitleOption> _bitmapSubtitleCache;

    public SubtitleSelector(ISubtitleStream dvbStreams, SubtitleRenderer subRender, TeletextSubtitleDecoder subDecoder)
    {
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: SubtitleSelector ctor");
      if (subRender == null)
      {
        throw new Exception("Nullpointer input not allowed ( SubtitleRenderer)");
      }
      else
      {
        this._dvbStreams = dvbStreams;
        this._subRender = subRender;
      }

      // load preferences
      //using (MediaPortal.Profile.Settings reader = new MediaPortal.Profile.Settings(MediaPortal.Configuration.Config.GetFile(MediaPortal.Configuration.Config.Dir.Config, "MediaPortal.xml")))
      //{
      //  preferedLanguages = new List<string>();
      //  string languages = reader.GetValueAsString("tvservice", "preferredsublanguages", "");
      //  ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: SubtitleSelector: sublangs entry content: " + languages);
      //  StringTokenizer st = new StringTokenizer(languages, ";");
      //  while (st.HasMore)
      //  {
      //    string lang = st.NextToken();
      //    if (lang.Length != 3)
      //    {
      //      ServiceRegistration.Get<ILogger>().Warn("Language {0} is not in the correct format!", lang);
      //    }
      //    else
      //    {
      //      preferedLanguages.Add(lang);
      //      ServiceRegistration.Get<ILogger>().Info("Prefered language {0} is {1}", preferedLanguages.Count, lang);
      //    }
      //  }
      //}

      // TODO: Access MP2 VideoSettings.PreferredSubtitleLanguage ?
      _preferedLanguages = new List<string>();

      _pageEntries = new Dictionary<int, TeletextPageEntry>();

      _bitmapSubtitleCache = new List<SubtitleOption>();

      lock (_syncLock)
      {
        if (subDecoder != null)
        {
          subDecoder.SetPageInfoCallback(OnPageInfo);
        }

        if (dvbStreams != null)
        {
          RetrieveBitmapSubtitles();
          _subStreamCallback = OnSubtitleReset;
          IntPtr pSubStreamCallback = Marshal.GetFunctionPointerForDelegate(_subStreamCallback);
          ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Calling SetSubtitleStreamEventCallback");
          dvbStreams.SetSubtitleResetCallback(pSubStreamCallback);
        }

        if (_preferedLanguages.Count > 0)
        {
          _autoSelectOption = new SubtitleOption
          {
            Language = "None",
            IsAuto = true,
            Type = SubtitleType.None
          };

          SetOption(0); // the autoselect mode will have index 0 (ugly)
        }
      }
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: End SubtitleSelector ctor");
    }

    // ONLY call from MP main thread!
    private void RetrieveBitmapSubtitles()
    {
      _bitmapSubtitleCache.Clear();

      try
      {
        // collect dvb bitmap subtitle options
        int streamCount = 0;
        _dvbStreams.GetSubtitleStreamCount(ref streamCount);
        Debug.Assert(streamCount >= 0 && streamCount <= 100);

        for (int i = 0; i < streamCount; i++)
        {
          var subLang = new SubtitleLanguage();
          _dvbStreams.GetSubtitleStreamLanguage(i, ref subLang);
          SubtitleOption option = new SubtitleOption
          {
            Type = SubtitleType.Bitmap,
            Language = subLang.lang,
            BitmapIndex = i
          };
          _bitmapSubtitleCache.Add(option);
          ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Retrieved bitmap option Lang : " + option);
        }
      }
      catch (Exception e)
      {
        ServiceRegistration.Get<ILogger>().Error(e);
      }
    }



    private int OnSubtitleReset(int count, IntPtr pOpts, ref int selected_bitmap_index)
    {
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: OnSubtitleReset");
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: selected_bitmap_index " + selected_bitmap_index);
      lock (_syncLock)
      {
        _bitmapSubtitleCache.Clear();
        _pageEntries.Clear();

        ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Number of bitmap options {0}", count);
        IntPtr current = pOpts;
        for (int i = 0; i < count; i++)
        {
          ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Bitmap index " + i);
          SubtitleStream bOpt = (SubtitleStream)Marshal.PtrToStructure(current, typeof(SubtitleStream));
          SubtitleOption opt = new SubtitleOption
          {
            BitmapIndex = i,
            Type = SubtitleType.Bitmap,
            Language = "" + (char)bOpt.Lang0 + (char)bOpt.Lang1 + (char)bOpt.Lang2
          };
          ServiceRegistration.Get<ILogger>().Debug(opt.ToString());
          _bitmapSubtitleCache.Add(opt);
          current = (IntPtr)(((int)current) + Marshal.SizeOf(bOpt));
        }

        selected_bitmap_index = -1; // we didnt select a bitmap index

        if (_currentOption != null && _currentOption.IsAuto)
        {
          SubtitleOption prefered = CheckForPreferedLanguage();
          if (prefered != null)
          {
            _currentOption.BitmapIndex = prefered.BitmapIndex;
            _currentOption.Entry = prefered.Entry;
            _currentOption.Language = prefered.Language;
            _currentOption.Type = prefered.Type;
            ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Auto-selection of " + _currentOption);
          }
          else
          {
            _currentOption.Language = "None";
            _currentOption.Type = SubtitleType.None;
          }

          _subRender.SetSubtitleOption(_currentOption);
          if (_currentOption.Type == SubtitleType.Bitmap)
          {
            selected_bitmap_index = _currentOption.BitmapIndex;
            ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Returns selected_bitmap_index == {0} to ISubStream", selected_bitmap_index);
          }
        }

      }
      return 0;
    }

    private void OnPageInfo(TeletextPageEntry entry)
    {
      lock (_syncLock)
      {
        if (!_pageEntries.ContainsKey(entry.Page))
        {
          _pageEntries.Add(entry.Page, entry);
          if (_currentOption != null && _currentOption.IsAuto)
          {
            SubtitleOption prefered = CheckForPreferedLanguage();
            if (prefered != null)
            {
              _currentOption.BitmapIndex = prefered.BitmapIndex;
              _currentOption.Entry = prefered.Entry;
              _currentOption.Language = prefered.Language;
              _currentOption.Type = prefered.Type;
              ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Auto-selection of " + _currentOption);
            }
            else
            {
              _currentOption.Type = SubtitleType.None;
              _currentOption.Language = "None";
            }

            _subRender.SetSubtitleOption(_currentOption);
            // we cannot update the bitmap sub stream here
          }
        }
      }
    }

    /// <summary>
    /// Attempts to auto choose a subtitle option
    /// based on the prefered languages
    /// </summary>
    private SubtitleOption CheckForPreferedLanguage()
    {
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: SubtitleSelector: CheckForPreferedLanguage");
      List<SubtitleOption> options = CollectOptions();
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Has {0} options", options.Count);

      SubtitleOption prefered = null;
      int priority = int.MaxValue;
      int prefOptIndex = -1;

      for (int optIndex = 1; optIndex < options.Count; optIndex++)
      {
        SubtitleOption opt = options[optIndex];
        int index = _preferedLanguages.IndexOf(opt.Language);
        ServiceRegistration.Get<ILogger>().Debug(opt + " Pref index " + index);

        if (index >= 0 && index < priority)
        {
          ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Setting as pref");
          prefered = opt;
          priority = index;
          prefOptIndex = optIndex;
        }
      }
      return prefered;
    }

    private List<SubtitleOption> CollectOptions()
    {
      //ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: SubtitleSelector: CollectOptions");
      List<SubtitleOption> options = new List<SubtitleOption>();

      if (_autoSelectOption != null)
      {
        options.Add(_autoSelectOption);
      }

      options.AddRange(_bitmapSubtitleCache);

      // collect teletext options
      foreach (KeyValuePair<int, TeletextPageEntry> p in _pageEntries)
      {
        SubtitleOption option = new SubtitleOption
        {
          Type = SubtitleType.Teletext,
          Language = p.Value.Language,
          Entry = p.Value
        };
        options.Add(option);
        ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Added Teletext option Lang : " + option);
      }
      return options;
    }


    public int CountOptions()
    {
      return CollectOptions().Count;
    }

    public int GetCurrentOption()
    {
      return _lastSubtitleIndex;
    }

    /// <summary>
    /// Call only on MP main thread
    /// </summary>
    /// <param name="index"></param>
    public void SetOption(int index)
    {
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: SetOption {0}", index);
      List<SubtitleOption> options = CollectOptions();
      if (index >= options.Count)
      {
        ServiceRegistration.Get<ILogger>().Error("SetOption with too large index!");
        return;
      }
      SubtitleOption option = options[index];
      _lastSubtitleIndex = index;
      _currentOption = option;

      if (option.IsAuto)
      {
        ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector:  Set autoselect mode");
        SubtitleOption prefered = CheckForPreferedLanguage();
        if (prefered != null)
        {
          option.BitmapIndex = prefered.BitmapIndex;
          option.Entry = prefered.Entry;
          option.Language = prefered.Language;
          option.Type = prefered.Type;
          ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Auto-selection of " + option);
        }
        else
        {
          option.Type = SubtitleType.None;
          _currentOption.Language = "None";
          _subRender.SetSubtitleOption(option);
        }
      }

      if (option.Type == SubtitleType.Bitmap)
      {
        _dvbStreams.SetSubtitleStream(option.BitmapIndex);
      }
      ServiceRegistration.Get<ILogger>().Debug("SubtitleSelector: Subtitle is now " + _currentOption.ToString());
      _subRender.SetSubtitleOption(option);
    }

    public string GetCurrentLanguage()
    {
      if (_currentOption == null)
      {
        ServiceRegistration.Get<ILogger>().Error("Calling GetCurrentLanguage with no subtitle set!");
        return "Unknown";
      }
      else if (_currentOption.IsAuto)
      {
        return "Auto:" + _currentOption.Language;
      }
      else if (_currentOption.Type == SubtitleType.Teletext && _currentOption.Entry.Language.Trim().Length == 0)
      {
        return "p" + _currentOption.Entry.Page;
      }
      else return _currentOption.Language;
    }
  }

}
