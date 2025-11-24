using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class togle_toolpanel : MonoBehaviour
{
    public Animator animator;

    private bool isOpen = false;

    public void TogglePanel()
    {
        isOpen = !isOpen;
        animator.SetBool("IsOpen", isOpen);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
