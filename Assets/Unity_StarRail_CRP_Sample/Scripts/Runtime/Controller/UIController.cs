using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

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

        private int _currentSceneId;
        private int _loadSceneId;
        
        private void Start()
        {
            _inputActionAsset = GetComponent<InputSystemUIInputModule>().actionsAsset;
            _player = _inputActionAsset.FindActionMap("Player");
            _esc = _inputActionAsset.FindActionMap("UI").FindAction("Menu");
            _esc.Enable();

            // Init load
            SceneManager.LoadSceneAsync(1, LoadSceneMode.Additive);
            _currentSceneId = 1;
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
            LoadScene(value + 1);
        }

        public void OnValueChangedDisplay(Int32 value)
        {
            Scene scene = SceneManager.GetSceneByBuildIndex(_currentSceneId);
            if (scene.IsValid())
            {
                var cameras = FindCameraInScene(scene);

                for (int i = 0; i < cameras.Count; i++)
                {
                    cameras[i].SetActive(i == value);
                }
            }
        }

        public void OnClickExit()
        {
            Debug.Log("Exit");
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void LoadScene(int sceneId)
        {
            StartCoroutine(LoadSceneCoroutine(sceneId));
        }

        private IEnumerator LoadSceneCoroutine(int sceneId)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneId, LoadSceneMode.Additive);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            
            SceneManager.UnloadSceneAsync(_currentSceneId);
            
            // AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(_currentSceneId);
            // 
            // while (!asyncUnload.isDone)
            // {
            //     yield return null;
            // }

            _currentSceneId = sceneId;
        }

        List<GameObject> FindCameraInScene(Scene scene)
        {
            List<GameObject> cameras = new List<GameObject>();

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                if (rootGameObject.TryGetComponent(out Camera camera))
                {
                    cameras.Add(rootGameObject);
                }
            }

            return cameras;
        }
    }
#endif
}