namespace FLIRcapture_2Ch.Utilities
{
    public static class Config
    {
        //confusing now. I should add tag for camera and for audio to make it clear when calling
        public static class Camera
        {
            // Camera defaults
            public const int FrameRate = 30;                    // Frames per second
            public const double ExposureTimeMS = 10.0;              // Exposure time in milliseconds (ms)
            public const double GainValuedB = 2.0;                 // Gain value in dB
        }

        public static class Audio
        {
            // Audio defaults
            public const int SampleRate = 384000;               // Hz
            public const int BitDepth = 16;
            public const int Channels = 1;                      // 1 = Mono

        }

        public static class Path
        {
            public const string OutputDirectory = null; // Catch if no output directory is selected at GUI
        }

        // Logging
        public static class Log
        {
            public const double TimestampBinSizeSec = 0.02;     // seconds
        }

    }
}