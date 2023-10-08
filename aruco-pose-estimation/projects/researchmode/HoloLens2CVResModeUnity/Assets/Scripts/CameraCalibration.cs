using UnityEngine;
using System;
using TMPro;

#if ENABLE_WINMD_SUPPORT
using HoloLens2CVForUnity;
using Microsoft.MixedReality.OpenXR;
#endif

public class CameraCalibration : MonoBehaviour
{
    TCPClient tcpClient;                                  // handles socket communications
    public TextMeshPro HUD;                               // hud to display the current status
    private int imagesSaved = 0;                          // https://answers.opencv.org/question/60786/how-many-images-for-camera-calibration/

    public GameObject LFPreviewPlane = null;              // plane to display Left Front camera image
    private Material LFMediaMaterial = null;
    private Texture2D LFMediaTexture = null;
    private byte[] LFFrameData = null;

    public GameObject RFPreviewPlane = null;              // plane to display Right Front camera image
    private Material RFMediaMaterial = null;
    private Texture2D RFMediaTexture = null;
    private byte[] RFFrameData = null;

    bool captureImages = false;

#if ENABLE_WINMD_SUPPORT
ResearchModeCV resModeCV;
Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
#endif

    private void Awake()
    {
        // get reference coordinate system
#if ENABLE_WINMD_SUPPORT
    try 
	{	        
		unityWorldOrigin = PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as Windows.Perception.Spatial.SpatialCoordinateSystem;
Debug.Log("Obtained reference coordinate system");
	}
	catch (global::System.Exception)
	{
		throw;
	}
#endif
    }

    // Start is called before the first frame update
    void Start()
    {
        // set LF & RF preview plane textures
        if (LFPreviewPlane != null)                                            
        {
            LFMediaMaterial = LFPreviewPlane.GetComponent<MeshRenderer>().material;
            LFMediaTexture = new Texture2D(640, 480, TextureFormat.Alpha8, false);
            LFMediaMaterial.mainTexture = LFMediaTexture;
        }

        if (RFPreviewPlane != null)
        {
            RFMediaMaterial = RFPreviewPlane.GetComponent<MeshRenderer>().material;
            RFMediaTexture = new Texture2D(640, 480, TextureFormat.Alpha8, false);
            RFMediaMaterial.mainTexture = RFMediaTexture;
        }

        tcpClient = GetComponent<TCPClient>();
        tcpClient.ConnectToServerEvent();

#if ENABLE_WINMD_SUPPORT
        HUD.text = "Initializing ...";

        resModeCV = new ResearchModeCV();
        resModeCV.SetReferenceCoordinateSystem(unityWorldOrigin);
        resModeCV.Configure(1, true, false, 0.55f, 0);
        resModeCV.InitializeSpatialCamerasFront();
        resModeCV.StartSpatialCamerasFrontLoop();

        HUD.text = "Real time preview started";
#endif

    }

    // Update is called once per frame
    void LateUpdate()
    {
        // Check if Button A of the Xbox controller is pressed
        if (Input.GetKeyDown(KeyCode.JoystickButton0))
        {
            captureImages = true;
        }
#if ENABLE_WINMD_SUPPORT
        if (resModeCV.LFImageUpdated() && resModeCV.RFImageUpdated())
        {
            UpdatePreviewTexturesAndSaveImages();   // preview current spatial images
        }
#endif
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) StopSensorsEvent();
    }

    void StopSensorsEvent()
    {
#if ENABLE_WINMD_SUPPORT
resModeCV.StopAllSensorDevice();
#endif
    }

    public void UpdatePreviewTexturesAndSaveImages()
    {
        long ts_ft_left = 0, ts_ft_right = 0;

        byte[] LFImage = null;
        byte[] RFImage = null;

#if ENABLE_WINMD_SUPPORT

        // update LF camera texture
        if (LFPreviewPlane != null && resModeCV.LFImageUpdated())
        {
            LFImage = resModeCV.GetLFCameraBuffer(out ts_ft_left);
            if (LFImage.Length > 0)
            {
                if (LFFrameData == null)
                {
                    LFFrameData = LFImage;
                }
                else
                {
                    System.Buffer.BlockCopy(LFImage, 0, LFFrameData, 0, LFFrameData.Length);
                }

                LFMediaTexture.LoadRawTextureData(LFFrameData);
                LFMediaTexture.Apply();
            }
        }

        // update RF camera texture
        if (RFPreviewPlane != null && resModeCV.RFImageUpdated())
        {

            RFImage = resModeCV.GetRFCameraBuffer(out ts_ft_right);
            if (RFImage.Length > 0)
            {
                if (RFFrameData == null)
                {
                    RFFrameData = RFImage;
                }
                else
                {
                    System.Buffer.BlockCopy(RFImage, 0, RFFrameData, 0, RFFrameData.Length);
                }

                RFMediaTexture.LoadRawTextureData(RFFrameData);
                RFMediaTexture.Apply();
            }
        }

        // saving images
        if (captureImages)
        {
           if (tcpClient.Connected)
	       {
#if WINDOWS_UWP
               // get time stamp from file time
               Windows.Perception.PerceptionTimestamp ts_left = Windows.Perception.PerceptionTimestampHelper.FromHistoricalTargetTime(DateTime.FromFileTime(ts_ft_left));
               Windows.Perception.PerceptionTimestamp ts_right = Windows.Perception.PerceptionTimestampHelper.FromHistoricalTargetTime(DateTime.FromFileTime(ts_ft_right));

               // convert to unix time
               long ts_unix_left = ts_left.TargetTime.ToUnixTimeMilliseconds();
               long ts_unix_right = ts_right.TargetTime.ToUnixTimeMilliseconds();
               long ts_unix_current = GetCurrentTimestampUnix();

               // send images
               if (tcpClient != null)
               {
                    if (LFImage != null && RFImage != null)
	                {
                          tcpClient.SendSpatialImageAsync(LFImage, RFImage, ts_unix_left, ts_unix_right);
	                }
               }
               imagesSaved++;
               HUD.text = "[" + imagesSaved + "] images saved to server";
#endif
	       }
           else
	       {
               HUD.text = "TCP connection failed, retrying ...";
               tcpClient.ConnectToServerEvent();
	       }
           captureImages = false;
       }
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
