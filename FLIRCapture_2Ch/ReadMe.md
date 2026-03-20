FLIRCapture_2Ch
Dual‑channel video + audio recorder for FLIR/Teledyne cameras using Spinnaker SDK, OpenCvSharp, and NAudio.
Designed for Windows, Visual Studio 2022, and .NET Framework 4.8.

1. System Requirements

OS: Windows 10 / 11 (x64)
IDE: Visual Studio 2022
Framework: .NET Framework 4.8
Platform target: x64 only
C# language level: C# 7.3 (default for .NET Framework projects)


2. External SDK (Required)
Spinnaker SDK (FLIR / Teledyne)

⚠️ Spinnaker is NOT installed via NuGet

You must install the full Spinnaker SDK separately.
Requirements:

Spinnaker SDK with .NET / C# support enabled
USB3 or GigE transport layers installed
SpinView must be able to stream the camera reliably

After installation, ensure this file exists:
C:\Program Files\Teledyne\Spinnaker\bin64\vs2015\SpinnakerNET.dll

Once started a VS project
Right click on the project > Add > Reference > Browse
C:\Program Files\Teledyne\Spinnaker\bin64\vs2015\SpinnakerNET.dll

add this to the project reference.



FLIRCapture_2Ch/
│
├── Devices/
│   ├── CameraDevice.cs
│   └── AudioDevice.cs
│
├── Utilities/
│   ├── RecChannel.cs
│   └── Config.cs
│
├── Form1.cs
├── Program.cs
└── ReadMe.md


NuGet Packages (Required)
Install these via NuGet Package Manager:
✅ Required

NAudio

Audio device enumeration and WAV recording


OpenCvSharp4 //Video processing and encoding
OpenCvSharp4.extention
OpenCvSharp4.runtime.win

Native OpenCV binaries (FFmpeg support)


5. File Responsibilities & Relationships
Form1.cs (UI / Orchestration)

WinForms UI
Enumerates cameras and microphones
Handles Start / Stop for each channel
Owns two RecChannel instances:

_ch1
_ch2


Receives preview frames via callback
Handles global hotkey (Ctrl+Alt+Q)

Depends on:

Utilities.RecChannel
Devices.CameraDevice
Devices.AudioDevice


Utilities/RecChannel.cs (Channel Glue)
Represents one recording channel:

1 camera + 1 microphone
Owns:

CameraDevice
AudioDevice


Starts/stops both together
Passes preview callback to UI
Passes errors back to UI

Used by:

Form1.cs


Devices/CameraDevice.cs (Video Capture)
Handles all camera interaction:

Spinnaker camera selection (by index)
Camera initialization and configuration
Acquisition loop (threaded)
Mono vs color detection:

Mono → Mono8
Color/Bayer → BGR8 (HQ_LINEAR)


Video writing:

MP4 (preferred)
AVI MJPEG (fallback)


CSV logging:

Frame index
Monotonic timestamp (Stopwatch)
Wall‑clock ISO time
Status flags



Depends on:

SpinnakerNET
OpenCvSharp
Utilities.Config


Devices/AudioDevice.cs (Audio Capture)
Handles microphone recording:

Uses NAudio
Records WAV audio
Optional CSV timestamp logging (bin‑based)
Sample‑accurate timing
Safe start/stop/dispose

Used by:

RecChannel.cs


Utilities/Config.cs (Central Configuration)
Holds static configuration values:
C#public static class Config{    public static class Camera    {        public const int FrameRate = 30;        public const double ExposureTimeMS = 10.0;        public const double GainValuedB = 2.0;    }    public static class Audio    {        public const int SampleRate = 384000;        public const int BitDepth = 16;        public const int Channels = 1;    }    public const double TimestampBinSizeSec = 0.02;}Show more lines
Used by:

CameraDevice
AudioDevice
RecChannel


6. Build Configuration (Critical)
In Visual Studio:

Configuration: Debug or Release
Platform: x64 ✅
❌ Any CPU will NOT work with Spinnaker


7. Runtime Notes
Mono vs Color Cameras

Mono sensors are handled natively (Mono8)
Preview and video are expanded to BGR for compatibility
No fake color is introduced

GenTL / USB Stability
If you see errors like:
GenTL error code: -1011
Failed waiting for EventData on NEW_BUFFER_DATA

Common causes:

USB bandwidth saturation
Insufficient stream buffers
Camera not fully entering acquisition state

Recommended:

Use rear motherboard USB ports
Avoid USB hubs
Disable USB power saving
Test camera streaming in SpinView first


8. Common Pitfalls

❌ Forgetting to include .cs files in the project
❌ Using ManagedSystem.GetInstance() (older SDKs don’t support it)
❌ Using using (ManagedCameraList ...)
❌ Expecting GenICam namespace (not available in older SDKs)
❌ Targeting Any CPU
❌ Mixing modern C# syntax (is not) in C# 7.3 projects


9. First‑Run Checklist

✅ SpinView streams camera for ≥60 seconds
✅ SpinnakerNET.dll referenced correctly
✅ NuGet packages restored
✅ Platform set to x64
✅ One camera tested first (Channel 1)
✅ Output folder selected in UI


10. License / Notes
Internal research / lab use.
Adapt as needed for your specific camera models and acquisition modes.

