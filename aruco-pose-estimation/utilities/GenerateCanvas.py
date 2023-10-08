# simple python script to generate canvas with opencv for aruco markers
# inspired by https://stackoverflow.com/questions/57845196/tune-of-aruco-detection-parameters-on-marker-identification @John's answer

import numpy as np
import cv2
import os
import glob

sourceDir = 'output/'
for image in os.listdir(sourceDir):
    if (image.endswith(".jpg")):
        im = cv2.imread(sourceDir + '/' + image)
        row, col = im.shape[:2]
        bottom = im[row-2:row, 0:col]

        border_size = 200   #px
        canvas = cv2.copyMakeBorder(
            im,
            top=border_size,
            bottom=border_size,
            left=border_size,
            right=border_size,
            borderType=cv2.BORDER_CONSTANT,
            value=[255, 255, 255]
        )

        # canvas size = img size + border size
        height, width, c = canvas.shape

        nTiles = 18
        padding = 3
        cornerLength = 5
        dx = width / nTiles
        dy = height / nTiles

        # top left
        cv2.rectangle(canvas, (0, 0), (int(dx), int(dy * cornerLength)), (0, 0, 0), -1)
        cv2.rectangle(canvas, (0, 0), (int(cornerLength * dx), int(dy)), (0, 0, 0), -1)

        # bottom left
        cv2.rectangle(canvas, (0, int(height - dy * cornerLength)), (int(dx), int(height)), (0, 0, 0), -1)
        cv2.rectangle(canvas, (0, int(height - dy)), (int(dx * cornerLength), int(height)), (0, 0, 0), -1)

        # top right
        cv2.rectangle(canvas, (int(width - dx), 0), (int(width), int(dy * cornerLength)), (0, 0, 0), -1)
        cv2.rectangle(canvas, (int(width - cornerLength * dx), 0), (int(width), int(dy)), (0, 0, 0), -1)

        # bottom right
        cv2.rectangle(canvas, (int(width - dx), int(height - dy * cornerLength)), (int(width), int(height)), (0, 0, 0), -1)
        cv2.rectangle(canvas, (int(width - cornerLength * dx), int(height - dy)), (int(width), int(height)), (0, 0, 0), -1)

        outputdir = 'output/with_canvas'
        if not os.path.isdir(outputdir):
            os.mkdir(outputdir)

        marker_path = os.path.join(outputdir, image)
        cv2.imwrite(marker_path, canvas)

cv2.waitKey(0)