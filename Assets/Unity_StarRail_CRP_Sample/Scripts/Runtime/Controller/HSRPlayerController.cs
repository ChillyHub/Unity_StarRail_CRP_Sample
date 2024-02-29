using System;
using Cinemachine;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity_StarRail_CRP_Sample
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class HSRPlayerController : MonoBehaviour
    {
        public float walkSpeed = 1.0f;
        public float runSpeed = 5.0f;
        public float fastRunSpeed = 10.0f;

        public float speedChangeRate = 10.0f;
        
        [Range(0.0f, 0.3f)]
        public float rotationSmoothTime = 0.12f;
        
        [Header("Player Grounded")]
        public float groundedOffset = -0.14f;
        public float groundedRadius = 0.28f;
        public LayerMask groundLayers;
        
        [Header("Cinemachine")]
        public GameObject cinemachineCameraTarget;
        
        public float maxDistance = 5.0f;
        public float minDistance = 0.5f;
        public float distanceChangeRate = 10.0f;
        
        public float topClamp = 70.0f;
        public float bottomClamp = -30.0f;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
        private InputActionMap _player;
        private InputAction _moveAction;
        private InputAction _walkMoveAction;
        private InputAction _lookAction;
        private InputAction _walkAction;
        private InputAction _fastRunAction;
        private InputAction _zoomAction;
        private string _currentControlScheme;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private Cinemachine3rdPersonFollow _personFollow;
        
        private const float _threshold = 0.01f;

        private bool _hasAnimator;
        
        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;
        private float _cameraDistance;
        private float _targetCameraDistance;
        
        // Player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        
        private bool _onWalk = false;
        private bool _onWalkPressing = false;
        private bool _onFastRun = false;
        private bool _onFastRunPressing = false;

        private bool _grounded;
        private float _verticalVelocity;
        
        // Animation ID
        private int _animIDSpeed;
        private int _animIDMotionSpeed;
        
        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }
        
        private void Awake()
        {
            
        }

        private void Start()
        {
            _cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;
            
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _personFollow = GameObject.Find("PlayerFollowCamera").GetComponent<CinemachineVirtualCamera>()
                .GetCinemachineComponent<Cinemachine3rdPersonFollow>();

#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
            _player = _playerInput.actions.FindActionMap("Player");
            _moveAction = _player.FindAction("Move");
            _walkMoveAction = _player.FindAction("WalkMove");
            _lookAction = _player.FindAction("Look");
            _walkAction = _player.FindAction("Walk");
            _fastRunAction = _player.FindAction("FastRun");
            _zoomAction = _player.FindAction("Zoom");
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif
            
            _cameraDistance = _personFollow.CameraDistance;
            _targetCameraDistance = _cameraDistance;
            
            _onWalk = false;
            _onWalkPressing = false;
            _onFastRun = false;
            _onFastRunPressing = false;

            AssignAnimationIDs();
            
            GroundedCheck();
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            Gravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM 
            _player.Disable();
            //_moveAction.Disable();
            //_walkMoveAction.Disable();
            //_lookAction.Disable();
            //_walkAction.Disable();
            //_fastRunAction.Disable();
            //_zoomAction.Disable();
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (_grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z),
                groundedRadius);
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }
        
        private void Gravity()
        {
            if (_grounded)
            {
                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < 50.0f)
            {
                _verticalVelocity += -9.8f * Time.deltaTime;
            }
        }
        
        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset,
                transform.position.z);
            _grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            //if (_hasAnimator)
            //{
            //    _animator.SetBool(_animIDGrounded, _grounded);
            //}
        }
        
        private void Move()
        {
            Vector2 move = _moveAction.ReadValue<Vector2>();
            Vector2 walkMove = _walkMoveAction.ReadValue<Vector2>();
            
            UpdateMoveState(move, walkMove);

            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _onWalk ? walkSpeed : _onFastRun ? fastRunSpeed : runSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (move == Vector2.zero && walkMove == Vector2.zero)
            {
                targetSpeed = 0.0f;
            }

            // a reference to the players current horizontal velocity
            Vector3 controllerVelocity = _controller.velocity;
            float currentHorizontalSpeed = new Vector3(controllerVelocity.x, 0.0f, controllerVelocity.z).magnitude;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - 0.000001f ||
                currentHorizontalSpeed > targetSpeed + 0.000001f)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * speedChangeRate);
                
                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);
            if (_animationBlend < 0.01f)
            {
                _animationBlend = 0.0f;
            }

            // normalise input direction
            Vector3 inputDirection = Vector3.zero;
            if (move != Vector2.zero)
            {
                inputDirection = new Vector3(move.x, 0.0f, move.y).normalized;
            }
            else if (walkMove != Vector2.zero)
            {
                inputDirection = new Vector3(walkMove.x, 0.0f, walkMove.y).normalized;
            }

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (inputDirection != Vector3.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  cinemachineCameraTarget.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    rotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }
            
            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + 
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, 1.0f);
            }
        }
        
        private void CameraRotation()
        {
            Vector2 look = _lookAction.ReadValue<Vector2>();
            float zoom = _zoomAction.ReadValue<float>();

#if UNITY_ANDROID
            look *= 0.4f;
#endif

            // if there is an input and camera position is not fixed
            if (look.sqrMagnitude >= _threshold)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, bottomClamp, topClamp);
            
            // Zoom
            _targetCameraDistance -= zoom / 360.0f;
            _targetCameraDistance = Mathf.Clamp(_targetCameraDistance, minDistance, maxDistance);
            _cameraDistance = Mathf.Lerp(_cameraDistance,  _targetCameraDistance, 
                Time.deltaTime * distanceChangeRate);
            //_cameraDistance -= zoom * Time.deltaTime * distanceChangeRate / 18.0f;
            //_cameraDistance = Mathf.Clamp(_cameraDistance, minDistance, maxDistance);
            _personFollow.CameraDistance = _cameraDistance;

            // Cinemachine will follow this target
            cinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch,
                _cinemachineTargetYaw, 0.0f);
        }

        private void UpdateMoveState(Vector2 move, Vector2 walkMove)
        {
            if (_fastRunAction.IsPressed() && !_onFastRunPressing)
            {
                _onFastRunPressing = true;
                _onFastRun = !_onFastRun;
            }
            else if (!_fastRunAction.IsPressed() && _onFastRunPressing)
            {
                _onFastRunPressing = false;
            }

            if (move == Vector2.zero)
            {
                _onFastRun = false;
            }

            if (_playerInput.currentControlScheme == "Gamepad")
            {
                if (walkMove != Vector2.zero && move == Vector2.zero)
                {
                    _onWalk = true;
                }
                else
                {
                    _onWalk = false;
                }
            }
            else
            {
                if (_walkAction.IsPressed() && !_onWalkPressing)
                {
                    _onWalkPressing = true;
                    _onWalk = !_onWalk;
                }
                else if (!_walkAction.IsPressed() && _onWalkPressing)
                {
                    _onWalkPressing = false;
                }
            }
        }
        
        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }
}