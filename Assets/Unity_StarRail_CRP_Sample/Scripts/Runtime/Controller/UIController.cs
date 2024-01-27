using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Unity_StarRail_CRP_Sample
{
#if ENABLE_INPUT_SYSTEM 
    public class UIController : MonoBehaviour
    {
        public Canvas menu;

        private InputActionAsset _inputActionAsset;
        private InputActionMap _player;
        private InputAction _esc;

        private bool _isPause = false;
        private bool _isPressing = false;
        
        private void Start()
        {
            _inputActionAsset = GetComponent<InputSystemUIInputModule>().actionsAsset;
            _player = _inputActionAsset.FindActionMap("Player");
            _esc = _inputActionAsset.FindActionMap("UI").FindAction("Menu");
            _esc.Enable();
        }

        private void Update()
        {
            bool esc = Convert.ToBoolean(_esc.ReadValue<float>());

            if (esc && !_isPressing)
            {
                _isPause = !_isPause;
                _isPressing = true;
            }
            else if (!esc)
            {
                _isPressing = false;
            }

            //Time.timeScale = _isPause ? 0.0f : 1.0f;
            AudioListener.pause = _isPause;
            menu.gameObject.SetActive(_isPause);

            if (_isPause)
            {
                _player.Disable();
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                _player.Enable();
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void OnValueChangedScene(Int32 value)
        {
            
        }

        public void OnValueChangedDisplay(Int32 value)
        {
            int targetDisplayIndex = value;
            Debug.Log(Display.displays.Length);
            if (targetDisplayIndex < Display.displays.Length)
            {
                Display.displays[targetDisplayIndex].Activate();
            }
        }

        public void OnClickExit()
        {
            Debug.Log("Exit");
            Application.Quit();
        }
    }
#endif
}