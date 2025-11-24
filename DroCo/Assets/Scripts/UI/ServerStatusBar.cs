using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class ServerStatusBar : MonoBehaviour {

    [SerializeField]
    private TMP_Text serverIpText;
    [SerializeField]
    private TMP_Text connectionStatusText;
    [SerializeField]
    private Image outline;

    public void OnPointerEnter() {
        outline.color = Color.white;
    }

    public void OnPointerExit() {
        outline.color = Color.grey;
    }

    public void SetServerIP(string ip) {
        serverIpText.text = ip;
    }

    public void SetServerStatus(GameManager.ConnectionStatus status) {
        switch (status) {
            case GameManager.ConnectionStatus.Closed:
                connectionStatusText.text = status.ToString();
                connectionStatusText.color = Color.red;
                break;
            case GameManager.ConnectionStatus.Listening:
                connectionStatusText.text = status.ToString();
                connectionStatusText.color = Color.white;
                break;
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
}
