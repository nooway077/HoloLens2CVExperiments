# This script can compute camera calibration parameters for the
# photo video camera of the HoloLens 2.

import numpy as np
import cv2
import glob
import yaml

data_folder = 'data/photovideo/'

criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 30, 0.001)
# Arrays to store object points and image points from all the images.
objpoints = [] # 3d point in real world 
imgpoints = [] # 2d points in image plane.
images = glob.glob(data_folder + '*.tiff')

# https://longervision.github.io/2017/03/16/ComputerVision/OpenCV/opencv-internal-calibration-chessboard/
# https://stackoverflow.com/questions/31249037/calibrating-webcam-using-python-and-opencv-error
# https://stackoverflow.com/questions/37310210/camera-calibration-with-opencv-how-to-adjust-chessboard-square-size

# checkerboard Dimensions
cbrow = 6
cbcol = 9

# square length (mm)
squareLength = 25.44

objp = np.zeros((cbrow * cbcol, 3), np.float32)
objp[:, :2] = np.mgrid[0:cbcol, 0:cbrow].T.reshape(-1, 2)
objp = objp * squareLength


for fname in images:
    gray = cv2.imread(fname, cv2.IMREAD_GRAYSCALE)
    ret = False
    ret, corners = cv2.findChessboardCorners(gray, (cbcol, cbrow), None)
    if ret == True:
        objpoints.append(objp)
        cv2.cornerSubPix(gray, corners, (11,11), (-1,-1), criteria)
        imgpoints.append(corners)

        cv2.drawChessboardCorners(gray, (cbcol, cbrow), corners, ret)

ret, mtx, dist, rvecs, tvecs = cv2.calibrateCamera(objpoints, imgpoints, gray.shape[::-1], None, None)

data = {'camera_matrix': np.asarray(mtx).tolist(),
        'dist_coeff': np.asarray(dist).tolist()}

with open("PhotoVideo_intrinsics.yaml", "w") as f:
    yaml.dump(data, f)

