using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
//using UnityEngine.XR.ARFoundation;

#if ENABLE_WINMD_SUPPORT
using Windows.Graphics.Imaging;
using Microsoft.MixedReality.OpenXR;
using OpenCVBridge;
#endif

public class ArUcoTracking : MonoBehaviour
{
    public TextMeshPro HUD;                                         // Hud to display the current status
    public float markerSize;                                        // Size of the printed aruco marker's side in meters
    public ArUcoUtils.ArUcoDictionary arUcoDictionary;              // The ArUco dictionary the marker is generated from
    public GameObject markerGo;                                     // Game object that is rendered on top of detected markers
    public CameraUtils.MediaCaptureProfiles mediaCaptureProfile;    // Allows the selection of camera capture profiles with different resolutions || HL2_896x504 should be used!
    public bool autoReleaseMarkerGos;                               // After a preset time, every instace of the markerGo will be removed
    public bool sendDetectedArUcoDataViaTCP;                        // Enables sending raw aruco data from OpenCV via TCP (position & rotation are relative to PV camera, no conversions done)
    public bool useCustomCameraIntrinsics;                          // Enables custom camera calibration parameters instead of quierying it from frames
    public CameraIntrinsics customCameraIntrinsics;                 // Holds the user defined calibration data

    List<GameObject> _markerGos = new List<GameObject>();
    int frameCounter = 0;
    bool _isRunning = false;
    CameraIntrinsics perFrameCameraIntrinsics;
    CameraIntrinsics _cameraIntrinsics;

#if ENABLE_WINMD_SUPPORT
    OpenCVHelper _cvHelper = null;
    MediaCapturer _mediaCapturer = null;
    TCPClient _tcpClient = null;

    Windows.Perception.Spatial.SpatialCoordinateSystem _unityCoordinateSystem = null;
    Windows.Perception.Spatial.SpatialCoordinateSystem _frameCoordinateSystem = null;
#endif

    // Awake is called when an enabled script instance is being loaded
    private void Awake()
    {
        // Get reference coordinate system
#if ENABLE_WINMD_SUPPORT
		_unityCoordinateSystem = PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as Windows.Perception.Spatial.SpatialCoordinateSystem;
#endif
    }

    // Start is called before the first frame update
    async void Start()
    {
        try
        {
            HUD.text = "Initializing ...";

            // Set markerGo's size & disable until markers detected
            markerGo.transform.localScale = new Vector3(markerSize, markerSize, markerSize);
            markerGo.SetActive(false);

            // Setup mediacapture specs
            int width = 0;
            int height = 0;
            int frameRate = 30;

            switch (mediaCaptureProfile)
            {
                case CameraUtils.MediaCaptureProfiles.HL2_2272x1278:
                    width = 2272;
                    height = 1278;
                    break;
                case CameraUtils.MediaCaptureProfiles.HL2_896x504:
                    width = 896;
                    height = 504;
                    break;

                case CameraUtils.MediaCaptureProfiles.HL2_1280x720:
                    width = 1280;
                    height = 720;
                    break;

                default:
                    width = 0;
                    height = 0;
                    break;
            }

#if ENABLE_WINMD_SUPPORT
            try
            {
                if (sendDetectedArUcoDataViaTCP)
	            {
                    _tcpClient = GetComponent<TCPClient>();
                    _tcpClient.ConnectToServerEvent();
	            }

                _cvHelper = new OpenCVHelper();

                _mediaCapturer = new MediaCapturer();
                await _mediaCapturer.StartCapture(width, height, frameRate);

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
                        HandleArUcoTracking(mediaFrameReference);
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

    private void Update()
    {
        if (frameCounter == 30)
        {
            if (autoReleaseMarkerGos)
            {
                for (int i = 0; i < _markerGos.Count; i++)
                {
                    GameObject.Destroy(_markerGos[i]);
                }
                _markerGos.Clear();
            }
            frameCounter = 0;
        }
        frameCounter++;
    }

    private async void OnApplicationFocus(bool focus)
    {
#if ENABLE_WINMD_SUPPORT
       if (!focus) await _mediaCapturer.StopCapturing();
#endif
    }

#if ENABLE_WINMD_SUPPORT
    private void HandleArUcoTracking(Windows.Media.Capture.Frames.MediaFrameReference mediaFrameReference)
    {
        // Request software bitmap from media frame reference
        var softwareBitmap = mediaFrameReference?.VideoMediaFrame?.SoftwareBitmap;

        // Request camera intrinsics from media frame reference
        var pFIntrinsics = mediaFrameReference?.VideoMediaFrame?.CameraIntrinsics;

        if (!useCustomCameraIntrinsics && pFIntrinsics != null) // when frame intrinsics = null, defaults to custom intrinsics
	    {
            // NullReferenceException, these are null per frame (unknown reason, yet to be investigated)
            // --> Per frame intrinsics is not yet supported, use custom camera intrinsics instead
            _cameraIntrinsics.focalLength = VectorExtensions.ToUnity(pFIntrinsics.FocalLength);
            _cameraIntrinsics.principalPoint = VectorExtensions.ToUnity(pFIntrinsics.PrincipalPoint);
            _cameraIntrinsics.radialDistortion = VectorExtensions.ToUnity(pFIntrinsics.RadialDistortion);
            _cameraIntrinsics.tangentialDistortion = VectorExtensions.ToUnity(pFIntrinsics.TangentialDistortion);
	    }
        else
	    {
            _cameraIntrinsics = customCameraIntrinsics;
	    }

        if (softwareBitmap != null)
	    {
            // Cache frame coordinate system
            _frameCoordinateSystem = mediaFrameReference.CoordinateSystem;

            DetectMarkers(softwareBitmap, _cameraIntrinsics);
	    }

        // Dispose of the bitmap
        softwareBitmap?.Dispose();
    }

    private void DetectMarkers(SoftwareBitmap softwareBitmap, CameraIntrinsics intrinsics)
    {
        int frameProcessingTime = 0;

        // Process softwarebitmap with OpenCV
        var markers = _cvHelper.ProcessWithArUco(
                        softwareBitmap, 
                        VectorExtensions.ToNumerics(intrinsics.focalLength), 
                        VectorExtensions.ToNumerics(intrinsics.principalPoint), 
                        VectorExtensions.ToNumerics(intrinsics.radialDistortion), 
                        VectorExtensions.ToNumerics(intrinsics.tangentialDistortion),
                        (int)arUcoDictionary,
                        markerSize,
                        out frameProcessingTime);

        if (markers.Count != 0)
        {
            // Iterate through the detected markers & place markerGos
            foreach (var marker in markers)
	        {
                UnityEngine.Vector3 translationUnity = ArUcoUtils.Vec3FromFloat3(marker.Position());
                UnityEngine.Vector3 rotationRodrigues = ArUcoUtils.Vec3FromFloat3(marker.Rotation());
                UnityEngine.Quaternion rotationUnity = ArUcoUtils.RotationQuatFromRodrigues(rotationRodrigues);

                UnityEngine.Matrix4x4 markerTransformUnityCamera = ArUcoUtils.GetTransformInUnityCamera(translationUnity, rotationUnity);
                UnityEngine.Matrix4x4 cameraToWorldUnity = CameraUtils.GetViewToUnityTransform(_frameCoordinateSystem, _unityCoordinateSystem);

                UnityEngine.Matrix4x4 transformUnityWorld = cameraToWorldUnity * markerTransformUnityCamera;

                UnityEngine.Vector3 markerPos = ArUcoUtils.GetVectorFromMatrix(transformUnityWorld);
                UnityEngine.Quaternion markerRot = ArUcoUtils.GetQuatFromMatrix(transformUnityWorld);

                // Update UI with detections
                UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                {
                    HUD.text = "Detected " + markers.Count + " markers" +
                    "\nLast camera frame processed in " + frameProcessingTime + " ms";

                    string markerText = "[Marker " + marker.Id() + "]";
                    string markerName = "marker" + marker.Id();

                    var instance = GameObject.Find(markerName);

                    if (instance != null)
	                {
                        // Update existing markerGo's position 
                        instance.transform.SetPositionAndRotation(markerPos, markerRot);
	                }
                    else
	                {
                        // Create a new instance of the markerGo to represent the marker
                        var newInstance = Instantiate(markerGo, markerPos, markerRot);
                        newInstance.name = markerName;
                        var tmp = newInstance.GetComponentInChildren<TextMeshProUGUI>();
                        tmp.SetText(markerText);
                        newInstance.SetActive(true);
                        _markerGos.Add(newInstance);
	                }

                    Debug.Log("marker [" + marker.Id() + "] pos xyz: " + markerPos.x + " " + markerPos.y + " " + markerPos.z);

                }, false);

                if (sendDetectedArUcoDataViaTCP)
                {
                    if (_tcpClient != null)
                    {
                        // Sending detected marker's data via TCP
                        string markerData = "Marker [" + marker.Id() + "] " +
                        "\ntranslation (XYZ): " + translationUnity.x + " " + translationUnity.y + " " + translationUnity.z + " " + 
                        "\nrotation (Rodrigues XYZ): " + + rotationRodrigues.x + " " + rotationRodrigues.y + " " + rotationRodrigues.z;
                        _tcpClient.SendMarkerDataAsync(markerData);
                    }      
                }
	        }
        }
        else
	    {
            // Update UI
            UnityEngine.WSA.Application.InvokeOnAppThread(() =>
            {
                HUD.text = "Detected " + markers.Count + " markers" +
                    "\nLast camera frame processed in " + frameProcessingTime + " ms";
            }, false);
	    }
    }
#endif
}

// Unity engine vector version of camera intrinsics class
// System.Numerics is not supported in the inspector ...
// variables must be converted to System.Numerics before passed to winRT
[Serializable]
public class CameraIntrinsics
{
    public UnityEngine.Vector2 focalLength;
    public UnityEngine.Vector2 principalPoint;
    public UnityEngine.Vector3 radialDistortion;
    public UnityEngine.Vector2 tangentialDistortion;
    // public int imageWidth;
    // public int imageHeight;
}

// https://stackoverflow.com/questions/76442537/is-there-a-way-to-define-cast-from-system-numerics-vector2-to-unityengine-vector
// https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods
public static class VectorExtensions
{
    public static UnityEngine.Vector2 ToUnity(this System.Numerics.Vector2 vec) => new UnityEngine.Vector2(vec.X, vec.Y);
    public static System.Numerics.Vector2 ToNumerics(this UnityEngine.Vector2 vec) => new System.Numerics.Vector2(vec.x, vec.y);

    public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 vec) => new UnityEngine.Vector3(vec.X, vec.Y, vec.Z);
    public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 vec) => new System.Numerics.Vector3(vec.x, vec.y, vec.z);
}

