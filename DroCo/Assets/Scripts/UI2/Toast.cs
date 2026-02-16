using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Toast : MonoBehaviour {
    public static Toast call;

    [Header("Settings")]
    public CanvasGroup group;
    public TMP_Text toastText;
    public Sprite warningIcon;
    public Sprite warningIconHigh;
    public Image iconImage;

    private Coroutine rutine;

    void Awake() {
        if (call == null)
            call = this;
        else
            Destroy(gameObject);

        group.alpha = 0f;
    }

    public void Show(string warning, float duration = 2.0f, bool isHigh = false) {
        toastText.text = warning;
        iconImage.sprite = isHigh ? warningIconHigh : warningIcon;
        iconImage.rectTransform.sizeDelta = new Vector2(60, 60);

        if (rutine != null)
            StopCoroutine(rutine);
        rutine = StartCoroutine(FadeToast(duration));
    }

    IEnumerator FadeToast(float duration) {
        float fadeSpeed = 5f;
        while (group.alpha < 1) {
            group.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(duration);

        while (group.alpha > 0) {
            group.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }
    }
}
