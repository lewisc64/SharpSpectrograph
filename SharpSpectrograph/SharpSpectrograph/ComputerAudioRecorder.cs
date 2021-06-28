using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSpectrograph
{
    public class MonoComputerAudioRecorder : IDisposable
    {
        private WasapiLoopbackCapture _loopbackCapture;

        private WaveOutEvent _silencePlayer;

        public int SampleRate {
            get
            {
                return _loopbackCapture.WaveFormat.SampleRate;
            }
        }

        private List<float> Buffer { get; } = new List<float>();

        public int Channels { get; } = 1;

        public int SampleChunkSize { get; set; } = -1;

        public bool Recording { get; private set; }

        public event EventHandler<float[]> SampleChunkRecorded;

        public MonoComputerAudioRecorder()
        {
            _loopbackCapture = new WasapiLoopbackCapture();

            _loopbackCapture.DataAvailable += (_, b) =>
            {
                Buffer.AddRange(b.Buffer.ToMonoSamples(_loopbackCapture.WaveFormat, 0, b.BytesRecorded));
                while (Buffer.Count >= SampleChunkSize && Buffer.Any())
                {
                    if (SampleChunkSize == -1)
                    {
                        SampleChunkRecorded.Invoke(this, Buffer.ToArray());
                        Buffer.Clear();
                    }
                    else
                    {
                        SampleChunkRecorded.Invoke(this, Buffer.Take(SampleChunkSize).ToArray());
                        Buffer.RemoveRange(0, SampleChunkSize);
                    }
                }
            };

            _silencePlayer = new WaveOutEvent();
            _silencePlayer.Init(new Silence());
            _silencePlayer.Play();
        }

        public void Start()
        {
            Recording = true;
            _loopbackCapture.StartRecording();
        }

        public void Stop()
        {
            Recording = false;
            _loopbackCapture.StopRecording();
        }

        public void Dispose()
        {
            _silencePlayer.Dispose();
            _loopbackCapture.Dispose();
        }

        private class Silence : WaveProvider32
        {
            public override int Read(float[] buffer, int offset, int sampleCount)
            {
                return buffer.Length;
            }
        }
    }
}
