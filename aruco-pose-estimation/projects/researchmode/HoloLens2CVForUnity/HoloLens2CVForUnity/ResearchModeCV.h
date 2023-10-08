#pragma once
#include "ResearchModeCV.g.h"

namespace winrt::HoloLens2CVForUnity::implementation
{
    struct ResearchModeCV : ResearchModeCVT<ResearchModeCV>
    {
        ResearchModeCV();
        static HRESULT CheckCamConsent();

        bool LFImageUpdated();
        bool RFImageUpdated();
        bool ArUcoDetectionsUpdated();

        int32_t GetDetectedMarkersCount();
        int32_t GetFrameProcessingTime();

        Windows::Foundation::Collections::IVector<DetectedArUcoMarker> GetDetectedMarkers();

        com_array<uint8_t> GetLFCameraBuffer(int64_t& ts);
        com_array<uint8_t> GetRFCameraBuffer(int64_t& ts);

        void SetReferenceCoordinateSystem(Windows::Perception::Spatial::SpatialCoordinateSystem refCoord);

        void Configure(
            int _sensor, 
            bool _enableBuffer, 
            bool _enableArUcoDetector,
            float _markerSize, 
            int _dictId);

        void SetCameraIntrinsics(  
            int _cameraType,                                                           
            Windows::Foundation::Numerics::float2 _focalLength,
            Windows::Foundation::Numerics::float2 _principalPoint,
            Windows::Foundation::Numerics::float3 _radialDistortion,
            Windows::Foundation::Numerics::float2 _tangentialDistortion);

        void InitializeSpatialCamerasFront();
        void StartSpatialCamerasFrontLoop();
        void StopAllSensorDevice();

        std::mutex mu;

    private:

        float m_markerLength;
        int m_sensor;
        int m_dictId;
        int m_frameProcessingTime = 0;
        bool m_enableBuffer;
        bool m_enableArUcoDetector;

        struct CameraIntrinsics
        {
            Windows::Foundation::Numerics::float2 focalLength;
            Windows::Foundation::Numerics::float2 principalPoint;
            Windows::Foundation::Numerics::float3 radialDistortion;
            Windows::Foundation::Numerics::float2 tangentialDistortion;
        };

        CameraIntrinsics m_LFCameraIntrinsics;
        CameraIntrinsics m_RFCameraIntrinsics;

        UINT8* m_LFImage = nullptr;
        UINT8* m_RFImage = nullptr;

        IResearchModeSensor* m_LFSensor = nullptr;
        IResearchModeCameraSensor* m_LFCameraSensor = nullptr;
        IResearchModeSensor* m_RFSensor = nullptr;
        IResearchModeCameraSensor* m_RFCameraSensor = nullptr;

        ResearchModeSensorResolution m_LFResolution;
        ResearchModeSensorResolution m_RFResolution;
        IResearchModeSensorDevice* m_pSensorDevice = nullptr;
        std::vector<ResearchModeSensorDescriptor> m_sensorDescriptors;
        IResearchModeSensorDeviceConsent* m_pSensorDeviceConsent = nullptr;
        Windows::Perception::Spatial::SpatialLocator m_locator = 0;
        Windows::Perception::Spatial::SpatialCoordinateSystem m_refFrame = nullptr;

        std::atomic_int m_LFbufferSize = 0;
        std::atomic_int m_RFbufferSize = 0;

        std::atomic_bool m_LFImageUpdated = false;
        std::atomic_bool m_RFImageUpdated = false;

        std::atomic_bool m_spatialCamerasFrontLoopStarted = false;

        static void SpatialCamerasFrontLoop(ResearchModeCV* pResearchModeCV);
        static void CamAccessOnComplete(ResearchModeSensorConsent consent);

        static void ProcessSensorImageWithArUco(const BYTE* pImage,
            ResearchModeSensorResolution resolution, 
            DirectX::XMMATRIX cameraToWorld, 
            CameraIntrinsics camIntrinsics, 
            int dictId, 
            float markerLenght, 
            int& frameProcessingTime,
            Windows::Foundation::Collections::IVector<DetectedArUcoMarker>& detectedMarkers);

        DirectX::XMFLOAT4X4 m_LFCameraPose;
        DirectX::XMMATRIX m_LFCameraPoseInvMatrix;
        DirectX::XMFLOAT4X4 m_RFCameraPose;
        DirectX::XMMATRIX m_RFCameraPoseInvMatrix;

        static long long checkAndConvertUnsigned(UINT64 val);

        std::thread* m_pSpatialCamerasFrontUpdateThread;

        static DirectX::XMMATRIX ResearchModeCV::SpatialLocationToDxMatrix(Windows::Perception::Spatial::SpatialLocation location);

        struct Frame {
            UINT64 timestamp;     // QPC 
            int64_t timestamp_ft; // FileTime
            UINT8* image = nullptr;
        };

        struct SpatialCameraFrame {
            Frame LFFrame;
            Frame RFFrame;
        } m_lastSpatialFrame;

        std::atomic_bool m_ArUcoDetectionsUpdated = false;
        Windows::Foundation::Collections::IVector<DetectedArUcoMarker> m_detectedMarkers;
    };
}
namespace winrt::HoloLens2CVForUnity::factory_implementation
{
    struct ResearchModeCV : ResearchModeCVT<ResearchModeCV, implementation::ResearchModeCV>
    {
    };
}
