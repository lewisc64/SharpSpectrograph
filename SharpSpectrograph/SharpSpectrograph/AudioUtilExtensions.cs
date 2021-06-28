using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSpectrograph
{
    public static class AudioUtilExtensions
    {
        public static IEnumerable<float> ToMonoSamples(this byte[] buffer, WaveFormat format, int offset, int length)
        {
            var bytesPerSample = format.BitsPerSample / 8;

            if (bytesPerSample != 4)
            {
                throw new ArgumentException("WaveFormat must be IEEE floats.", nameof(format));
            }

            for (var i = offset; i < offset + length; i += format.Channels * bytesPerSample)
            {
                var values = new List<float>();
                for (var channel = 0; channel < format.Channels; channel++)
                {
                    values.Add(BitConverter.ToSingle(buffer, i + bytesPerSample * channel));
                }
                yield return values.Average();
            }
        }
    }
}
