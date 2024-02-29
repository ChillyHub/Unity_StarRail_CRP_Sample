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
        public Canvas touchZone;

        private InputActionAsset _inputActionAsset;
        private InputActionMap _player;
        private InputActionMap _camera;
        private InputAction _point;
        private InputAction _click;
        private InputAction _esc;
        private InputAction _alt;

        private bool _isPause = false;
        private bool _isPressingEsc = false;
        private bool _isPressingAlt = false;

        private int _currentSceneId;
        private int _loadSceneId;
        
        private void Start()
        {
            _inputActionAsset = GetComponent<InputSystemUIInputModule>().actionsAsset;
            _player = _inputActionAsset.FindActionMap("Player");
            _camera = _inputActionAsset.FindActionMap("Camera");
            _esc = _inputActionAsset.FindActionMap("UI").FindAction("Menu");
            _alt = _inputActionAsset.FindActionMap("UI").FindAction("Alt");
            
            _esc.Enable();
            _alt.Enable();

            // Init load
            InitLoadScene();
            
#if UNITY_ANDROID
            if (touchZone != null)
            {
                touchZone.gameObject.SetActive(true);
            }

            Application.targetFrameRate = 120;
#else
            if (touchZone != null)
            {
                touchZone.gameObject.SetActive(false);
            }
#endif
        }

        private void Update()
        {
            bool esc = Convert.ToBoolean(_esc.ReadValue<float>());
            bool alt = Convert.ToBoolean(_alt.ReadValue<float>());

            if (esc && !_isPressingEsc)
            {
                _isPause = !_isPause;
                _isPressingEsc = true;
            }
            else if (!esc)
            {
                _isPressingEsc = false;
            }
            
            if (alt && !_isPressingAlt)
            {
                _isPressingAlt = true;
            }
            else if (!alt)
            {
                _isPressingAlt = false;
            }

            if (menu == null)
            {
                return;
            }

            //Time.timeScale = _isPause ? 0.0f : 1.0f;
            AudioListener.pause = _isPause;
            menu.gameObject.SetActive(_isPause);
#if UNITY_ANDROID
             if (touchZone == null)
            {
                return;
            }
            touchZone.gameObject.SetActive(!_isPause);
#endif

            if (_isPause || _isPressingAlt)
            {
                _player.Disable();
                _camera.Disable();
#if !UNITY_ANDROID
                Cursor.lockState = CursorLockMode.None;
#endif
            }
            else
            {
                _player.Enable();
                _camera.Enable();
#if !UNITY_ANDROID
                Cursor.lockState = CursorLockMode.Locked;
#endif
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
#if !UNITY_ANDROID
            Cursor.lockState = CursorLockMode.Locked;
#endif
        }
        
        public void OnValueChangedVSync(Int32 value)
        {
            QualitySettings.vSyncCount = value;
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

                if (cameras.Count <= value)
                {
                    return;
                }

                for (int i = 0; i < cameras.Count; i++)
                {
                    cameras.Values[i].SetActive(i == value);
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
        
        private void InitLoadScene(int sceneId = 1)
        {
            StartCoroutine(InitLoadSceneCoroutine(sceneId));
        }

        private void LoadScene(int sceneId)
        {
            StartCoroutine(LoadSceneCoroutine(sceneId));
        }
        
        private IEnumerator InitLoadSceneCoroutine(int sceneId)
        {
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneId, LoadSceneMode.Additive);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            
            SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(sceneId));

            _currentSceneId = 1;
        }

        private IEnumerator LoadSceneCoroutine(int sceneId)
        {
            SceneManager.UnloadSceneAsync(_currentSceneId);
            
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneId, LoadSceneMode.Additive);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            
            SceneManager.SetActiveScene(SceneManager.GetSceneByBuildIndex(sceneId));

            // AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(_currentSceneId);
            // 
            // while (!asyncUnload.isDone)
            // {
            //     yield return null;
            // }

            _currentSceneId = sceneId;
        }

        SortedList<string, GameObject> FindCameraInScene(Scene scene)
        {
            SortedList<string, GameObject> cameras = new SortedList<string, GameObject>();

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                if (rootGameObject.TryGetComponent(out Camera camera))
                {
                    cameras.Add(rootGameObject.name, rootGameObject);
                }
            }

            return cameras;
        }
    }
#endif
}