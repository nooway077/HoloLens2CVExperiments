#include "pch.h"
#include "DetectedMarker.h"
#include "DetectedMarker.g.cpp"

namespace winrt::OpenCVBridge::implementation
{
	DetectedMarker::DetectedMarker(_In_ int32_t id,
		_In_ Windows::Foundation::Numerics::float3 position,
		_In_ Windows::Foundation::Numerics::float3 rotation)
	{
		_id = id;
		_position = position;
		_rotation = rotation;
	}
	int32_t DetectedMarker::Id()
	{
		return _id;
	}
	Windows::Foundation::Numerics::float3 DetectedMarker::Position()
	{
		return _position;
	}
	Windows::Foundation::Numerics::float3 DetectedMarker::Rotation()
	{
		return _rotation;
	}
}
