#region Copyright (C) 2007-2014 Team MediaPortal

/*
    Copyright (C) 2007-2014 Team MediaPortal
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

#region Copyright (C) 2011, Jacob Johnston

// Copyright (C) 2011, Jacob Johnston 
//
// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions: 
//
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software. 
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL 
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE. 

#endregion

namespace MediaPortal.UI.Presentation.Players
{
  /// <summary>
  /// Provides access to sound player functionality needed to render a spectrum analyzer.
  /// </summary>
  public interface ISpectrumPlayer : IAudioPlayer
  {
    /// <summary>
    /// Copies the current FFT data to a buffer.
    /// </summary>
    /// <remarks>
    /// The FFT data in the buffer should consist only of the real number intensity values. This means that if your FFT algorithm returns
    /// complex numbers (as many do), you'd run an algorithm similar to:
    /// <code>
    /// for(int i = 0; i &lt; complexNumbers.Length / 2; i++)
    ///     fftResult[i] = Math.Sqrt(complexNumbers[i].Real * complexNumbers[i].Real + complexNumbers[i].Imaginary * complexNumbers[i].Imaginary);
    /// </code>
    /// </remarks>
    /// <param name="fftDataBuffer">The buffer to copy the FFT data to. The buffer should consist of only non-imaginary numbers.</param>
    /// <returns>True if data was written to the buffer, otherwise false.</returns>
    bool GetFFTData(float[] fftDataBuffer);

    /// <summary>
    /// Gets the index in the FFT data buffer for a given frequency.
    /// </summary>
    /// <param name="frequency">The frequency for which to obtain a buffer index.</param>
    /// <param name="frequencyIndex">If the return value is <c>true</c>, this value will return an index in the FFT data buffer which was returned
    /// by method <see cref="GetFFTData"/>.</param>
    /// <returns><c>true</c>, if the FFT buffer was already created, else <c>false</c>.</returns>
    bool GetFFTFrequencyIndex(int frequency, out int frequencyIndex);
  }
}