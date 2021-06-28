using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FftSharp;

namespace SharpSpectrograph
{
    public class Display
    {
        private Bitmap _buffer;

        private Bitmap _screen;

        private Graphics _graphics;

        private List<float> _sampleBuffer;

        private object _drawLock = new object();

        private Form Form { get; set; } = new Form();

        private PictureBox PictureBox { get; set; } = new PictureBox();

        private Image Image { get; set; }

        public int SampleRate { get; set; } = 48000;

        public int MaxBufferSize { get; set; } = 1000000;

        public int MinFrequency { get; set; } = 20;

        public int MaxFrequency { get; set; } = 10000;

        public int Width {
            get
            {
                return Form.Width;
            }
            private set
            {
                Form.Width = value;
                RecreateVisualBuffers();
            }
        }

        public int Height
        {
            get
            {
                return Form.Height;
            }
            private set
            {
                Form.Height = value;
                RecreateVisualBuffers();
            }
        }

        public Display()
        {
            _sampleBuffer = new List<float>();

            RecreateVisualBuffers();

            Form.Controls.Add(PictureBox);

            Form.Resize += (a, b) =>
            {
                Width = Form.Width;
                Height = Form.Height;
            };

            // Form.FormBorderStyle = FormBorderStyle.FixedSingle;
        }

        public void SendSamples(IEnumerable<float> samples)
        {
            _sampleBuffer.AddRange(samples);
        }

        public void RunWaveform()
        {
            Width = 1200;
            Height = 600;

            var bufferManager = new SampleBufferManager
            {
                SampleBuffer = _sampleBuffer,
                WindowSize = SampleRate / 20,
                MaxBufferSize = SampleRate * 10,
            };

            DisplayLoopCallback(() =>
            {
                _graphics.FillRectangle(Brushes.Black, new Rectangle(0, 0, Width, Height));
                DrawWaveform(bufferManager.GetWindow());
            });
        }

        public void RunSpectrogram()
        {
            Width = 1100;
            Height = 800;

            var bufferManager = new SampleBufferManager
            {
                SampleBuffer = _sampleBuffer,
                WindowSize = 4096 * 4,
                MaxBufferSize = SampleRate * 10,
            };

            const double spacingMultipler = 1.012;

            _graphics.FillRectangle(Brushes.Black, new Rectangle(0, 0, Width, Height));

            DisplayLoopCallback(() =>
            {
                _graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 0, 0, 0)), new Rectangle(0, 0, Width, Height / 2));
                _graphics.FillRectangle(new SolidBrush(Color.FromArgb(255, 0, 0, 0)), new Rectangle(0, Height / 2, Width, Height / 2));
                var samples = bufferManager.GetWindow();

                if (samples.Any())
                {
                    var transformed = Transform.FFTmagnitude(samples.Select(x => Convert.ToDouble(x) * 10).ToArray());

                    var start = MinFrequency * transformed.Length / SampleRate;
                    var end = MaxFrequency * transformed.Length / SampleRate;

                    var selected = new List<double>();

                    var spacing = 1D;

                    for (var i = 0; i < transformed.Length; i += (int)Math.Floor(spacing))
                    {
                        var set = transformed.Skip((int)(i - spacing)).Take((int)spacing);
                        selected.Add(set.Max());
                        spacing *= spacingMultipler;
                    }

                    DrawWaveform(selected.Select(x => x), fill: true);
                    DrawWaveform(selected.Select(x => -x), fill: false);
                }
            });
        }

        private void DisplayLoopCallback(Action callback, int fps = 60)
        {
            ShowForm();

            var timer = new FrameTimer
            {
                FramesPerSecond = fps,
            };

            var run = true;

            Form.FormClosed += (a, b) =>
            {
                run = false;
            };

            while (run)
            {
                lock (_drawLock)
                {
                    callback();
                }
                Update();
                timer.Wait();
            }
        }

        private void ShowForm()
        {
            Task.Run(() => { Form.ShowDialog(); });
        }

        private void DrawWaveform<T>(IEnumerable<T> values, Color color = default, bool fill = false)
        {
            if (!values.Any())
            {
                return;
            }

            if (color == default)
            {
                color = Color.Yellow;
            }

            float x = 0;
            var step = (float)Width / values.Count();

            var brush = new SolidBrush(color);
            var pen = new Pen(brush, 1);

            for (var i = 0; i < values.Count() - 1; i++)
            {
                var sample = values.ElementAt(i);
                var nextSample = values.ElementAt(i + 1);

                var x1 = (int)x;
                var y1 = (int)(Height / 2 - Height / 2 * (dynamic)sample);

                var x2 = (int)(x + step);
                var y2 = (int)(Height / 2 - Height / 2 * (dynamic)nextSample);

                _graphics.DrawLine(pen, x1, y1, x2, y2);

                if (fill)
                {
                    _graphics.FillPolygon(brush, new[] { new Point(x1, y1), new Point(x2, y2), new Point(x2, Height / 2), new Point(x1, Height / 2) });
                }

                x += step;
            }
        }

        private void RecreateVisualBuffers()
        {
            lock (_drawLock)
            {
                _buffer?.Dispose();
                _buffer = new Bitmap(Width, Height);

                _screen?.Dispose();
                _screen = new Bitmap(Width, Height);

                _graphics?.Dispose();
                _graphics = Graphics.FromImage(_buffer);
                // _graphics.SmoothingMode = SmoothingMode.AntiAlias;

                PictureBox.Image = _screen;
                PictureBox.Width = Width;
                PictureBox.Height = Height;
            }
        }

        private void Update()
        {
            try
            {
                var screenGraphics = Graphics.FromImage(_screen);
                screenGraphics.DrawImage(_buffer, new Point(0, 0));
                screenGraphics.Dispose();
                PictureBox.Invalidate(new Rectangle(0, 0, Width, Height));
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private class FrameTimer
        {
            Stopwatch _stopwatch = new Stopwatch();

            public int FramesPerSecond { get; set; }

            public FrameTimer()
            {
                _stopwatch.Start();
            }

            public void Wait()
            {
                while (_stopwatch.Elapsed.Ticks * 0.0001 < 1000 / (double)FramesPerSecond)
                {
                }
                _stopwatch.Restart();
            }
        }

        private class SampleBufferManager
        {
            private int _currentPosition = 0;

            public List<float> SampleBuffer { get; set; }

            public int WindowSize { get; set; }

            public int MaxBufferSize { get; set; }

            public SampleBufferManager()
            {
            }

            public float[] GetWindow()
            {
                var samples = SampleBuffer.Take(_currentPosition).TakeLast(WindowSize).ToArray();

                if (SampleBuffer.Count > MaxBufferSize)
                {
                    var diff = SampleBuffer.Count - MaxBufferSize;
                    _currentPosition -= diff;
                    SampleBuffer.RemoveRange(0, diff);
                }

                _currentPosition += (SampleBuffer.Count - _currentPosition) / 2;

                if (samples.Length != WindowSize)
                {
                    return new float[0];
                }

                return samples;
            }
        }
    }


}
