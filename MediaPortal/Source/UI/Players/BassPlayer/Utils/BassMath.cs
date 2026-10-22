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

namespace MediaPortal.UI.Players.BassPlayer.Utils
{
  /// <summary>
  /// Helper methods for the evaluation of BASS level and FFT data.
  /// </summary>
  public static class BassMath
  {
    /// <summary>
    /// Gets the index of the given frequency in the FFT data.
    /// </summary>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <param name="fftSize">Number of FFT samples (for example 4096).</param>
    /// <param name="sampleRate">Sample rate of the stream.</param>
    public static int FFTFrequency2Index(int frequency, int fftSize, int sampleRate)
    {
      return (int) Math.Round(fftSize * (double) frequency / sampleRate);
    }

    /// <summary>
    /// Converts a level (as returned by BASS) to decibel.
    /// </summary>
    public static double LevelToDB(int level, double maxLevel)
    {
      return 20.0 * Math.Log10(level / maxLevel);
    }

    public static int LowWord32(int value)
    {
      return value & 0xFFFF;
    }

    public static int HighWord32(int value)
    {
      return (value >> 16) & 0xFFFF;
    }
  }
}
