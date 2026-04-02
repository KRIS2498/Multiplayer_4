using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Движение")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _gravity = -9.81f;

    [Header("Поворот камеры")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _maxLookAngle = 80f;

    [Header("Input Actions (перетащите .inputactions файл)")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Компоненты")]
    [SerializeField] private Camera _playerCamera;

    private float _verticalRotation = 0f;
    private CharacterController _characterController;
    private Vector3 _velocity;

    // Input
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _isRunning;
    private InputActionMap _actionMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _runAction;

    public override void OnNetworkSpawn()
    {
        // Получаем CharacterController
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            Debug.LogError("CharacterController отсутствует! Добавьте компонент.");
            enabled = false;
            return;
        }

        // Находим камеру
        if (_playerCamera == null)
        {
            _playerCamera = GetComponentInChildren<Camera>();
        }

        // Отключаем для не-владельца
        if (!IsOwner)
        {
            if (_playerCamera != null)
                _playerCamera.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        // Настройка курсора
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Настройка Input Actions
        if (_inputActions == null)
        {
            Debug.LogError("Input Actions не назначен! Перетащите .inputactions файл в поле.");
            return;
        }

        _actionMap = _inputActions.FindActionMap("Player");
        if (_actionMap == null)
        {
            Debug.LogError("Action Map 'Player' не найден!");
            return;
        }

        _moveAction = _actionMap.FindAction("Move");
        _lookAction = _actionMap.FindAction("Look");
        _runAction = _actionMap.FindAction("Run");

        if (_moveAction == null) Debug.LogError("Action 'Move' не найден!");
        if (_lookAction == null) Debug.LogError("Action 'Look' не найден!");
        if (_runAction == null) Debug.LogError("Action 'Run' не найден!");

        _actionMap.Enable();

        Debug.Log("PlayerController инициализирован успешно!");
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (_actionMap == null) return;

        // Читаем ввод
        _moveInput = _moveAction.ReadValue<Vector2>();
        _lookInput = _lookAction.ReadValue<Vector2>();
        _isRunning = _runAction.IsPressed();

        // Отладка - раскомментируйте для проверки
        // if (_moveInput != Vector2.zero)
        //     Debug.Log($"Move: {_moveInput}, Run: {_isRunning}");

        HandleMovement();
        HandleMouseLook();
        HandleGravity();
    }

    private void HandleMovement()
    {
        // Вычисляем направление движения
        Vector3 moveDirection = (transform.right * _moveInput.x) + (transform.forward * _moveInput.y);

        // Нормализуем для диагонального движения
        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        // Скорость с учётом бега
        float currentSpeed = _isRunning ? _runSpeed : _walkSpeed;

        // Двигаем персонажа
        Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
        _characterController.Move(movement);
    }

    private void HandleMouseLook()
    {
        float mouseX = _lookInput.x * _mouseSensitivity;
        float mouseY = _lookInput.y * _mouseSensitivity;

        // Поворот по горизонтали
        transform.Rotate(Vector3.up * mouseX);

        // Поворот по вертикали (ограниченный)
        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -_maxLookAngle, _maxLookAngle);
        _playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
    }

    private void HandleGravity()
    {
        // Гравитация
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        _velocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    private void OnDisable()
    {
        if (_actionMap != null && IsOwner)
        {
            _actionMap.Disable();
        }
    }
}