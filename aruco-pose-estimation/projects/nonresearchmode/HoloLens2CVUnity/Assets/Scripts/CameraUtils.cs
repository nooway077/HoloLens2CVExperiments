using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class CameraUtils
{
    // https://learn.microsoft.com/en-us/windows/mixed-reality/develop/advanced-concepts/locatable-camera-overview
    public enum MediaCaptureProfiles
    {
        HL2_2272x1278,
        HL2_896x504,
        HL2_1280x720
    }

    // https://github.com/doughtmw/display-calibration-hololens/blob/main/unity-sandbox/HoloLens2-Display-Calibration/Assets/Scripts/NetworkBehaviour.cs#L641
#if ENABLE_WINMD_SUPPORT
public static Matrix4x4 GetViewToUnityTransform(
        Windows.Perception.Spatial.SpatialCoordinateSystem frameCoordinateSystem, 
        Windows.Perception.Spatial.SpatialCoordinateSystem unityCoordinateSystem)
    {
        if (frameCoordinateSystem == null || unityCoordinateSystem == null)
        {
            return Matrix4x4.identity;
        }

        // Get the reference transform from camera frame to unity space
        System.Numerics.Matrix4x4? cameraToUnityRef = frameCoordinateSystem.TryGetTransformTo(unityCoordinateSystem);

        // Return identity if value does not exist
        if (!cameraToUnityRef.HasValue)
            return Matrix4x4.identity;

        // No cameraViewTransform available currently, using identity for HL2
        // Inverse of identity is identity
        var viewToCamera = Matrix4x4.identity;
        var cameraToUnity = ArUcoUtils.Mat4x4FromFloat4x4(cameraToUnityRef.Value);

        // Compute transform to relate winrt coordinate system with unity coordinate frame (viewToUnity)
        // WinRT transfrom -> Unity transform by transpose and flip row 3
        var viewToUnityWinRT = viewToCamera * cameraToUnity;
        var viewToUnity = Matrix4x4.Transpose(viewToUnityWinRT);
        viewToUnity.m20 *= -1.0f;
        viewToUnity.m21 *= -1.0f;
        viewToUnity.m22 *= -1.0f;
        viewToUnity.m23 *= -1.0f;

        return viewToUnity;
    }
#endif
}
