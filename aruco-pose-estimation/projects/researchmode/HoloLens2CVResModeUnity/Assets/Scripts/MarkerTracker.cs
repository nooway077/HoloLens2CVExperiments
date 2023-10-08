using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq.Expressions;

#if ENABLE_WINMD_SUPPORT
using HoloLens2CVForUnity;
using Microsoft.MixedReality.OpenXR;
#endif

public class MarkerTracker : MonoBehaviour
{
    [Tooltip("Game object which will be rendered on top of the marker")]
    public GameObject markerGo;

    [Tooltip("Size of the printed aruco marker's side in meters")]
    public float markerSize;

    [Tooltip("Name of the ArUco dictionary the marker is generated from")]
    public ArUcoDictionary arUcoDictionary;

    [Tooltip("This sensor will be used for CV")]
    public Sensor sensor;

    public CameraIntrinsics LeftFrontCameraIntrinsics;    // LEFT Front camera intrinsics holder
    public CameraIntrinsics RightFrontCameraIntrinsics;   // RIGHT Front camera intrinsics holder

    public TextMeshPro HUD;                               // hud to display the current status

#if ENABLE_WINMD_SUPPORT
    ResearchModeCV _resModeCV = null;
    Windows.Perception.Spatial.SpatialCoordinateSystem _unityCoordinateSystem = null;
#endif

    private void Awake()
    {
        // get reference coordinate system
#if ENABLE_WINMD_SUPPORT
        _unityCoordinateSystem = PerceptionInterop.GetSceneCoordinateSystem(UnityEngine.Pose.identity) as Windows.Perception.Spatial.SpatialCoordinateSystem;
#endif
    }

    // Start is called before the first frame update
    void Start()
    {
        HUD.text = "Initilizing ...";
        try
        {
#if ENABLE_WINMD_SUPPORT
            _resModeCV = new ResearchModeCV();

            _resModeCV.SetReferenceCoordinateSystem(_unityCoordinateSystem);

            _resModeCV.SetCameraIntrinsics(0,
                VectorExtensions.ToNumerics(LeftFrontCameraIntrinsics.focalLength),
                VectorExtensions.ToNumerics(LeftFrontCameraIntrinsics.principalPoint),
                VectorExtensions.ToNumerics(LeftFrontCameraIntrinsics.radialDistortion),
                VectorExtensions.ToNumerics(LeftFrontCameraIntrinsics.tangentialDistortion));

            _resModeCV.SetCameraIntrinsics(1,
                VectorExtensions.ToNumerics(RightFrontCameraIntrinsics.focalLength),
                VectorExtensions.ToNumerics(RightFrontCameraIntrinsics.principalPoint),
                VectorExtensions.ToNumerics(RightFrontCameraIntrinsics.radialDistortion),
                VectorExtensions.ToNumerics(RightFrontCameraIntrinsics.tangentialDistortion));

            _resModeCV.Configure((int)sensor, false, true, markerSize, (int)arUcoDictionary);

            _resModeCV.InitializeSpatialCamerasFront();
            _resModeCV.StartSpatialCamerasFrontLoop();
#endif
            HUD.text = "Sensor loop is running!";
        }
        catch (Exception ex)
        {
            HUD.text = "Failed to start sensor loop: " + ex.Message;
        }

        markerGo.transform.localScale = new Vector3(markerSize, markerSize, markerSize);
        markerGo.SetActive(false);
    }

    // Update is called once per frame
    void LateUpdate()
    {
#if ENABLE_WINMD_SUPPORT
        HUD.text = "ArUco detection count: " + _resModeCV.GetDetectedMarkersCount() +
        "\nLast camera frame processing time: " + _resModeCV.GetFrameProcessingTime() + " ms" +
        "\n Sensor: " + sensor;
#endif
        try
        {
#if ENABLE_WINMD_SUPPORT
            IList<DetectedArUcoMarker> detectedArUcoMarkers = _resModeCV.GetDetectedMarkers();
            if (detectedArUcoMarkers.Count != 0)
	        {   
                // currently only marker 0 is displayed

                UnityEngine.Vector3 markerPosUnity = VectorExtensions.ToUnity(detectedArUcoMarkers[0].Position());
                //markerPosUnity.y *= -1f;

                UnityEngine.Vector3 markerRotRodrigues = VectorExtensions.ToUnity(detectedArUcoMarkers[0].Rotation());
                UnityEngine.Quaternion markerRotUnity = VectorExtensions.GetRotation(markerRotRodrigues);

                UnityEngine.Matrix4x4 cameraToWorld = MatrixExtensions.ToUnity(detectedArUcoMarkers[0].CameraToWorldUnity());
                UnityEngine.Matrix4x4 markerToCam = MatrixExtensions.TransformInUnitySpace(markerPosUnity, markerRotUnity);

                UnityEngine.Matrix4x4 markerLocationUnity = cameraToWorld * markerToCam;

                UnityEngine.Vector3 pos = VectorExtensions.GetTranslation(markerLocationUnity);
                UnityEngine.Quaternion rot = VectorExtensions.GetRotation(markerLocationUnity);

                markerGo.transform.SetPositionAndRotation(pos, rot);
                markerGo.SetActive(true);

                Debug.Log("markerPos xyz: " + pos.x + " " + pos.y + " " + pos.z);
            }
#endif
        }
        catch (Exception ex)
        {
            HUD.text = "Failed to update aruco detections: " + ex.Message;
        }
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) StopSensorsEvent();
    }

    void StopSensorsEvent()
    {
#if ENABLE_WINMD_SUPPORT
        _resModeCV.StopAllSensorDevice();
#endif
    }

    public enum ArUcoDictionary
    {
        DICT_4X4_50 = 0,
        DICT_4X4_100,
        DICT_4X4_250,
        DICT_4X4_1000,
        DICT_5X5_50,
        DICT_5X5_100,
        DICT_5X5_250,
        DICT_5X5_1000,
        DICT_6X6_50,
        DICT_6X6_100,
        DICT_6X6_250,
        DICT_6X6_1000,
        DICT_7X7_50,
        DICT_7X7_100,
        DICT_7X7_250,
        DICT_7X7_1000,
        DICT_ARUCO_ORIGINAL,
        DICT_APRILTAG_16h5,
        DICT_APRILTAG_25h9,
        DICT_APRILTAG_36h10,
        DICT_APRILTAG_36h11
    }

    public enum Sensor { LeftFront = 0, RightFront }
}

// unity engine vector version of camera intrinsics class
// System.Numerics is not supported in the inspector ...
// variables must be converted to System.Numerics before passed to winRT
[Serializable]
public class CameraIntrinsics
{
    public UnityEngine.Vector2 focalLength;
    public UnityEngine.Vector2 principalPoint;
    public UnityEngine.Vector3 radialDistortion;
    public UnityEngine.Vector2 tangentialDistortion;
    // public int imageWidth;  Not required, queried directly from sensor
    // public int imageHeight; Not required, queried directly from sensor
}

// https://stackoverflow.com/questions/76442537/is-there-a-way-to-define-cast-from-system-numerics-vector2-to-unityengine-vector
// https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods
public static class VectorExtensions
{
    public static UnityEngine.Vector2 ToUnity(this System.Numerics.Vector2 vec) => new UnityEngine.Vector2(vec.X, vec.Y);
    public static System.Numerics.Vector2 ToNumerics(this UnityEngine.Vector2 vec) => new System.Numerics.Vector2(vec.x, vec.y);

    public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 vec) => new UnityEngine.Vector3(vec.X, vec.Y, vec.Z);
    public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 vec) => new System.Numerics.Vector3(vec.x, vec.y, vec.z);

    // https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/ @numberkruncher
    public static UnityEngine.Vector3 GetTranslation(this UnityEngine.Matrix4x4 matrix)
    {
        Vector3 translation;
        translation.x = matrix.m03;
        translation.y = matrix.m13;
        translation.z = matrix.m23;
        return translation;
    }

    // https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/ @numberkruncher
    public static Quaternion GetRotation(this UnityEngine.Matrix4x4 matrix)
    {
        Vector3 forward;
        forward.x = matrix.m02;
        forward.y = matrix.m12;
        forward.z = matrix.m22;

        Vector3 upwards;
        upwards.x = matrix.m01;
        upwards.y = matrix.m11;
        upwards.z = matrix.m21;

        return Quaternion.LookRotation(forward, upwards);
    }

    public static Quaternion GetRotation(this UnityEngine.Vector3 v)
    {
        /*
        var angle = Mathf.Rad2Deg * v.magnitude;
        var axis = v.normalized;
        Quaternion q = Quaternion.AngleAxis(angle, axis);

        // Ensure: 
        // Positive x axis is in the left direction of the observed marker
        // Positive y axis is in the upward direction of the observed marker
        // Positive z axis is facing outward from the observed marker
        // Convert from rodrigues to quaternion representation of angle
        q = Quaternion.Euler(
            -1.0f * q.eulerAngles.x,
            q.eulerAngles.y,
            -1.0f * q.eulerAngles.z) * Quaternion.Euler(0, 0, 180);

        return q;
        */

        // https://answers.opencv.org/question/110441/use-rotation-vector-from-aruco-in-unity3d/

        float theta = (float)(Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z) * 180 / Math.PI);
        Vector3 axis = new Vector3(v.x, v.y, v.z);
        Quaternion rot = Quaternion.AngleAxis(theta, axis);

        return rot;
    }
}

public static class MatrixExtensions
{
    public static UnityEngine.Matrix4x4 ToUnity(this System.Numerics.Matrix4x4 matrix) => new UnityEngine.Matrix4x4()
    {
        m00 = matrix.M11,
        m01 = matrix.M12,
        m02 = matrix.M13,
        m03 = matrix.M14,

        m10 = matrix.M21,
        m11 = matrix.M22,
        m12 = matrix.M23,
        m13 = matrix.M24,

        m20 = matrix.M31,
        m21 = matrix.M32,
        m22 = matrix.M33,
        m23 = matrix.M34,

        m30 = matrix.M41,
        m31 = matrix.M42,
        m32 = matrix.M43,
        m33 = matrix.M44,
    };
    public static UnityEngine.Matrix4x4 TransformInUnitySpace(UnityEngine.Vector3 v, Quaternion q)
    {
        // https://forum.unity.com/threads/trouble-with-transform-matrix-order-i-think.83732/

        var t = Matrix4x4.Translate(v);
        var r = Matrix4x4.Rotate(q);
        var s = Matrix4x4.Scale(UnityEngine.Vector3.one);
        var trs = t * r * s;

        return trs;
    }
}

