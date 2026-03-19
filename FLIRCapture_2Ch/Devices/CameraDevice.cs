using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SpinnakerNET;
using SpinnakerNET.GenApi;
using SpinnakerNET.GenICam;
using FLIRcapture_2Ch;

namespace FLIRcapture_2Ch.Devices
{
    public sealed class CameraDevice : IDisposable
    {
        // Callbacks
        private readonly Action<Bitmap> _onFrame;
        private readonly Action<string> _onError;

        // Selection & output
        private string _cameraIdOrIndex;
        private string _videoPath;
        private string _csvPath;

        // Spinnaker
        private ManagedSystem _system;
        private IManagedCamera _camera;
        private INodeMap _nodeMap;
        private string _cameraSerial;
        private int _cameraIndexInt = -1;

        // Processing
        private readonly ManagedImageProcessor _processor = new ManagedImageProcessor();
        private VideoWriter _writer;
        private Thread _worker;
        private CancellationTokenSource _cts;

        // Timing & logging
        private Stopwatch _sw;
        private long _frameIndex;
        private StreamWriter _csv;
        public bool IsRunning { get; private set; }

        public CameraDevice(Action<Bitmap> onFrame = null, Action<string> onError = null)
        {
            _onFrame = onFrame;
            _onError = onError;
        }

        public void Initialize(string cameraIdOrIndex, string videoPath, double? targetFps = null)
        {
            if (IsRunning) throw new InvalidOperationException("Camera running.");
            if (string.IsNullOrWhiteSpace(cameraIdOrIndex)) throw new ArgumentNullException(nameof(cameraIdOrIndex));
            if (string.IsNullOrWhiteSpace(videoPath)) throw new ArgumentNullException(nameof(videoPath));

            _cameraIdOrIndex = cameraIdOrIndex.Trim();
            _videoPath = videoPath;
            _csvPath = Path.ChangeExtension(_videoPath, ".csv");

            Directory.CreateDirectory(Path.GetDirectoryName(_videoPath) ?? ".");
            Directory.CreateDirectory(Path.GetDirectoryName(_csvPath) ?? ".");

            // Spinnaker system
            _system = new ManagedSystem();

            // Select by index first (as per your preference)
            var list = _system.GetCameras();
            try
            {
                if (list.Count == 0) throw new InvalidOperationException("No cameras found.");

                if (!int.TryParse(_cameraIdOrIndex, out _cameraIndexInt) ||
                    _cameraIndexInt < 0 || _cameraIndexInt >= list.Count)
                {
                    throw new InvalidOperationException($"Camera index '{_cameraIdOrIndex}' invalid. Available count: {list.Count}");
                }

                _camera = list[_cameraIndexInt];
                // we keep _camera; list will be disposed below
            }
            finally
            {
                list.Clear();
                list.Dispose();
            }

            // Init and read serial
            _camera.Init();
            _nodeMap = _camera.GetNodeMap();
            try
            {
                _cameraSerial = _camera.TLDevice.DeviceSerialNumber?.ToString();
            }
            catch { _cameraSerial = ""; }

            // Configure
            SetEnum(_nodeMap, "AcquisitionMode", "Continuous");
            TrySetEnum(_nodeMap, "ExposureAuto", "Off");

            // Exposure time in microseconds (Config is ms)
            if (Config.Camera.ExposureTimeMS > 0)
            {
                TrySetFloat(_nodeMap, "ExposureTime", Config.Camera.ExposureTimeMS * 1000.0);
            }

            // Gain (dB)
            TrySetEnum(_nodeMap, "GainAuto", "Off");
            if (Config.Camera.GainValuedB >= 0)
            {
                TrySetFloat(_nodeMap, "Gain", Config.Camera.GainValuedB);
            }

            // Buffer handling
            TrySetEnum(_camera.GetTLStreamNodeMap(), "StreamBufferHandlingMode", "NewestOnly");

            // Processing
            _processor.SetColorProcessing(ColorProcessingAlgorithm.EdgeSensing);

            // CSV
            _csv = new StreamWriter(_csvPath);
            _csv.WriteLine("FrameIndex,RelTimeSec,WallClockISO,Triggered,Status,CameraIndex,CameraSerial");

            _frameIndex = 0;
            _sw = new Stopwatch();
        }

        public void Start()
        {
            if (_camera == null) throw new InvalidOperationException("Initialize first.");
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _camera.BeginAcquisition();
            _sw.Restart();

            _worker = new Thread(() => CaptureLoop(_cts.Token))
            {
                IsBackground = true,
                Name = "CameraCaptureLoop"
            };
            _worker.Start();
            IsRunning = true;
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                CleanupWriters();
                return;
            }

            try { _cts?.Cancel(); } catch { }
            try
            {
                if (_worker != null && _worker.IsAlive)
                {
                    if (!_worker.Join(TimeSpan.FromSeconds(2)))
                        _worker.Interrupt();
                }
            }
            catch { }
            finally { _worker = null; }

            try { _camera?.EndAcquisition(); } catch { }

            CleanupWriters();
            IsRunning = false;
        }

        public void Dispose()
        {
            Stop();

            if (_camera != null)
            {
                try { _camera.DeInit(); } catch { }
                _camera.Dispose();
                _camera = null;
            }

            if (_system != null)
            {
                try { _system.Dispose(); } catch { }
                _system = null;
            }
        }

        private void CaptureLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    using (var raw = _camera.GetNextImage(1000))
                    {
                        if (raw == null || !raw.IsValid)
                        {
                            WriteRow(_frameIndex, _sw.Elapsed.TotalSeconds, "Timeout", triggered: 0);
                            continue;
                        }
                        if (raw.IsIncomplete)
                        {
                            WriteRow(_frameIndex, _sw.Elapsed.TotalSeconds, "Incomplete", triggered: 0);
                            continue;
                        }

                        using (var bgr = new ManagedImage())
                        {
                            _processor.Convert(raw, bgr, PixelFormatEnums.BGR8);

                            int width = (int)bgr.Width;
                            int height = (int)bgr.Height;

                            if (_writer == null)
                            {
                                // Prefer MP4; fallback to AVI MJPG if MP4 not available in your OpenCV runtime
                                if (!TryOpenVideoWriterMp4(width, height, Config.Camera.FrameRate))
                                {
                                    if (!TryOpenVideoWriterAviMjpg(width, height, Config.Camera.FrameRate))
                                        throw new InvalidOperationException("Failed to open video writer (MP4 and AVI both failed).");
                                }
                            }

                            using (var mat = new Mat(height, width, MatType.CV_8UC3, bgr.DataPtr, bgr.Stride))
                            {
                                _writer.Write(mat);

                                // Log timing
                                double rel = _sw.Elapsed.TotalSeconds;
                                WriteRow(_frameIndex, rel, "OK", triggered: 0);

                                // Preview
                                if (_onFrame != null)
                                {
                                    Bitmap bmp = BitmapConverter.ToBitmap(mat);
                                    _onFrame.Invoke(bmp);
                                }

                                _frameIndex++;
                            }
                        }
                    }
                }
            }
            catch (ThreadInterruptedException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _onError?.Invoke($"Camera loop error: {ex.Message}");
            }
            finally
            {
                CleanupWriters();
            }
        }

        private void WriteRow(long frameIdx, double relTimeSec, string status, int triggered)
        {
            string iso = DateTime.UtcNow.ToString("O");
            _csv.WriteLine($"{frameIdx},{relTimeSec:F6},{iso},{triggered},{status},{_cameraIndexInt},{_cameraSerial}");
        }

        private bool TryOpenVideoWriterMp4(int width, int height, int fps)
        {
            try
            {
                _writer = new VideoWriter(
                    _videoPath,
                    FourCC.MP4V,          // mp4v; works if your OpenCV has FFmpeg
                    fps,
                    new OpenCvSharp.Size(width, height),
                    isColor: true);

                if (!_writer.IsOpened())
                {
                    _writer.Release();
                    _writer.Dispose();
                    _writer = null;
                    return false;
                }
                return true;
            }
            catch
            {
                try { _writer?.Release(); _writer?.Dispose(); } catch { }
                _writer = null;
                return false;
            }
        }

        private bool TryOpenVideoWriterAviMjpg(int width, int height, int fps)
        {
            try
            {
                string aviPath = Path.ChangeExtension(_videoPath, ".avi");
                _writer = new VideoWriter(
                    aviPath,
                    FourCC.MJPG,
                    fps,
                    new OpenCvSharp.Size(width, height),
                    isColor: true);

                if (!_writer.IsOpened())
                {
                    _writer.Release();
                    _writer.Dispose();
                    _writer = null;
                    return false;
                }
                // Note: CSV path remained with original extension; keep or change as you prefer
                return true;
            }
            catch
            {
                try { _writer?.Release(); _writer?.Dispose(); } catch { }
                _writer = null;
                return false;
            }
        }

        private void CleanupWriters()
        {
            try { _csv?.Flush(); _csv?.Close(); } catch { }
            _csv = null;

            if (_writer != null)
            {
                try { _writer.Release(); _writer.Dispose(); } catch { }
                _writer = null;
            }

            try { _sw?.Stop(); } catch { }
        }

        // Node helpers
        private static void SetEnum(INodeMap nodeMap, string nodeName, string entryName)
        {
            var node = nodeMap.GetNode<IEnum>(nodeName);
            if (node == null || !node.IsReadable || !node.IsWritable) return;
            var entry = node.GetEntryByName(entryName);
            if (entry != null && entry.IsReadable) node.Value = entry.Value;
        }

        private static void TrySetEnum(INodeMap nodeMap, string nodeName, string entryName)
        {
            try { SetEnum(nodeMap, nodeName, entryName); } catch { }
        }

        private static void TrySetFloat(INodeMap nodeMap, string nodeName, double value)
        {
            try
            {
                var node = nodeMap.GetNode<IFloat>(nodeName);
                if (node != null && node.IsWritable)
                {
                    double v = Math.Max(node.Min, Math.Min(node.Max, value));
                    node.Value = v;
                }
            }
            catch { }
        }
    }
}