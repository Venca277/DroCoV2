using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionBar : MonoBehaviour {

    [SerializeField]
    private TMP_InputField serverIpInputField;
    [SerializeField]
    private TMP_Text connectionStatusText;
    [SerializeField]
    private Image outline;

    public void SaveServerIP(string serverIP) {
        GameManager.Instance.SaveServerIP(serverIP);
    }

    public void SetServerIP(string serverIP) {
        serverIpInputField.text = serverIP;
    }

    public void SetConnectionStatus(GameManager.ConnectionStatus status) {
        switch (status) {
            case GameManager.ConnectionStatus.Connected:
                connectionStatusText.text = status.ToString();
                connectionStatusText.color = Color.green;
                break;
            case GameManager.ConnectionStatus.Disconnected:
                connectionStatusText.text = status.ToString();
                connectionStatusText.color = Color.red;
                break;
        }
    }

    public void OnPointerEnter() {
        outline.color = Color.white;
    }

    public void OnPointerExit() {
        outline.color = Color.grey;
    }

}
