#pragma once
#include "ResearchModeApi.h"

#include <stdio.h>
#include <iostream>
#include <sstream>
#include <wchar.h>
#include <thread>
#include <mutex>
#include <atomic>
#include <future>
#include <cmath>
#include <DirectXMath.h>
#include <vector>

#include <unknwn.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>

#include <winrt/Windows.Perception.Spatial.h>
#include <winrt/Windows.Perception.Spatial.Preview.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Input.Spatial.h>

/*
#include <opencv2/core.hpp>
#include <opencv2/calib3d/calib3d.hpp>	// cv::calibrateCamera()
#include <opencv2/highgui/highgui.hpp>
#include <opencv2/imgproc.hpp>  // cv::Canny()
#include <opencv2/aruco.hpp>
#include <opencv2/core/mat.hpp>
*/

#include <opencv2/opencv.hpp>	// for opencv 4.8+