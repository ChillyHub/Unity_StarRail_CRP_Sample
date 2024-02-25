﻿using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity_StarRail_CRP_Sample
{
    public class SceneCameraController : MonoBehaviour
    {
        public InputActionAsset inputActions;
        
        public float moveSpeed = 10f;
        public float rotateSpeed = 100f;

        private Vector2 moveInput;
        private Vector2 rotateInput;
        private bool shiftInput;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction shiftAction;

        void Start()
        {
            moveAction = inputActions.FindActionMap("Camera").FindAction("Move");
            lookAction = inputActions.FindActionMap("Camera").FindAction("Look");
            shiftAction = inputActions.FindActionMap("Camera").FindAction("Shift");

            moveAction.Enable();
            lookAction.Enable();
            shiftAction.Enable();

            // moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            // lookAction.performed += ctx => rotateInput = ctx.ReadValue<Vector2>();
        }

        void Update()
        {
            moveInput = moveAction.ReadValue<Vector2>();
            rotateInput = lookAction.ReadValue<Vector2>();
            shiftInput = Convert.ToBoolean(shiftAction.ReadValue<float>());
            
            // Move
            Vector3 move = new Vector3(moveInput.x, 0, moveInput.y) * (moveSpeed * Time.deltaTime * (shiftInput ? 2 : 1));
            transform.Translate(move, Space.Self);

            // Rotate
            float yaw = rotateInput.x * (rotateSpeed * Time.deltaTime);
            float pitch = rotateInput.y * (rotateSpeed * Time.deltaTime);
            transform.Rotate(Vector3.up, yaw, Space.World);
            transform.Rotate(Vector3.right, -pitch, Space.Self);
        }

        void OnDestroy()
        {
            moveAction.Disable();
            lookAction.Disable();
            shiftAction.Disable();
        }
    }
}