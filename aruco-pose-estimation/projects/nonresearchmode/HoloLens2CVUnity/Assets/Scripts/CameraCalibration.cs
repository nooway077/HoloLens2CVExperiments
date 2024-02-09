using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using UnityEngine;
using TMPro;

#if ENABLE_WINMD_SUPPORT
using Windows.Graphics.Imaging;
using Microsoft.MixedReality.OpenXR;
#endif

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}

public class CameraCalibration : MonoBehaviour
{
    public TextMeshPro HUD;                                         // Hud to display the current status
    public CameraUtils.MediaCaptureProfiles mediaCaptureProfile;    // Allows the selection of camera capture profiles with different resolutions || HL2_896x504 should be used!
    public GameObject PVPreviewPlane = null;                        // Plane to display PV camera image

    private Material PVMediaMaterial = null;
    private Texture2D PVMediaTexture = null;
    private byte[] PVFrameData = null;
    private byte[] PVImageBuffer = null;
    private bool sendingPVImage = false;

    int _imagesSaved = 0;                                           // https://answers.opencv.org/question/60786/how-many-images-for-camera-calibration/
    bool _isRunning = false;

    int _imageWidth = 0;
    int _imageHeight = 0;

#if ENABLE_WINMD_SUPPORT
    MediaCapturer _mediaCapturer = null;
    TCPClient _tcpClient = null;
#endif

    // Start is called before the first frame update
    async void Start()
    {
        try
        {
            HUD.text = "Initializing ...";

            // Setup mediacapture specs
            int frameRate = 30;

            switch (mediaCaptureProfile)
            {
                case CameraUtils.MediaCaptureProfiles.HL2_2272x1278:
                    _imageWidth = 2272;
                    _imageHeight = 1278;
                    break;
                case CameraUtils.MediaCaptureProfiles.HL2_896x504:
                    _imageWidth = 896;
                    _imageHeight = 504;
                    break;

                case CameraUtils.MediaCaptureProfiles.HL2_1280x720:
                    _imageWidth = 1280;
                    _imageHeight = 720;
                    break;

                default:
                    _imageWidth = 0;
                    _imageHeight = 0;
                    break;
            }

            // Setup Preview Plane
            if (PVPreviewPlane != null)
            {
                PVMediaMaterial = PVPreviewPlane.GetComponent<MeshRenderer>().material;
                PVMediaTexture = new Texture2D(_imageWidth, _imageHeight, TextureFormat.BGRA32, false); // https://docs.unity3d.com/ScriptReference/TextureFormat.html
                PVMediaMaterial.mainTexture = PVMediaTexture;
            }

#if ENABLE_WINMD_SUPPORT
            try
            {
                _tcpClient = GetComponent<TCPClient>();
                _tcpClient.ConnectToServerEvent();

                _mediaCapturer = new MediaCapturer();
                await _mediaCapturer.StartCapture(_imageWidth, _imageHeight, frameRate);

                HUD.text = "Camera started. Running!";
            }
            catch (Exception ex)
            {
                HUD.text = "Failed to start camera: " + ex.Message;
            }

            // Run processing loop in separate parallel Task
            _isRunning = true;

            await Task.Run(async () =>
            {
                while (_isRunning)
                {
                    if (_mediaCapturer.IsCapturing)
                    {
                        var mediaFrameReference = _mediaCapturer.GetLatestFrameRef();
                        HandlePreviewTextureUpdate(mediaFrameReference);
                        mediaFrameReference?.Dispose();
                    }
                    else
	                {
                        return;
	                }
                }
            });
#endif
        }
        catch (Exception ex)
        {
            HUD.text = "Task failed succesfully: " + ex.Message;
        }
    }

    // Update is called once per frame
    async void LateUpdate()
    {
        // Check if Xbox Controller's A button is pressed
        if (Input.GetKeyDown(KeyCode.JoystickButton0))
        {
            sendingPVImage = true;
            SavePVImageEvent();
        }
    }

    void SavePVImageEvent()
    {
#if ENABLE_WINMD_SUPPORT
#if WINDOWS_UWP
        if (_tcpClient != null)
        {
            long ts = GetCurrentTimestampUnix();
            _tcpClient.SendPVImageAsync(PVImageBuffer, ts);
            _imagesSaved++;
            Debug.Log("Image with " + ts + " timestamp and " + PVImageBuffer.Length + " data length saved");
        }
        sendingPVImage = false;
#endif
#endif
    }

#if ENABLE_WINMD_SUPPORT
    unsafe void HandlePreviewTextureUpdate(Windows.Media.Capture.Frames.MediaFrameReference mediaFrameReference)
    {
        // Request software bitmap from media frame reference
        var softwareBitmap = mediaFrameReference?.VideoMediaFrame?.SoftwareBitmap;

        if (softwareBitmap != null)
	    {
            // https://stackoverflow.com/questions/34291291/how-to-get-byte-array-from-softwarebitmap
            // Get byte array from softwareBitmap
            using (var input = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Read))
            using (var inputReference = input.CreateReference())
            {
                byte* inputBytes = null;
                uint inputCapacity = 0;
                ((IMemoryBufferByteAccess)inputReference).GetBuffer(out inputBytes, out inputCapacity);

                // https://stackoverflow.com/questions/17569419/c-sharp-convert-unsafe-byte-to-byte
                int len = _imageWidth * _imageHeight * 4;   // width * height * channels (format is BGRA8, set in MediaCapturer.cs aka subtype)
                byte[] PVImage = new byte[len];
                Marshal.Copy((IntPtr)inputBytes, PVImage, 0, len);

                if (PVFrameData == null)
                {
                    PVFrameData = PVImage;
                }
                else
                {
                    System.Buffer.BlockCopy(PVImage, 0, PVFrameData, 0, PVFrameData.Length);
                }

                // Update PV Image buffer (this is for sending images via TCP)
                if (!sendingPVImage)
	            {
                    PVImageBuffer = PVImage;
	            }

                // Update Texture & UI
                UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                {
                    PVMediaTexture.LoadRawTextureData(PVFrameData);
                    PVMediaTexture.Apply();

                    HUD.text = "Realtime Preview Running || Press [A] Button to save a picture" +
                    "\n " + _imagesSaved + " images saved";
                }, false);
            }
	    }

        // Dispose of the bitmap
        softwareBitmap?.Dispose();
    }
#endif

    private async void OnApplicationFocus(bool focus)
    {
#if ENABLE_WINMD_SUPPORT
       if (!focus) await _mediaCapturer.StopCapturing();
#endif
    }

#if WINDOWS_UWP
    private long GetCurrentTimestampUnix()
    {
        // Get the current time, in order to create a PerceptionTimestamp. 
        Windows.Globalization.Calendar c = new Windows.Globalization.Calendar();
        Windows.Perception.PerceptionTimestamp ts = Windows.Perception.PerceptionTimestampHelper.FromHistoricalTargetTime(c.GetDateTime());
        return ts.TargetTime.ToUnixTimeMilliseconds();
        //return ts.SystemRelativeTargetTime.Ticks;
    }
    private Windows.Perception.PerceptionTimestamp GetCurrentTimestamp()
    {
        // Get the current time, in order to create a PerceptionTimestamp. 
        Windows.Globalization.Calendar c = new Windows.Globalization.Calendar();
        return Windows.Perception.PerceptionTimestampHelper.FromHistoricalTargetTime(c.GetDateTime());
    }
#endif
}
