using System;
using System.Drawing;
using System.IO;
using FLIRcapture_2Ch;
using FLIRcapture_2Ch.Devices;

namespace FLIRcapture_2Ch.Utilities
{
    public sealed class RecChannel : IDisposable
    {
        private readonly string _cameraIdOrIndex;    // index as string, e.g. "0"
        private readonly int _micIndex;
        private readonly string _videoPath;
        private readonly string _audioPath;
        private readonly Action<Bitmap> _onFrameReady;
        private readonly Action<string> _onError;
        private readonly bool _enableAudioCsv;

        private CameraDevice _camera;
        private AudioDevice _audio;

        public bool IsRunning { get; private set; }

        public RecChannel(
            string cameraIdOrIndex,
            int micIndex,
            string videoPath,
            string audioPath,
            Action<Bitmap> onFrameReady,
            Action<string> onError = null,
            bool enableAudioCsv = true)
        {
            _cameraIdOrIndex = cameraIdOrIndex ?? throw new ArgumentNullException(nameof(cameraIdOrIndex));
            _micIndex = micIndex;
            _videoPath = videoPath ?? throw new ArgumentNullException(nameof(videoPath));
            _audioPath = audioPath ?? throw new ArgumentNullException(nameof(audioPath));
            _onFrameReady = onFrameReady; // can be null for headless
            _onError = onError;
            _enableAudioCsv = enableAudioCsv;
        }


        public void Start()
        {
            if (IsRunning) return;

            Directory.CreateDirectory(Path.GetDirectoryName(_videoPath) ?? ".");
            Directory.CreateDirectory(Path.GetDirectoryName(_audioPath) ?? ".");

            // Camera
            _camera = new CameraDevice(
                onFrame: bmp => _onFrameReady?.Invoke(bmp),
                onError: msg => _onError?.Invoke($"Camera: {msg}")
            );
            _camera.Initialize(_cameraIdOrIndex, _videoPath, Config.Camera.FrameRate);

            // Audio
            _audio = new AudioDevice();
            string audioCsv = _enableAudioCsv ? Path.ChangeExtension(_audioPath, ".csv") : null;
            _audio.Initialize(_micIndex, _audioPath, audioCsv);

            // Start both
            try
            {
                _camera.Start();
                _audio.Start();
                IsRunning = true;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Start failed: {ex.Message}");
                try { _camera?.Stop(); } catch { }
                try { _audio?.Stop(); } catch { }
                try { _camera?.Dispose(); } catch { }
                try { _audio?.Dispose(); } catch { }
                _camera = null;
                _audio = null;
                IsRunning = false;
                throw;
            }
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                Cleanup();
                return;
            }

            try { _camera?.Stop(); } catch (Exception ex) { _onError?.Invoke($"Camera stop: {ex.Message}"); }
            try { _audio?.Stop(); } catch (Exception ex) { _onError?.Invoke($"Audio stop: {ex.Message}"); }

            Cleanup();
            IsRunning = false;
        }

        private void Cleanup()
        {
            try { _camera?.Dispose(); } catch { }
            try { _audio?.Dispose(); } catch { }
            _camera = null;
            _audio = null;
        }

        public void Dispose() => Stop();

        /// <summary>Optional marker to tag the current audio bin (for triggers/hotkeys).</summary>
        public void MarkAudio() { try { _audio?.Mark(); } catch { } }
    }
}