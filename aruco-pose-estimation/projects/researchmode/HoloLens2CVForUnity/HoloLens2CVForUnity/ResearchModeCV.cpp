#include "pch.h"
#include "ResearchModeCV.h"
#include "ResearchModeCV.g.cpp"

extern "C"
HMODULE LoadLibraryA(
	LPCSTR lpLibFileName
);

static ResearchModeSensorConsent camAccessCheck;
static HANDLE camConsentGiven;

using namespace DirectX;
using namespace winrt::Windows::Perception;
using namespace winrt::Windows::Perception::Spatial;
using namespace winrt::Windows::Perception::Spatial::Preview;

typedef std::chrono::duration<int64_t, std::ratio<1, 10'000'000>> HundredsOfNanoseconds;
#undef max  // https://stackoverflow.com/questions/27442885/syntax-error-with-stdnumeric-limitsmax
static constexpr UINT64 kMaxLongLong = static_cast<UINT64>(std::numeric_limits<long long>::max());

namespace winrt::HoloLens2CVForUnity::implementation
{

	ResearchModeCV::ResearchModeCV()
	{
		// Load Research Mode library
		camConsentGiven = CreateEvent(nullptr, true, false, nullptr);
		HMODULE hrResearchMode = LoadLibraryA("ResearchModeAPI");
		HRESULT hr = S_OK;

		if (hrResearchMode)
		{
			typedef HRESULT(__cdecl* PFN_CREATEPROVIDER) (IResearchModeSensorDevice** ppSensorDevice);
			PFN_CREATEPROVIDER pfnCreate = reinterpret_cast<PFN_CREATEPROVIDER>(GetProcAddress(hrResearchMode, "CreateResearchModeSensorDevice"));
			if (pfnCreate)
			{
				winrt::check_hresult(pfnCreate(&m_pSensorDevice));
			}
			else
			{
				winrt::check_hresult(E_INVALIDARG);
			}
		}

		// Get spatial locator of rigNode
		GUID guid;
		IResearchModeSensorDevicePerception* pSensorDevicePerception;
		winrt::check_hresult(m_pSensorDevice->QueryInterface(IID_PPV_ARGS(&pSensorDevicePerception)));
		winrt::check_hresult(pSensorDevicePerception->GetRigNodeId(&guid));
		pSensorDevicePerception->Release();
		m_locator = SpatialGraphInteropPreview::CreateLocatorForNode(guid);

		size_t sensorCount = 0;

		winrt::check_hresult(m_pSensorDevice->QueryInterface(IID_PPV_ARGS(&m_pSensorDeviceConsent)));
		winrt::check_hresult(m_pSensorDeviceConsent->RequestCamAccessAsync(ResearchModeCV::CamAccessOnComplete));

		m_pSensorDevice->DisableEyeSelection();

		winrt::check_hresult(m_pSensorDevice->GetSensorCount(&sensorCount));
		m_sensorDescriptors.resize(sensorCount);
		winrt::check_hresult(m_pSensorDevice->GetSensorDescriptors(m_sensorDescriptors.data(), m_sensorDescriptors.size(), &sensorCount));
	}

	HRESULT ResearchModeCV::CheckCamConsent()
	{
		HRESULT hr = S_OK;
		DWORD waitResult = WaitForSingleObject(camConsentGiven, INFINITE);
		if (waitResult == WAIT_OBJECT_0)
		{
			switch (camAccessCheck)
			{
			case ResearchModeSensorConsent::Allowed:
				OutputDebugString(L"Access is granted");
				break;
			case ResearchModeSensorConsent::DeniedBySystem:
				OutputDebugString(L"Access is denied by the system");
				hr = E_ACCESSDENIED;
				break;
			case ResearchModeSensorConsent::DeniedByUser:
				OutputDebugString(L"Access is denied by the user");
				hr = E_ACCESSDENIED;
				break;
			case ResearchModeSensorConsent::NotDeclaredByApp:
				OutputDebugString(L"Capability is not declared in the app manifest");
				hr = E_ACCESSDENIED;
				break;
			case ResearchModeSensorConsent::UserPromptRequired:
				OutputDebugString(L"Capability user prompt required");
				hr = E_ACCESSDENIED;
				break;
			default:
				OutputDebugString(L"Access is denied by the system");
				hr = E_ACCESSDENIED;
				break;
			}
		}
		else
		{
			hr = E_UNEXPECTED;
		}
		return hr;
	}

	void ResearchModeCV::CamAccessOnComplete(ResearchModeSensorConsent consent)
	{
		camAccessCheck = consent;
		SetEvent(camConsentGiven);
	}

	void ResearchModeCV::InitializeSpatialCamerasFront()
	{
		DirectX::XMMATRIX cameraNodeToRigPoseInverted;
		DirectX::XMMATRIX cameraNodeToRigPose;
		DirectX::XMVECTOR det;

		for (auto sensorDescriptor : m_sensorDescriptors)
		{
			if (sensorDescriptor.sensorType == LEFT_FRONT)
			{
				winrt::check_hresult(m_pSensorDevice->GetSensor(sensorDescriptor.sensorType, &m_LFSensor));
				winrt::check_hresult(m_LFSensor->QueryInterface(IID_PPV_ARGS(&m_LFCameraSensor)));
				winrt::check_hresult(m_LFCameraSensor->GetCameraExtrinsicsMatrix(&m_LFCameraPose));
				cameraNodeToRigPose = XMLoadFloat4x4(&m_LFCameraPose);
				det = XMMatrixDeterminant(cameraNodeToRigPose);
				m_LFCameraPoseInvMatrix = XMMatrixInverse(&det, cameraNodeToRigPose);
			}
			if (sensorDescriptor.sensorType == RIGHT_FRONT)
			{
				winrt::check_hresult(m_pSensorDevice->GetSensor(sensorDescriptor.sensorType, &m_RFSensor));
				winrt::check_hresult(m_RFSensor->QueryInterface(IID_PPV_ARGS(&m_RFCameraSensor)));
				winrt::check_hresult(m_RFCameraSensor->GetCameraExtrinsicsMatrix(&m_RFCameraPose));
				cameraNodeToRigPose = XMLoadFloat4x4(&m_RFCameraPose);
				det = XMMatrixDeterminant(cameraNodeToRigPose);
				m_RFCameraPoseInvMatrix = XMMatrixInverse(&det, cameraNodeToRigPose);
			}
		}
	}

	void ResearchModeCV::StartSpatialCamerasFrontLoop()
	{
		if (m_refFrame == nullptr)
		{
			m_refFrame = m_locator.GetDefault().CreateStationaryFrameOfReferenceAtCurrentLocation().CoordinateSystem();
		}

		if (SUCCEEDED(CheckCamConsent())) m_pSpatialCamerasFrontUpdateThread = new std::thread(ResearchModeCV::SpatialCamerasFrontLoop, this);
	}

	void ResearchModeCV::SpatialCamerasFrontLoop(ResearchModeCV* pResearchModeCV)
	{
		// prevent starting loop for multiple times
		if (!pResearchModeCV->m_spatialCamerasFrontLoopStarted)
		{
			pResearchModeCV->m_spatialCamerasFrontLoopStarted = true;
		}
		else {
			return;
		}

		pResearchModeCV->m_detectedMarkers = { winrt::single_threaded_vector<DetectedArUcoMarker>() };

		pResearchModeCV->m_LFSensor->OpenStream();
		pResearchModeCV->m_RFSensor->OpenStream();

		try
		{
			while (pResearchModeCV->m_spatialCamerasFrontLoopStarted)
			{
				IResearchModeSensorFrame* pLFCameraFrame = nullptr;
				IResearchModeSensorFrame* pRFCameraFrame = nullptr;
				ResearchModeSensorResolution LFResolution;
				ResearchModeSensorResolution RFResolution;
				pResearchModeCV->m_LFSensor->GetNextBuffer(&pLFCameraFrame);
				pResearchModeCV->m_RFSensor->GetNextBuffer(&pRFCameraFrame);

				// process sensor frame
				pLFCameraFrame->GetResolution(&LFResolution);
				pResearchModeCV->m_LFResolution = LFResolution;
				pRFCameraFrame->GetResolution(&RFResolution);
				pResearchModeCV->m_RFResolution = RFResolution;

				IResearchModeSensorVLCFrame* pLFFrame = nullptr;
				winrt::check_hresult(pLFCameraFrame->QueryInterface(IID_PPV_ARGS(&pLFFrame)));
				IResearchModeSensorVLCFrame* pRFFrame = nullptr;
				winrt::check_hresult(pRFCameraFrame->QueryInterface(IID_PPV_ARGS(&pRFFrame)));

				size_t LFOutBufferCount = 0;
				const BYTE* pLFImage = nullptr;
				pLFFrame->GetBuffer(&pLFImage, &LFOutBufferCount);
				pResearchModeCV->m_LFbufferSize = LFOutBufferCount;
				size_t RFOutBufferCount = 0;
				const BYTE* pRFImage = nullptr;
				pRFFrame->GetBuffer(&pRFImage, &RFOutBufferCount);
				pResearchModeCV->m_RFbufferSize = RFOutBufferCount;

				// get tracking transform
				ResearchModeSensorTimestamp timestamp_left, timestamp_right;
				pLFCameraFrame->GetTimeStamp(&timestamp_left);
				pRFCameraFrame->GetTimeStamp(&timestamp_right);

				auto ts_left = PerceptionTimestampHelper::FromSystemRelativeTargetTime(HundredsOfNanoseconds(checkAndConvertUnsigned(timestamp_left.HostTicks)));
				auto ts_right = PerceptionTimestampHelper::FromSystemRelativeTargetTime(HundredsOfNanoseconds(checkAndConvertUnsigned(timestamp_right.HostTicks)));

				// locate camera (location of camera rig to world origin)
				auto rigToWorld_l = pResearchModeCV->m_locator.TryLocateAtTimestamp(ts_left, pResearchModeCV->m_refFrame);
				auto rigToWorld_r = rigToWorld_l;
				if (ts_left.TargetTime() != ts_right.TargetTime()) {
					rigToWorld_r = pResearchModeCV->m_locator.TryLocateAtTimestamp(ts_right, pResearchModeCV->m_refFrame);
				}

				if (rigToWorld_l == nullptr || rigToWorld_r == nullptr)
				{
					continue;
				}

				// get camera to world transforms (camera node to rig inv * camera rig to world)
				auto LfToWorld = pResearchModeCV->m_LFCameraPoseInvMatrix * SpatialLocationToDxMatrix(rigToWorld_l);
				auto RfToWorld = pResearchModeCV->m_RFCameraPoseInvMatrix * SpatialLocationToDxMatrix(rigToWorld_r);

				if (pResearchModeCV->m_enableArUcoDetector)
				{
					// clear previously detected markers
					pResearchModeCV->m_detectedMarkers.Clear();

					// detect & estimate pose of markers on left front camera image
					if (pResearchModeCV->m_sensor == 0)
					{
						pResearchModeCV->ProcessSensorImageWithArUco(pLFImage, LFResolution, LfToWorld,
							pResearchModeCV->m_LFCameraIntrinsics,
							pResearchModeCV->m_dictId,
							pResearchModeCV->m_markerLength,
							pResearchModeCV->m_frameProcessingTime,
							pResearchModeCV->m_detectedMarkers);
					}
					// detect & estimate pose of markers on right front camera image
					if (pResearchModeCV->m_sensor == 1)
					{
						pResearchModeCV->ProcessSensorImageWithArUco(pRFImage, RFResolution, RfToWorld,
							pResearchModeCV->m_RFCameraIntrinsics,
							pResearchModeCV->m_dictId,
							pResearchModeCV->m_markerLength,
							pResearchModeCV->m_frameProcessingTime,
							pResearchModeCV->m_detectedMarkers);
					}

					// markers ready to be queried
					pResearchModeCV->m_ArUcoDetectionsUpdated = true;
				}

				{
					std::lock_guard<std::mutex> l(pResearchModeCV->mu);

					if (pResearchModeCV->m_enableBuffer)
					{
						// get time stamp for frames
						pResearchModeCV->m_lastSpatialFrame.LFFrame.timestamp = timestamp_left.HostTicks;
						pResearchModeCV->m_lastSpatialFrame.RFFrame.timestamp = timestamp_right.HostTicks;

						pResearchModeCV->m_lastSpatialFrame.LFFrame.timestamp_ft = ts_left.TargetTime().time_since_epoch().count();
						pResearchModeCV->m_lastSpatialFrame.RFFrame.timestamp_ft = ts_right.TargetTime().time_since_epoch().count();

						// save LF and RF images
						if (!pResearchModeCV->m_lastSpatialFrame.LFFrame.image)
						{
							OutputDebugString(L"Create Space for Left Front Image...\n");
							pResearchModeCV->m_lastSpatialFrame.LFFrame.image = new UINT8[LFOutBufferCount];
						}
						memcpy(pResearchModeCV->m_lastSpatialFrame.LFFrame.image, pLFImage, LFOutBufferCount * sizeof(UINT8));

						if (!pResearchModeCV->m_lastSpatialFrame.RFFrame.image)
						{
							OutputDebugString(L"Create Space for Right Front Image...\n");
							pResearchModeCV->m_lastSpatialFrame.RFFrame.image = new UINT8[RFOutBufferCount];
						}
						memcpy(pResearchModeCV->m_lastSpatialFrame.RFFrame.image, pRFImage, RFOutBufferCount * sizeof(UINT8));

						// images ready to be queried
						pResearchModeCV->m_LFImageUpdated = true;
						pResearchModeCV->m_RFImageUpdated = true;
					}
				}

				// release space
				if (pLFFrame) pLFFrame->Release();
				if (pRFFrame) pRFFrame->Release();

				if (pLFCameraFrame) pLFCameraFrame->Release();
				if (pRFCameraFrame) pRFCameraFrame->Release();
			}
		}
		catch (...) {}
		pResearchModeCV->m_LFSensor->CloseStream();
		pResearchModeCV->m_LFSensor->Release();
		pResearchModeCV->m_LFSensor = nullptr;

		pResearchModeCV->m_RFSensor->CloseStream();
		pResearchModeCV->m_RFSensor->Release();
		pResearchModeCV->m_RFSensor = nullptr;
	}

	// Stop the sensor loop and release buffer space.
	// Sensor object should be released at the end of the loop function
	void ResearchModeCV::StopAllSensorDevice()
	{
		if (m_lastSpatialFrame.LFFrame.image)
		{
			delete[] m_lastSpatialFrame.LFFrame.image;
			m_lastSpatialFrame.LFFrame.image = nullptr;
		}
		if (m_lastSpatialFrame.RFFrame.image)
		{
			delete[] m_lastSpatialFrame.RFFrame.image;
			m_lastSpatialFrame.RFFrame.image = nullptr;
		}

		m_pSensorDevice->Release();
		m_pSensorDevice = nullptr;
		m_pSensorDeviceConsent->Release();
		m_pSensorDeviceConsent = nullptr;
	}

	// Set the reference coordinate system. Need to be set before the sensor loop starts; otherwise, default coordinate will be used.
	void ResearchModeCV::SetReferenceCoordinateSystem(winrt::Windows::Perception::Spatial::SpatialCoordinateSystem refCoord)
	{
		m_refFrame = refCoord;
	}

	void ResearchModeCV::SetCameraIntrinsics(int _cameraType,
		Windows::Foundation::Numerics::float2 _focalLength,
		Windows::Foundation::Numerics::float2 _principalPoint,
		Windows::Foundation::Numerics::float3 _radialDistortion,
		Windows::Foundation::Numerics::float2 _tangentialDistortion)
	{
		if (_cameraType == 0)
		{
			// set LEFT Front camera's intrinsics
			m_LFCameraIntrinsics.focalLength = _focalLength;
			m_LFCameraIntrinsics.principalPoint = _principalPoint;
			m_LFCameraIntrinsics.radialDistortion = _radialDistortion;
			m_LFCameraIntrinsics.tangentialDistortion = _tangentialDistortion;
		}
		if (_cameraType == 1)
		{
			// set Right Front camera's intrinsics
			m_RFCameraIntrinsics.focalLength = _focalLength;
			m_RFCameraIntrinsics.principalPoint = _principalPoint;
			m_RFCameraIntrinsics.radialDistortion = _radialDistortion;
			m_RFCameraIntrinsics.tangentialDistortion = _tangentialDistortion;
		}
	}

	void ResearchModeCV::Configure(int _sensor, bool _enableBuffer, bool _enableArUcoDetector, float _markerLength, int _dictId)
	{
		m_sensor = _sensor;
		m_enableBuffer = _enableBuffer;
		m_enableArUcoDetector = _enableArUcoDetector;
		m_markerLength = _markerLength;
		m_dictId = _dictId;
		/*
		std::stringstream ss;
		ss << "Configured ArUco Detector with: \n" <<
			"markerLength: " << _markerLength << "\n" <<
			"dictionaryId: " << _dictId;
		std::string s = ss.str();
		OutputDebugStringA(s.c_str());
		*/
	}

	inline bool ResearchModeCV::LFImageUpdated() { return m_LFImageUpdated; }
	inline bool ResearchModeCV::RFImageUpdated() { return m_RFImageUpdated; }
	inline bool ResearchModeCV::ArUcoDetectionsUpdated() { return m_ArUcoDetectionsUpdated; }

	int32_t ResearchModeCV::GetDetectedMarkersCount()
	{
		m_ArUcoDetectionsUpdated = false;
		return m_detectedMarkers.Size();
	}

	int32_t ResearchModeCV::GetFrameProcessingTime()
	{
		return m_frameProcessingTime;
	}

	Windows::Foundation::Collections::IVector<DetectedArUcoMarker> ResearchModeCV::GetDetectedMarkers()
	{
		m_ArUcoDetectionsUpdated = false;
		return m_detectedMarkers;
	}

	com_array<uint8_t> ResearchModeCV::GetLFCameraBuffer(int64_t& ts)
	{
		std::lock_guard<std::mutex> l(mu);
		if (!m_lastSpatialFrame.LFFrame.image)
		{
			return com_array<UINT8>();
		}
		com_array<UINT8> tempBuffer = com_array<UINT8>(std::move_iterator(m_lastSpatialFrame.LFFrame.image), std::move_iterator(m_lastSpatialFrame.LFFrame.image + m_LFbufferSize));
		ts = m_lastSpatialFrame.LFFrame.timestamp_ft;
		m_LFImageUpdated = false;
		return tempBuffer;
	}

	com_array<uint8_t> ResearchModeCV::GetRFCameraBuffer(int64_t& ts)
	{
		std::lock_guard<std::mutex> l(mu);
		if (!m_lastSpatialFrame.RFFrame.image)
		{
			return com_array<UINT8>();
		}
		com_array<UINT8> tempBuffer = com_array<UINT8>(std::move_iterator(m_lastSpatialFrame.RFFrame.image), std::move_iterator(m_lastSpatialFrame.RFFrame.image + m_RFbufferSize));
		ts = m_lastSpatialFrame.RFFrame.timestamp_ft;
		m_RFImageUpdated = false;
		return tempBuffer;
	}

	long long ResearchModeCV::checkAndConvertUnsigned(UINT64 val)
	{
		assert(val <= kMaxLongLong);
		return static_cast<long long>(val);
	}

	XMMATRIX ResearchModeCV::SpatialLocationToDxMatrix(SpatialLocation location) {
		auto rot = location.Orientation();
		auto quatInDx = XMFLOAT4(rot.x, rot.y, rot.z, rot.w);
		auto rotMat = XMMatrixRotationQuaternion(XMLoadFloat4(&quatInDx));
		auto pos = location.Position();
		auto posMat = XMMatrixTranslation(pos.x, pos.y, pos.z);
		return rotMat * posMat;
	}

	void ResearchModeCV::ProcessSensorImageWithArUco(const BYTE* pImage,
		ResearchModeSensorResolution resolution,
		DirectX::XMMATRIX cameraToWorld,
		CameraIntrinsics cameraIntrinsics,
		int dictId,
		float markerLenght,
		int& frameProcessingTime,
		Windows::Foundation::Collections::IVector<DetectedArUcoMarker>& detectedMarkers)
	{
		// https://stackoverflow.com/questions/22387586/measuring-execution-time-of-a-function-in-c
		using std::chrono::high_resolution_clock;
		using std::chrono::duration_cast;
		using std::chrono::duration;
		using std::chrono::milliseconds;

		auto t1 = high_resolution_clock::now();

		// https://github.com/opencv/opencv_contrib/blob/4.x/modules/aruco/samples/detect_markers.cpp
		// arucu dictionary from id
		cv::aruco::Dictionary dictionary = 
			cv::aruco::getPredefinedDictionary(cv::aruco::PredefinedDictionaryType(dictId));

		// size of the printed marker's side in meters
		float mlenght = markerLenght;

		// camera intrinsic parameters for aruco based pose estimation (camera matrix)
		cv::Mat cameraMatrix(3, 3, CV_64F, cv::Scalar(0));

		cameraMatrix.at<double>(0, 0) = cameraIntrinsics.focalLength.x;
		cameraMatrix.at<double>(0, 2) = cameraIntrinsics.principalPoint.x;
		cameraMatrix.at<double>(1, 1) = cameraIntrinsics.focalLength.y;
		cameraMatrix.at<double>(1, 2) = cameraIntrinsics.principalPoint.y;
		cameraMatrix.at<double>(2, 2) = 1.0;

		// camera distortion matrix for aruco based pose estimation
		cv::Mat distortionCoefficientsMatrix(1, 5, CV_64F);

		distortionCoefficientsMatrix.at<double>(0, 0) = cameraIntrinsics.radialDistortion.x;
		distortionCoefficientsMatrix.at<double>(0, 1) = cameraIntrinsics.radialDistortion.y;
		distortionCoefficientsMatrix.at<double>(0, 2) = cameraIntrinsics.tangentialDistortion.x;
		distortionCoefficientsMatrix.at<double>(0, 3) = cameraIntrinsics.tangentialDistortion.y;
		distortionCoefficientsMatrix.at<double>(0, 4) = cameraIntrinsics.radialDistortion.z;

		// camera to world transposed
		DirectX::XMMATRIX cameraToWorldT;

		// camera to world unity transform float4x4
		Windows::Foundation::Numerics::float4x4 viewToUnity;

		// https://stackoverflow.com/questions/76802576/how-to-estimate-pose-of-single-marker-in-opencv-python-4-8-0
		// https://github.com/opencv/opencv_contrib/blob/4.x/modules/aruco/samples/detect_markers.cpp
		// set marker corner points
		cv::Mat objPoints(4, 1, CV_32FC3);
		objPoints.ptr<cv::Vec3f>(0)[0] = cv::Vec3f(-mlenght / 2.f, mlenght / 2.f, 0);
		objPoints.ptr<cv::Vec3f>(0)[1] = cv::Vec3f(mlenght / 2.f, mlenght / 2.f, 0);
		objPoints.ptr<cv::Vec3f>(0)[2] = cv::Vec3f(mlenght / 2.f, -mlenght / 2.f, 0);
		objPoints.ptr<cv::Vec3f>(0)[3] = cv::Vec3f(-mlenght / 2.f, -mlenght / 2.f, 0);

		std::vector<int> ids;
		std::vector<std::vector<cv::Point2f>> corners, rejected;

		// load sensor image
		cv::Mat processed(resolution.Height, resolution.Width, CV_8U, (void*)pImage);

		// initialize aruco detector
		cv::aruco::DetectorParameters detectorParams = cv::aruco::DetectorParameters();
		cv::aruco::ArucoDetector detector(dictionary, detectorParams);

		// detect markers
		detector.detectMarkers(processed, corners, ids, rejected);

		size_t  nMarkers = corners.size();
		std::vector<cv::Vec3d> rvecs(nMarkers), tvecs(nMarkers);

		if (ids.size() > 0)
		{
			// https://gamedev.stackexchange.com/questions/153816/why-do-these-directxmath-functions-seem-like-they-return-column-major-matrics
			// transposing camera to world -> row major to column major matrix
			cameraToWorldT = DirectX::XMMatrixTranspose(cameraToWorld);

			// store as float4x4 for Unity
			DirectX::XMStoreFloat4x4(&viewToUnity, cameraToWorldT);

			// invert Z axis to match Unity coordinate system
			viewToUnity.m31 *= -1.0f;
			viewToUnity.m32 *= -1.0f;
			viewToUnity.m33 *= -1.0f;
			viewToUnity.m34 *= -1.0f;

			// calculate pose for each marker
			for (size_t i = 0; i < nMarkers; i++) {
				cv::solvePnP(objPoints, corners.at(i), cameraMatrix, distortionCoefficientsMatrix, rvecs.at(i), tvecs.at(i));
			}

			// append detected markers to the ivector
			for (size_t i = 0; i < ids.size(); i++)
			{
				// X Y Z position
				// X Y Z orientation (Rodrigues)
				// camera to world unity
				DetectedArUcoMarker marker = DetectedArUcoMarker(
					ids[i],
					Windows::Foundation::Numerics::float3((float)tvecs[i][0], (float)tvecs[i][1], (float)tvecs[i][2]),
					Windows::Foundation::Numerics::float3((float)rvecs[i][0], (float)rvecs[i][1], (float)rvecs[i][2]),
					viewToUnity);
				detectedMarkers.Append(marker);
			}
		}

		auto t2 = high_resolution_clock::now();
		auto ms_int = duration_cast<milliseconds>(t2 - t1);

		frameProcessingTime = ms_int.count();
	}
}
