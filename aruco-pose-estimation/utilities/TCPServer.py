# based on Wenhao's https://github.com/petergu684/HoloLens2-ResearchMode-Unity/blob/master/python/TCPServer.py
# modified to support PV & aruco data

from email.headerregistry import HeaderRegistry
import socket
import struct
import sys
import os
import numpy as np
import cv2
import time
import pickle as pkl

# when running this script, make sure "serverPort" is forwarded
# or disable Windows Firewall temporarly to allow access

def tcp_server():
    serverHost = '192.168.137.1' # localhost
    serverPort = 9090
    data_folder = 'data/'
    save_folder_pv = data_folder + 'photovideo/'
    save_folder_lf = data_folder + 'leftfront/'
    save_folder_rf = data_folder + 'rightfront/'

    if not os.path.isdir(data_folder):
        os.mkdir(data_folder)

    if not os.path.isdir(save_folder_pv):
        os.mkdir(save_folder_pv)
        
    if not os.path.isdir(save_folder_lf):
        os.mkdir(save_folder_lf)

    if not os.path.isdir(save_folder_rf):
        os.mkdir(save_folder_rf)

    # Create a socket
    sSock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

    # Bind server to port
    try:
        sSock.bind((serverHost, serverPort))
        print('Server started on '+str(serverHost))
        print('Server bind to port '+str(serverPort))
    except socket.error as msg:
        print('Bind failed. Error Code : ' + str(msg[0]) + ' Message ' + msg[1])
        return

    sSock.listen(10)
    print('Start listening...')
    sSock.settimeout(3.0)
    while True:
        try:
            conn, addr = sSock.accept() # Blocking, wait for incoming connection
            break
        except KeyboardInterrupt:
            sys.exit(0)
        except Exception:
            continue

    print('Connected with ' + addr[0] + ':' + str(addr[1]))

    while True:
        # Receiving from client
        try:
            data = conn.recv(512*512*8+100) # ~2MB window
            if len(data)==0:
                continue
            header = data[0:1].decode('utf-8')
            print('--------------------------\nHeader: ' + header)
            print(len(data))

            if header == 'p':
                # save PV image
                data_length = struct.unpack(">i", data[1:5])[0]
                ts = struct.unpack(">q", data[5:13])[0]

                N = data_length
                PV_img_np = np.frombuffer(data[13:13+N], np.uint8).reshape((504,896, 4))
                cv2.imwrite(save_folder_pv + str(ts)+'_PV.tiff', PV_img_np)
                #print('Image data length is ' + str(data_length))
                print('Image with ts %d is saved' % (ts))

            if header == 'f':
                # save spatial camera images
                data_length = struct.unpack(">i", data[1:5])[0]
                ts_left, ts_right = struct.unpack(">qq", data[5:21])

                N = int(data_length/2)
                LF_img_np = np.frombuffer(data[21:21+N], np.uint8).reshape((480,640))
                RF_img_np = np.frombuffer(data[21+N:21+2*N], np.uint8).reshape((480,640))
                cv2.imwrite(save_folder_lf + str(ts_left)+'_LF.tiff', LF_img_np)
                cv2.imwrite(save_folder_rf + str(ts_right)+'_RF.tiff', RF_img_np)
                print('Image with ts %d and %d is saved' % (ts_left, ts_right))

            if  header == 'm':
                # print out detected aruco marker data
                # values can be extracted from the string and further
                # processed via this or other script
                # format can be adjusted inside the HoloLens2CVUnity project's
                # ArUcoTracking.cs script at line 260
                output = data[1:].decode('utf-8')
                print(output)

        except Exception as e:
            print(str(e))
            break
    
    print('Closing socket...')
    sSock.close()

if __name__ == "__main__":
    tcp_server()
