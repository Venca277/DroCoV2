using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SimpleDroneItem : MonoBehaviour {
    [Header("UI Elements")]
    public TMP_Text droneNameText;
    public Image statusDot;
    public Image droneIcon;

    // Tuhle funkci zavola UnitList, kdyz vytvari radek
    public void Setup(string name, Color groupColor, Sprite icon) // muzes pridat i Sprite icon
    {
        droneNameText.text = name;
        statusDot.color = groupColor;
        droneIcon.sprite = icon;
    }
}