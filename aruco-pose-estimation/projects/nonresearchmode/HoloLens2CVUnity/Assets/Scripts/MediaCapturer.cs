// This script is originally from Rene Schulte's repo: https://github.com/reneschulte/WinMLExperiments/blob/master/HoloVision20/Assets/Scripts/MediaCapturer.cs
// with some modifications

using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.Media;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.Devices;
using Windows.Graphics.Imaging;
using Windows.Devices.Enumeration;
#endif // ENABLE_WINMD_SUPPORT

public class MediaCapturer
{
    public bool IsCapturing { get; set; }

#if ENABLE_WINMD_SUPPORT
    private MediaCapture _captureManager = null;
    private MediaCaptureInitializationSettings _captureSettings = null;
    private MediaFrameReader _frameReader = null;

    /*  Original code:
    public async Task StartCapturing(uint width = 320, uint height = 240)
    {
        if (_captureManager == null || _captureManager.CameraStreamState == CameraStreamState.Shutdown || _captureManager.CameraStreamState == CameraStreamState.NotStreaming)
        {
            if (_captureManager != null)
            {
                _captureManager.Dispose();
            }

            // Find right camera settings and prefer back camera
            MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
            var allCameras = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var selectedCamera = allCameras.FirstOrDefault(c => c.EnclosureLocation?.Panel == Panel.Back) ?? allCameras.FirstOrDefault();
            if (selectedCamera != null)
            {
                settings.VideoDeviceId = selectedCamera.Id;
                settings.MemoryPreference = MediaCaptureMemoryPreference.Cpu;
                settings.StreamingCaptureMode = StreamingCaptureMode.Video;
            }

            // Init capturer and Frame reader
            _captureManager = new MediaCapture();
            await _captureManager.InitializeAsync(settings);
            var frameSource = _captureManager.FrameSources.Where(source => source.Value.Info.SourceKind == MediaFrameSourceKind.Color).First();

            // Convert the pixel formats
            var subtype = MediaEncodingSubtypes.Bgra8;
            //if (pixelFormat != BitmapPixelFormat.Bgra8)
            //{
            //    throw new Exception($"Pixelformat {pixelFormat} not supported yet. Add conversion here");
            //}

            // The overloads of CreateFrameReaderAsync with the format arguments will actually make a copy in FrameArrived
            BitmapSize outputSize = new BitmapSize { Width = width, Height = height };
            _frameReader = await _captureManager.CreateFrameReaderAsync(frameSource.Value, subtype, outputSize);
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

            await _frameReader.StartAsync();
            IsCapturing = true;
        }
    }

    public VideoFrame GetLatestFrame()
    {
        // The overloads of CreateFrameReaderAsync with the format arguments will actually return a copy so we don't have to copy again
        var frame = _frameReader.TryAcquireLatestFrame();

        var videoFrame = frame?.VideoMediaFrame?.GetVideoFrame();
        return videoFrame;
    }

    */

    // Modified for HL2 with custom resolution and framerate
    public async Task StartCapture(int width, int height, int frameRate)
    {
        if (_captureManager == null || _captureManager.CameraStreamState == CameraStreamState.Shutdown || _captureManager.CameraStreamState == CameraStreamState.NotStreaming)
        {
            if (_captureManager != null)
            {
                _captureManager.Dispose();
            }

            MediaFrameSource frameSource = null;
            MediaFrameSourceKind sourceKind = MediaFrameSourceKind.Color;
            var subtype = MediaEncodingSubtypes.Bgra8;

            var sourceGroups = await MediaFrameSourceGroup.FindAllAsync();

            // https://github.com/doughtmw/display-calibration-hololens/blob/35d1437f8f3b62dc33379a2aaa14e788dd309e0c/unity-sandbox/HoloLens2-Display-Calibration/Assets/Scripts/MediaCaptureUtility.cs#L156C13-L156C24
            var sourceInfo =
                sourceGroups.SelectMany(group => group.SourceInfos)
                .FirstOrDefault(
                    si =>
                    // Testing with Video Preview - 
                    // https://holodevelopers.slack.com/archives/C1CQKRQM6/p1605046698173100?thread_ts=1580916605.219700&cid=C1CQKRQM6
                        (si.MediaStreamType == MediaStreamType.VideoPreview) &&
                        (si.SourceKind == sourceKind) && 
                        (si.VideoProfileMediaDescription.Any(
                    desc =>
                        desc.Width == width &&
                        desc.Height == height &&
                        desc.FrameRate == frameRate)));

            if (sourceInfo != null)
            {
                var sourceGroup = sourceInfo.SourceGroup;

                _captureManager = new MediaCapture();

                // Setting SharingMode throws "The given key was not present in the dictionary exception" (according to the docs, it should be there...)
                // this means that saying "Take a picture" while mediacapture running is not yet possible. It will crash the application
                // because ExclusiveControl is the default option and it blocks access for other apps on the device while running.
                // Restart the HoloLens after the app crashed to clear camera access or the app wont work.
                /*
                _captureSettings = new MediaCaptureInitializationSettings();
                _captureSettings.SourceGroup = sourceGroup;
                _captureSettings.SharingMode = MediaCaptureSharingMode.SharedReadOnly;  // Looks like ExclusiveControl blocks taking screenshots on the device
                _captureSettings.StreamingCaptureMode = StreamingCaptureMode.Video;     // Audio not needed
                _captureSettings.MemoryPreference = MediaCaptureMemoryPreference.Cpu;   // We need softwarebitmaps
                */

                await _captureManager.InitializeAsync(
                new MediaCaptureInitializationSettings()
                {
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                    SourceGroup = sourceGroup,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                });

                frameSource = _captureManager.FrameSources[sourceInfo.Id];

                var selectedFormat = frameSource.SupportedFormats.First(
                    format => format.VideoFormat.Width == width && format.VideoFormat.Height == height &&
                    format.FrameRate.Numerator / format.FrameRate.Denominator == frameRate);

                await frameSource.SetFormatAsync(selectedFormat);

                _frameReader = await _captureManager.CreateFrameReaderAsync(frameSource, subtype);
                _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

                await _frameReader.StartAsync();
                IsCapturing = true;
            }
        }
    }

    public MediaFrameReference GetLatestFrameRef()
    {
        // The overloads of CreateFrameReaderAsync with the format arguments will actually return a copy so we don't have to copy again
        var mediaFrameReference = _frameReader.TryAcquireLatestFrame();
        return mediaFrameReference;
    }

#endif

    public async Task StopCapturing()
    {
#if ENABLE_WINMD_SUPPORT
        if (_captureManager != null && _captureManager.CameraStreamState != CameraStreamState.Shutdown)
        {
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _captureManager.Dispose();
            _captureManager = null;
        }
        IsCapturing = false;
#endif
    }
}
