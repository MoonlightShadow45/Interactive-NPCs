using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SendMessageButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Update()
    {
        if (gameObject.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                button.onClick.Invoke();
            }
        }
    }
}
