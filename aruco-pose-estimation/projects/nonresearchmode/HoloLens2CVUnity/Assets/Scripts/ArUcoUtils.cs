// This script is originally from Mitchell Doughty's repo: https://github.com/doughtmw/display-calibration-hololens/blob/main/unity-sandbox/HoloLens2-Display-Calibration/Assets/Scripts/ArUcoUtils.cs
// with some modifications

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public abstract class ArUcoUtils
{
    // https://docs.opencv.org/trunk/dc/df7/dictionary_8hpp.html
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

    // Convert from system numerics to unity Vector 3
    public static Vector3 Vec3FromFloat3(System.Numerics.Vector3 v)
    {
        return new Vector3()
        {
            x = v.X,
            y = v.Y,
            z = v.Z
        };
    }

    public static Vector3 GetVectorFromMatrix(Matrix4x4 m)
    {
        return m.GetColumn(3);
    }

    public static Quaternion GetQuatFromMatrix(Matrix4x4 m)
    {
        return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
    }

    /// <summary>
    /// https://github.com/EnoxSoftware/OpenCVForUnity/blob/a681093ccf8cc0d69a7cd2356dd5e29c1f854495/Assets/OpenCVForUnity/Examples/ContribModules/aruco/ArUcoExample/ArUcoExample.cs#L236
    /// </summary>
    /// <param name="v"></param>
    /// <param name="q"></param>
    /// <returns></returns>
    public static Matrix4x4 Matrix4x4InUnitySpace(Vector3 v, Quaternion q)
    {
        // Create the matrix from input
        var M = Matrix4x4.TRS(v, q, Vector3.one);
        var F = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, -1, 1));

        // (FMF)*x
        // Right-handed coordinates (OpenCV) to left-handed (Unity)
        // https://stackoverflow.com/questions/30234945/change-handedness-of-a-row-major-4x4-transformation-matrix
        return F * M * F;
    }

    // Convert from system numerics to unity matrix 4x4
    public static Matrix4x4 Mat4x4FromFloat4x4(System.Numerics.Matrix4x4 m)
    {
        return new Matrix4x4()
        {
            m00 = m.M11,
            m10 = m.M21,
            m20 = m.M31,
            m30 = m.M41,

            m01 = m.M12,
            m11 = m.M22,
            m21 = m.M32,
            m31 = m.M42,

            m02 = m.M13,
            m12 = m.M23,
            m22 = m.M33,
            m32 = m.M43,

            m03 = m.M14,
            m13 = m.M24,
            m23 = m.M34,
            m33 = m.M44,
        };
    }

    // Get a rotation quaternion from rodrigues
    public static Quaternion RotationQuatFromRodrigues(Vector3 v)
    {
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
    }

    public static Vector3 WindowsVectorToUnityVector(System.Numerics.Vector3 v)
    {
        return new Vector3(v.X, v.Y, -v.Z);
    }

    /// <summary>
    /// Handle conversion of the incoming transform from c++ component
    /// to the correct configuration for the Unity coordinate system.
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    /// <returns></returns>
    public static Matrix4x4 GetTransformInUnityCamera(Vector3 pos, Quaternion rot)
    {
        // right-handed coordinates system (OpenCV) to left-handed one (Unity)
        var t = new Vector3(pos.x, -pos.y, pos.z);

        // Compose a matrix
        var T = Matrix4x4.TRS(t, rot, Vector3.one);
        T.m20 *= -1.0f;
        T.m21 *= -1.0f;
        T.m22 *= -1.0f;
        T.m23 *= -1.0f;

        return T;
    }

    /// <summary>
    /// Take the average of input array
    /// </summary>
    /// <param name="inArr"></param>
    /// <returns></returns>
    public static Vector3 ArrayAvg(
        Vector3[] inArr)
    {
        float xM = 0;
        float yM = 0;
        float zM = 0;

        foreach (Vector3 vec in inArr)
        {
            xM += vec.x;
            yM += vec.y;
            zM += vec.z;
        }

        return new Vector3(xM / inArr.Length, yM / inArr.Length, zM / inArr.Length);
    }

    /// <summary>
    /// Take the average of the input quaternion
    /// </summary>
    /// <param name="quat"></param>
    /// <returns></returns>
    public static Quaternion CalcAverageQuaternion(
    Quaternion[] quat)
    {
        Quaternion mean = quat[0];
        for (int i = 1; i < quat.Length; i++)
        {
            float weight = 1.0f / (i + 1);
            mean = Quaternion.Slerp(mean, quat[i], weight);
        }
        return mean;
    }
}
