# simple python script to generate aruco markers with opencv

import cv2
from cv2 import aruco
import struct
import sys
import os

# output directory
outputdir = 'output/'

if not os.path.isdir(outputdir):
        os.mkdir(outputdir)
 
# number of markers to generate
markerstogenerate = 5

# marker image size in pixels (size * size)
size = 500 

# change cv2.aruco.DICT_6X6_100 if other dictionary required
dictionary = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_6X6_100)

for count in range(markerstogenerate) :

    id = count
    marker = cv2.aruco.generateImageMarker(dictionary, id, size)

    if count < 10 :
        marker_name = 'marker_id_0' + str(count) + '.jpg'
    else :
        marker_name = 'marker_id_' + str(count) + '.jpg'
    marker_path = os.path.join(outputdir, marker_name)

    cv2.imwrite(marker_path, marker)
