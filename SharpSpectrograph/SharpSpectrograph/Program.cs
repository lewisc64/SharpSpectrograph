using System;
using NAudio.Wave;
using FftSharp;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace SharpSpectrograph
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var recorder = new MonoComputerAudioRecorder())
            {
                var display = new Display
                {
                    SampleRate = recorder.SampleRate,
                };

                recorder.SampleChunkRecorded += (_, chunk) =>
                {
                    display.SendSamples(chunk);
                };

                recorder.Start();
                display.RunSpectrogram();
                recorder.Stop();
            }
        }
    }
}
