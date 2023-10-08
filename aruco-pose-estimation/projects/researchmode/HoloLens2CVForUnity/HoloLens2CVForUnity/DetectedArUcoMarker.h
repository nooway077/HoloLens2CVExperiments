#pragma once
#include "DetectedArUcoMarker.g.h"

namespace winrt::HoloLens2CVForUnity::implementation
{
	struct DetectedArUcoMarker : DetectedArUcoMarkerT<DetectedArUcoMarker>
	{
		DetectedArUcoMarker(_In_ int32_t id,
			_In_ Windows::Foundation::Numerics::float3 position,
			_In_ Windows::Foundation::Numerics::float3 rotation,
			_In_ Windows::Foundation::Numerics::float4x4 cameraToWorldUnity);

		int32_t Id();
		Windows::Foundation::Numerics::float3 Position();
		Windows::Foundation::Numerics::float3 Rotation();
		Windows::Foundation::Numerics::float4x4 CameraToWorldUnity();

	private:
		int32_t _id;
		Windows::Foundation::Numerics::float3 _position;
		Windows::Foundation::Numerics::float3 _rotation;
		Windows::Foundation::Numerics::float4x4 _cameraToWorldUnity;
	};
}
namespace winrt::HoloLens2CVForUnity::factory_implementation
{
    struct DetectedArUcoMarker : DetectedArUcoMarkerT<DetectedArUcoMarker, implementation::DetectedArUcoMarker>
    {
    };
}
