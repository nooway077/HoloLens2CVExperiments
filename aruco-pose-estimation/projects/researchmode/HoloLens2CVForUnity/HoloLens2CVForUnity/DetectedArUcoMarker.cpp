#include "pch.h"
#include "DetectedArUcoMarker.h"
#include "DetectedArUcoMarker.g.cpp"

namespace winrt::HoloLens2CVForUnity::implementation
{
	DetectedArUcoMarker::DetectedArUcoMarker(_In_ int32_t id,
		_In_ Windows::Foundation::Numerics::float3 position,
		_In_ Windows::Foundation::Numerics::float3 rotation,
		_In_ Windows::Foundation::Numerics::float4x4 cameraToWorldUnity)
	{
		_position = position;
		_rotation = rotation;
		_cameraToWorldUnity = cameraToWorldUnity;
	}
	int32_t DetectedArUcoMarker::Id()
	{
		return _id;
	}
	Windows::Foundation::Numerics::float3 DetectedArUcoMarker::Position()
	{
		return _position;
	}
	Windows::Foundation::Numerics::float3 DetectedArUcoMarker::Rotation()
	{
		return _rotation;
	}
	Windows::Foundation::Numerics::float4x4 DetectedArUcoMarker::CameraToWorldUnity()
	{
		return _cameraToWorldUnity;
	}
}
