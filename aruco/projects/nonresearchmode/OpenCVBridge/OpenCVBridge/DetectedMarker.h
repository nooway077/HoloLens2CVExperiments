#pragma once

#include "DetectedMarker.g.h"

namespace winrt::OpenCVBridge::implementation
{
    struct DetectedMarker : DetectedMarkerT<DetectedMarker>
    {
		DetectedMarker(_In_ int32_t id,
			_In_ Windows::Foundation::Numerics::float3 position,
			_In_ Windows::Foundation::Numerics::float3 rotation);

		int32_t Id();
		Windows::Foundation::Numerics::float3 Position();
		Windows::Foundation::Numerics::float3 Rotation();

	private:
		int32_t _id;
		Windows::Foundation::Numerics::float3 _position;
		Windows::Foundation::Numerics::float3 _rotation;
    };
}

namespace winrt::OpenCVBridge::factory_implementation
{
    struct DetectedMarker : DetectedMarkerT<DetectedMarker, implementation::DetectedMarker>
    {
    };
}
