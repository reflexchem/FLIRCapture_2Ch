using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NAudio.Wave;
using FLIRcapture_2Ch;
using FLIRcapture_2Ch.Utilities;


namespace FLIRcapture_2Ch.Devices
{
    public sealed class AudioDevice : IDisposable
    {
        public bool IsRunning { get; private set; }
        public int DeviceIndex { get; private set; } = -1;

        private WaveInEvent _waveIn;
        private WaveFileWriter _audioWriter;
        private StreamWriter _csvWriter;

        private WaveFormat _waveFormat;
        private long _totalSamples;             // accumulated samples written
        private double _binSizeSec;             // from Config.Log.TimestampBinSizeSec
        private double _nextBinEndSec;
        private readonly object _binLock = new object();
        private volatile bool _markerFlag;

        public void Initialize(int deviceIndex, string wavPath, string csvPath = null)
        {
            if (IsRunning) throw new InvalidOperationException("Audio is already running.");
            if (deviceIndex < 0 || deviceIndex >= WaveIn.DeviceCount)
                throw new ArgumentOutOfRangeException(nameof(deviceIndex), "Invalid audio device index.");

            Directory.CreateDirectory(Path.GetDirectoryName(wavPath) ?? ".");
            if (!string.IsNullOrWhiteSpace(csvPath))
                Directory.CreateDirectory(Path.GetDirectoryName(csvPath) ?? ".");

            // Pull from your Config.cs
            int sampleRate = Config.Audio.SampleRate;      // 384000 (validate device support)
            int bitDepth = Config.Audio.BitDepth;        // 16
            int channels = Config.Audio.Channels;        // 1

            _waveFormat = new WaveFormat(sampleRate, bitDepth, channels);

            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = _waveFormat,
                BufferMilliseconds = 20  // 20 ms is reasonable at very high SR to reduce callback pressure
            };

            _audioWriter = new WaveFileWriter(wavPath, _waveFormat);

            _csvWriter = null;
            if (!string.IsNullOrWhiteSpace(csvPath))
            {
                _csvWriter = new StreamWriter(csvPath);
                _csvWriter.WriteLine("BinEndTimeSec,WallClockISO,Marker,DeviceIndex,SampleRate,Channels,BitDepth");
            }

            _binSizeSec = Config.Log.TimestampBinSizeSec;   // 0.02 s
            _nextBinEndSec = _binSizeSec;
            _totalSamples = 0;
            DeviceIndex = deviceIndex;

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
        }

        public void Start()
        {
            if (_waveIn == null || _audioWriter == null)
                throw new InvalidOperationException("Call Initialize() first.");

            if (IsRunning) return;
            _waveIn.StartRecording();
            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                Cleanup();
                return;
            }
            try { _waveIn?.StopRecording(); } catch { Cleanup(); }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            // Write PCM directly (no extra allocation)
            _audioWriter.Write(e.Buffer, 0, e.BytesRecorded);

            // Advance sample counter
            int bytesPerSample = _waveFormat.BitsPerSample / 8;
            int samplesInBuffer = e.BytesRecorded / (bytesPerSample * _waveFormat.Channels);
            long newTotal = Interlocked.Add(ref _totalSamples, samplesInBuffer);

            if (_csvWriter != null)
            {
                double nowSec = (double)newTotal / _waveFormat.SampleRate;
                lock (_binLock)
                {
                    while (nowSec >= _nextBinEndSec)
                    {
                        string iso = DateTime.UtcNow.ToString("O");
                        int marker = _markerFlag ? 1 : 0;
                        _csvWriter.WriteLine($"{_nextBinEndSec:F6},{iso},{marker},{DeviceIndex},{_waveFormat.SampleRate},{_waveFormat.Channels},{_waveFormat.BitsPerSample}");
                        _markerFlag = false;
                        _nextBinEndSec += _binSizeSec;
                    }
                }
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Debug.WriteLine($"Audio stopped due to error: {e.Exception}");
            }
            Cleanup();
        }

        private void Cleanup()
        {
            IsRunning = false;

            if (_waveIn != null)
            {
                try
                {
                    _waveIn.DataAvailable -= OnDataAvailable;
                    _waveIn.RecordingStopped -= OnRecordingStopped;
                    _waveIn.Dispose();
                }
                catch { }
                _waveIn = null;
            }

            if (_audioWriter != null)
            {
                try { _audioWriter.Flush(); _audioWriter.Dispose(); } catch { }
                _audioWriter = null;
            }

            if (_csvWriter != null)
            {
                try
                {
                    // Emit the final bin edge (optional; keep consistent)
                    _csvWriter.Flush();
                    _csvWriter.Close();
                }
                catch { }
                _csvWriter = null;
            }

            _totalSamples = 0;
            _nextBinEndSec = _binSizeSec;
            _markerFlag = false;
        }

        public void Dispose() => Cleanup();

        /// <summary>
        /// Tag the current CSV bin with a marker (e.g., triggered frame, hotkey).
        /// </summary>
        public void Mark()
        {
            lock (_binLock) _markerFlag = true;
        }
    }
}