## Project Structure
• Form1.cs → UI and orchestration. Builds the form, handles events, enumerates devices, calls RecChannel.
• CameraDevice.cs → Camera control. Initialise(), Start(), Stop(). Handles video capture and frame timestamps.
• AudioDevice.cs → Microphone control. Initialise(), Start(), Stop(). Handles audio capture and audio timestamps.
• RecChannel.cs → Glue layer. Starts/stops both camera and audio together.
• Config.cs → Central constants (framerate, exposure, audio sample rate, logging bin size, output paths).
---
## Relationships
• Form1 → orchestrates UI, delegates to RecChannel.
• RecChannel → coordinates CameraDevice + AudioDevice.
• CameraDevice → video + frame timestamps.
• AudioDevice → audio + audio timestamps.
• Config → shared settings used everywhere.

          +---------------------------+
          |         Config.cs         |
          |  (shared defaults:       |
          |   camera, audio, paths)  |
          +------------+-------------+
                       |
                       v
+-------------------------------------------+
|                 Form1.cs                  |
|  - UI (dropdowns, paths, buttons)         |
|  - OnLoad: enumerate cameras/mics         |
|  - Creates RecChannel for each side       |
+-------------------+-----------------------+
                    |
        +-----------+-----------+
        |                       |
        v                       v
+---------------+       +---------------+
| RecChannel #1 |       | RecChannel #2 |
| camIndex, mic |       | camIndex, mic |
| videoPath     |       | videoPath     |
| audioPath     |       | audioPath     |
+------+--------+       +------+--------+
       |                       |
       |                       |
       v                       v
+--------------+        +--------------+
| CameraDevice |        | CameraDevice |
|  (Spinnaker) |        |  (Spinnaker) |
+--------------+        +--------------+
       ^                       ^
       |                       |
       |                       |
+--------------+        +--------------+
| AudioDevice  |        | AudioDevice  |
|   (NAudio)   |        |   (NAudio)   |
+--------------+        +--------------+

Legend:
- Form1: orchestrates UI + creates/starts/stops RecChannel.
- RecChannel: ties one CameraDevice + one AudioDevice.
- CameraDevice: SpinnakerNET capture, .mp4 + frame timestamps.
- AudioDevice: NAudio capture, .wav + audio timestamps.
- Config: central default parameters used by devices.
