using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity_StarRail_CRP_Sample
{
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class DisplaySwitcher : MonoBehaviour
    {
        public int targetDisplayIndex = 1;
        
        private PlayerInput _playerInput;
        private InputAction _displayAction;

        void Start()
        {
            if (targetDisplayIndex < Display.displays.Length)
            {
                Display.displays[targetDisplayIndex].Activate();
            }
            
            _playerInput = GetComponent<PlayerInput>();
            _displayAction = _playerInput.actions["Display"];
        }

        private void Update()
        {
            int index = (int)_displayAction.ReadValue<float>();

            if (index > 0)
            {
                targetDisplayIndex = index;
                Debug.Log($"DisplaySwitcher: Switch to Display {targetDisplayIndex}");
            }
            
            if (targetDisplayIndex < Display.displays.Length)
            {
                Display.displays[targetDisplayIndex].Activate();
            }
        }
    }
}