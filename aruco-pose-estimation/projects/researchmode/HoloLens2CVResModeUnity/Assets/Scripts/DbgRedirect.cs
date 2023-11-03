using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DbgRedirect : MonoBehaviour
{
    public bool EnableDBGDisplay;                         // in the inspector, set this to TRUE to view debug output in runtime
    public TextMeshPro output;

    private Queue<string> msgs = new Queue<string>();
    private static int _maxMsgCount = 6;

    // Start is called before the first frame update
    void Start()
    {
        if (EnableDBGDisplay)
        {
            Application.logMessageReceived += LogCallbackHandler;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    void LogCallbackHandler(string logString, string stackTrace, LogType type)
    {
        if (output == null) return;
        string message = string.Format("[{0}] {1}", type, logString);

        while (msgs.Count > _maxMsgCount - 1)
        {
            msgs.Dequeue();
        }
        msgs.Enqueue(message);

        string temp_str = "";
        foreach (string m in msgs)
        {
            temp_str += "> " + m + "\n";
        }
        output.text = temp_str;
    }
}
