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
using System.Runtime.InteropServices;
using System.Text;

namespace MediaPortal.Extensions.BassLibraries
{
  /// <summary>
  /// Reads the text frames of an ID3v2 tag (versions 2.2, 2.3 and 2.4), which BASS delivers as raw data (<c>TagType.ID3v2</c>).
  /// </summary>
  /// <remarks>
  /// The tag reader of ManagedBass decodes UTF-16 frames incorrectly (byte order mark and last character), so the frames are decoded here.
  /// </remarks>
  public static class Id3v2Reader
  {
    /// <summary>
    /// Reads the text frames of the tag.
    /// </summary>
    /// <param name="tag">Pointer to the tag as returned by <c>Bass.ChannelGetTags(handle, TagType.ID3v2)</c>.</param>
    /// <returns>Dictionary of the frame ids (for example <c>TIT2</c>, in the version 2.2 the 3 character ids are converted to the ones of 2.3)
    /// and their text. Frames which occur more than once are represented by the first one.</returns>
    public static IDictionary<string, string> ReadTextFrames(IntPtr tag)
    {
      Dictionary<string, string> result = new Dictionary<string, string>();
      if (tag == IntPtr.Zero)
        return result;

      byte[] header = new byte[10];
      Marshal.Copy(tag, header, 0, 10);
      if (header[0] != 'I' || header[1] != 'D' || header[2] != '3')
        return result;
      int version = header[3];
      if (version < 2 || version > 4)
        return result;
      bool unsynchronization = (header[5] & 0x80) != 0;
      bool extendedHeader = (header[5] & 0x40) != 0;
      int size = SynchSafe(header, 6);
      if (size <= 0 || size > 32 * 1024 * 1024)
        return result;

      byte[] data = new byte[size];
      Marshal.Copy(tag + 10, data, 0, size);
      if (unsynchronization && version < 4)
        data = RemoveUnsynchronization(data);

      int position = 0;
      if (extendedHeader && data.Length >= 4)
        position = version == 4 ? SynchSafe(data, 0) : BigEndian(data, 0, 4) + 4;

      int idLength = version == 2 ? 3 : 4;
      int headerLength = version == 2 ? 6 : 10;
      while (position + headerLength <= data.Length)
      {
        if (data[position] == 0)
          break; // Padding
        string id = Encoding.ASCII.GetString(data, position, idLength);
        int frameSize = version == 2 ? BigEndian(data, position + 3, 3) : version == 3 ? BigEndian(data, position + 4, 4) : SynchSafe(data, position + 4);
        int start = position + headerLength;
        if (frameSize <= 0 || start + frameSize > data.Length)
          break;

        string text = null;
        if (id.StartsWith("T", StringComparison.Ordinal) && id != "TXXX" && id != "TXX")
          text = DecodeText(data, start, frameSize);
        else if (id == "COMM" || id == "COM")
          text = DecodeComment(data, start, frameSize);
        if (!string.IsNullOrEmpty(text))
        {
          id = version == 2 ? ConvertId(id) : id;
          if (!result.ContainsKey(id))
            result[id] = text;
        }
        position = start + frameSize;
      }
      return result;
    }

    private static string ConvertId(string id)
    {
      switch (id)
      {
        case "TT2": return "TIT2";
        case "TP1": return "TPE1";
        case "TP2": return "TPE2";
        case "TAL": return "TALB";
        case "TCM": return "TCOM";
        case "TCO": return "TCON";
        case "TRK": return "TRCK";
        case "TYE": return "TYER";
        case "COM": return "COMM";
        default: return id;
      }
    }

    private static int SynchSafe(byte[] data, int offset)
    {
      return (data[offset] & 0x7F) << 21 | (data[offset + 1] & 0x7F) << 14 | (data[offset + 2] & 0x7F) << 7 | (data[offset + 3] & 0x7F);
    }

    private static int BigEndian(byte[] data, int offset, int count)
    {
      int result = 0;
      for (int i = 0; i < count; i++)
        result = result << 8 | data[offset + i];
      return result;
    }

    private static byte[] RemoveUnsynchronization(byte[] data)
    {
      List<byte> result = new List<byte>(data.Length);
      for (int i = 0; i < data.Length; i++)
      {
        result.Add(data[i]);
        if (data[i] == 0xFF && i + 1 < data.Length && data[i + 1] == 0)
          i++;
      }
      return result.ToArray();
    }

    /// <summary>
    /// Decodes a text frame: the first byte is the encoding, followed by the text. Several values (separated by a null character) are
    /// joined with ';'.
    /// </summary>
    private static string DecodeText(byte[] data, int offset, int length)
    {
      if (length < 2)
        return null;
      return DecodeString(data, offset + 1, length - 1, data[offset]);
    }

    private static string DecodeComment(byte[] data, int offset, int length)
    {
      // Encoding, language (3 bytes), short description (terminated), text
      if (length < 5)
        return null;
      byte encoding = data[offset];
      int start = offset + 4;
      int end = offset + length;
      bool wide = encoding == 1 || encoding == 2;
      int i = start;
      while (i < end && !(wide ? (i + 1 < end && data[i] == 0 && data[i + 1] == 0) : data[i] == 0))
        i += wide ? 2 : 1;
      i += wide ? 2 : 1;
      if (i >= end)
        return null;
      return DecodeString(data, i, end - i, encoding);
    }

    private static string DecodeString(byte[] data, int offset, int length, byte encoding)
    {
      string text;
      switch (encoding)
      {
        case 1:
          Encoding utf16 = Encoding.Unicode;
          if (length >= 2 && data[offset] == 0xFE && data[offset + 1] == 0xFF)
            utf16 = Encoding.BigEndianUnicode;
          if (length >= 2 && (data[offset] == 0xFE && data[offset + 1] == 0xFF || data[offset] == 0xFF && data[offset + 1] == 0xFE))
          {
            offset += 2;
            length -= 2;
          }
          text = utf16.GetString(data, offset, length);
          break;
        case 2:
          text = Encoding.BigEndianUnicode.GetString(data, offset, length);
          break;
        case 3:
          text = Encoding.UTF8.GetString(data, offset, length);
          break;
        default:
          text = Encoding.Latin1.GetString(data, offset, length);
          break;
      }
      text = text.Replace('﻿', ' ');
      string[] parts = text.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
      for (int i = 0; i < parts.Length; i++)
        parts[i] = parts[i].Trim();
      return string.Join(";", Array.FindAll(parts, p => p.Length > 0));
    }
  }
}
