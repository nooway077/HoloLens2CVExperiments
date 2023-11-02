using System;
using UnityEngine;

#if WINDOWS_UWP
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
#endif

public class TCPClient : MonoBehaviour
{
    private void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
#if WINDOWS_UWP
            StopConnection();
#endif
        }
    }

    [SerializeField]
    string serverIP, port;

    private bool connected = false;
    public bool Connected
    {
        get { return connected; }
    }

#if WINDOWS_UWP
    StreamSocket socket = null;
    public DataWriter dw;
    public DataReader dr;

    private async void StartConnection() 
    {
        if (socket != null)
        {
            socket.Dispose();
            Debug.Log("Socket down");
        }
        Debug.Log("Connecting to " + serverIP + ":" + port);
        try
        {
            socket = new StreamSocket();
            var hostName = new Windows.Networking.HostName(serverIP);
            await socket.ConnectAsync(hostName, port);
            dw = new DataWriter(socket.OutputStream);
            // dw.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            dr = new DataReader(socket.InputStream);
            dr.InputStreamOptions = InputStreamOptions.Partial;
            connected = true;
            Debug.Log("Socket connected");
        }
        catch (Exception ex)
        {
            SocketErrorStatus webErrorStatus = SocketError.GetStatus(ex.GetBaseException().HResult);
            Debug.Log(webErrorStatus.ToString() != "Unknown" ? webErrorStatus.ToString() : ex.Message);
        }
    }

    private void StopConnection()
    {
        dw?.DetachStream();
        dw?.Dispose();
        dw = null;

        dr?.DetachStream();
        dr?.Dispose();
        dr = null;

        socket?.Dispose();
        connected = false;
        Debug.Log("Socket disconnected");
    }

    bool lastMessageSent = true;
    public async void SendMarkerDataAsync(string markerData)
    {
        if (!lastMessageSent) return;
        lastMessageSent = false;
        try
        {
            // Write header
            dw.WriteString("m");    // header "m" for marker data

            // Write marker data
            dw.WriteString(markerData);

            // Send out
            await dw.StoreAsync();
            await dw.FlushAsync();
        }
        catch (Exception ex)
        {
            SocketErrorStatus webErrorStatus = SocketError.GetStatus(ex.GetBaseException().HResult);
            Debug.Log(webErrorStatus.ToString() != "Unknown" ? webErrorStatus.ToString() : ex.Message);
        }
        lastMessageSent = true;
    }

    public async void SendPVImageAsync(byte[] PVImage, long ts)
    {
        if (!lastMessageSent) return;
        lastMessageSent = false;
        try
        {
            // Write header
            dw.WriteString("p"); // header "p" for pv image

            // Write TimeStamp and Length
            dw.WriteInt32(PVImage.Length);
            dw.WriteInt64(ts);

            // Write actual data
            dw.WriteBytes(PVImage);

            // Send out
            await dw.StoreAsync();
            await dw.FlushAsync();
        }
        catch (Exception ex)
        {
            SocketErrorStatus webErrorStatus = SocketError.GetStatus(ex.GetBaseException().HResult);
            Debug.Log(webErrorStatus.ToString() != "Unknown" ? webErrorStatus.ToString() : ex.Message);
        }
        lastMessageSent = true;
    }

#endif

    public void ConnectToServerEvent()
    {
#if WINDOWS_UWP
        if (!connected) StartConnection();
        else StopConnection();
#endif
    }
}
