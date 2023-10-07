#include "pch.h"
#include "OpenCVHelper.h"
#include "OpenCVHelper.g.cpp"

using namespace winrt::impl;
using namespace Microsoft::WRL;
using namespace winrt::Windows::Graphics::Imaging;
using namespace winrt::Windows::Foundation::Numerics;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::OpenCVBridge::implementation
{
	Windows::Foundation::Collections::IVector<DetectedMarker> OpenCVHelper::ProcessWithArUco(
		Windows::Graphics::Imaging::SoftwareBitmap input, 
		Windows::Foundation::Numerics::float2 focalLength, 
		Windows::Foundation::Numerics::float2 principalPoint, 
		Windows::Foundation::Numerics::float3 radialDistortion, 
		Windows::Foundation::Numerics::float2 tangentialDistortion, 
		int dictionaryId, 
		float markerLength, 
		int& frameProcessingTime)
	{
		// https://stackoverflow.com/questions/22387586/measuring-execution-time-of-a-function-in-c
		using std::chrono::high_resolution_clock;
		using std::chrono::duration_cast;
		using std::chrono::duration;
		using std::chrono::milliseconds;

		auto t1 = high_resolution_clock::now();

		Windows::Foundation::Collections::IVector<DetectedMarker> detectedMarkers = { winrt::single_threaded_vector<DetectedMarker>() };
		frameProcessingTime = 0;

		// https://github.com/opencv/opencv_contrib/blob/4.x/modules/aruco/samples/detect_markers.cpp
		// aruco dictionary from id
		cv::aruco::Dictionary dictionary = cv::aruco::getPredefinedDictionary(cv::aruco::PredefinedDictionaryType(dictionaryId));

		// camera intrinsic parameters for aruco based pose estimation (camera matrix)
		cv::Mat cameraMatrix(3, 3, CV_64F, cv::Scalar(0));

		cameraMatrix.at<double>(0, 0) = focalLength.x;
		cameraMatrix.at<double>(0, 2) = principalPoint.x;
		cameraMatrix.at<double>(1, 1) = focalLength.y;
		cameraMatrix.at<double>(1, 2) = principalPoint.y;
		cameraMatrix.at<double>(2, 2) = 1.0;

		// camera distortion matrix for aruco based pose estimation
		cv::Mat distortionCoefficientsMatrix(1, 5, CV_64F);

		distortionCoefficientsMatrix.at<double>(0, 0) = radialDistortion.x;
		distortionCoefficientsMatrix.at<double>(0, 1) = radialDistortion.y;
		distortionCoefficientsMatrix.at<double>(0, 2) = tangentialDistortion.x;
		distortionCoefficientsMatrix.at<double>(0, 3) = tangentialDistortion.y;
		distortionCoefficientsMatrix.at<double>(0, 4) = radialDistortion.z;

		// https://stackoverflow.com/questions/76802576/how-to-estimate-pose-of-single-marker-in-opencv-python-4-8-0
		// https://github.com/opencv/opencv_contrib/blob/4.x/modules/aruco/samples/detect_markers.cpp
		// set marker corner points
		cv::Mat objPoints(4, 1, CV_32FC3);
		objPoints.ptr<cv::Vec3f>(0)[0] = cv::Vec3f(-markerLength / 2.f, markerLength / 2.f, 0);
		objPoints.ptr<cv::Vec3f>(0)[1] = cv::Vec3f(markerLength / 2.f, markerLength / 2.f, 0);
		objPoints.ptr<cv::Vec3f>(0)[2] = cv::Vec3f(markerLength / 2.f, -markerLength / 2.f, 0);
		objPoints.ptr<cv::Vec3f>(0)[3] = cv::Vec3f(-markerLength / 2.f, -markerLength / 2.f, 0);

		std::vector<int> ids;
		std::vector<std::vector<cv::Point2f>> corners, rejected;

		// load softwarebitmap to cv::Mat()
		cv::Mat processed, gray;
		TryConvert(input, processed);	// returns false if failed, but that is not used here

		// convert to grayscale
		cv::cvtColor(processed, gray, cv::COLOR_BGR2GRAY);

		// initialize aruco detector
		cv::aruco::DetectorParameters detectorParams = cv::aruco::DetectorParameters();
		cv::aruco::ArucoDetector detector(dictionary, detectorParams);

		// detect markers
		detector.detectMarkers(gray, corners, ids, rejected);

		size_t  nMarkers = corners.size();
		std::vector<cv::Vec3d> rvecs(nMarkers), tvecs(nMarkers);

		if (ids.size() > 0)
		{
			// calculate pose for each marker
			for (size_t i = 0; i < nMarkers; i++) {
				cv::solvePnP(objPoints, corners.at(i), cameraMatrix, distortionCoefficientsMatrix, rvecs.at(i), tvecs.at(i));
			}

			// append detected markers to the ivector
			for (size_t i = 0; i < ids.size(); i++)
			{
				DetectedMarker marker = DetectedMarker(
					ids[i],
					Windows::Foundation::Numerics::float3((float)tvecs[i][0], (float)tvecs[i][1], (float)tvecs[i][2]),
					Windows::Foundation::Numerics::float3((float)rvecs[i][0], (float)rvecs[i][1], (float)rvecs[i][2]));
				detectedMarkers.Append(marker);
			}
		}

		auto t2 = high_resolution_clock::now();
		auto ms_int = duration_cast<milliseconds>(t2 - t1);

		frameProcessingTime = ms_int.count();
		return detectedMarkers;

	}
	
	bool OpenCVHelper::TryConvert(
		Windows::Graphics::Imaging::SoftwareBitmap from, 
		cv::Mat& convertedMat)
	{
		unsigned char* pPixels = nullptr;
		unsigned int capacity = 0;
		if (!GetPointerToPixelData(from, &pPixels, &capacity))
		{
			return false;
		}
		// assume input SoftwareBitmap is BGRA8 
		cv::Mat mat(from.PixelHeight(), from.PixelWidth(), CV_8UC4, (void*)pPixels);

		// shallow copy because we want convertedMat.data = pPixels
		// don't use .copyTo or .clone
		convertedMat = mat;
		return true;
	}
		
	bool OpenCVHelper::GetPointerToPixelData(
		Windows::Graphics::Imaging::SoftwareBitmap bitmap,
		unsigned char** pPixelData, unsigned int* capacity)
	{
		BitmapBuffer bmpbuffer = bitmap.LockBuffer(BitmapBufferAccessMode::ReadWrite);
		IMemoryBufferReference reference = bmpbuffer.CreateReference();

		auto byteAccess = bmpbuffer.CreateReference().as<IMemoryBufferByteAccess>();

		if (byteAccess->GetBuffer(pPixelData, capacity) != S_OK)
		{
			return false;
		}
		return true;
	}

}
