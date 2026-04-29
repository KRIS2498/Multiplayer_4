using FishNet.Object;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private float _jumpHeight = 2f;

    [Header("Mouse Look")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _maxLookAngle = 80f;
    [SerializeField] private Transform _cameraTransform;

    private CharacterController _characterController;
    private float _verticalVelocity;
    private float _xRotation = 0f;
    private Vector2 _moveInput;
    private bool _isRunning;
    private bool _isJumping;
    private bool _isCursorLocked = true;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    public override void OnStartNetwork()
    {
        if (!base.Owner.IsLocalClient)
        {
            // Отключаем камеру для чужих игроков
            if (_cameraTransform != null)
                _cameraTransform.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        // Настройка камеры для своего игрока
        if (_cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                _cameraTransform = cam.transform;
        }

        // Блокируем курсор
        LockCursor(true);
    }

    private void Update()
    {
        if (!base.IsOwner) return;

        // Проверка на жизнь
        PlayerNetwork playerNetwork = GetComponent<PlayerNetwork>();
        if (playerNetwork != null && !playerNetwork.IsAlive.Value) return;

        // Получаем ввод
        GetInput();

        // Обработка движений
        HandleMovement();
        HandleMouseLook();
        HandleJump();
        HandleGravity();

        // Блокировка/разблокировка курсора по Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _isCursorLocked = !_isCursorLocked;
            LockCursor(_isCursorLocked);
        }
    }

    private void GetInput()
    {
        // Движение
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(horizontal, vertical);

        // Бег
        _isRunning = Input.GetKey(KeyCode.LeftShift);

        // Прыжок
        _isJumping = Input.GetButtonDown("Jump");
    }

    private void HandleMovement()
    {
        // Движение относительно поворота камеры (а не персонажа!)
        Vector3 moveDirection = _cameraTransform.right * _moveInput.x + _cameraTransform.forward * _moveInput.y;
        moveDirection.y = 0;
        moveDirection.Normalize();

        float currentSpeed = _isRunning ? _runSpeed : _walkSpeed;

        Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
        movement.y = _verticalVelocity * Time.deltaTime;

        _characterController.Move(movement);
    }

    private void HandleMouseLook()
    {
        if (!_isCursorLocked) return;

        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        // Поворот персонажа по горизонтали
        transform.Rotate(Vector3.up * mouseX);

        // Поворот камеры по вертикали
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -_maxLookAngle, _maxLookAngle);
        _cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
    }

    private void HandleJump()
    {
        if (_isJumping && _characterController.isGrounded)
        {
            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        }
    }

    private void HandleGravity()
    {
        if (_characterController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f;
        }

        _verticalVelocity += _gravity * Time.deltaTime;
    }

    private void LockCursor(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}