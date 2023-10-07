#pragma once
#include "OpenCVHelper.g.h"

namespace winrt::OpenCVBridge::implementation
{
    struct OpenCVHelper : OpenCVHelperT<OpenCVHelper>
    {
        OpenCVHelper() = default;

        Windows::Foundation::Collections::IVector<DetectedMarker> ProcessWithArUco(
            Windows::Graphics::Imaging::SoftwareBitmap input,
            Windows::Foundation::Numerics::float2 focalLength,
            Windows::Foundation::Numerics::float2 principalPoint,
            Windows::Foundation::Numerics::float3 radialDistortion,
            Windows::Foundation::Numerics::float2 tangentialDistortion,
            int dictionaryId,
            float markerLength,
            int& frameProcessingTime);

    private:
     
        // https://github.com/microsoft/Windows-universal-samples/blob/main/Samples/CameraOpenCV/shared/OpenCVBridge/OpenCVHelper.cpp#L150
        bool TryConvert(
            __in Windows::Graphics::Imaging::SoftwareBitmap from,
            __out cv::Mat& convertedMat);

           
        bool GetPointerToPixelData(
            Windows::Graphics::Imaging::SoftwareBitmap bitmap,
            unsigned char** pPixelData,
            unsigned int* capacity);
         
    };
}
namespace winrt::OpenCVBridge::factory_implementation
{
    struct OpenCVHelper : OpenCVHelperT<OpenCVHelper, implementation::OpenCVHelper>
    {
    };
}
