using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private float _jumpHeight = 2f;

    [Header("Mouse Look")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private Transform _cameraTransform;

    private CharacterController _characterController;
    private float _verticalVelocity;
    private float _xRotation = 0f;
    private float _currentSpeed;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // Настройка для владельца
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;

            if (_cameraTransform == null)
                _cameraTransform = GetComponentInChildren<Camera>().transform;

            // Включаем обработку ввода
            enabled = true;
        }
        else
        {
            // Не обрабатывать ввод для других целей
            enabled = false;

            // Удаляем AudioListener с чужих персонажей
            var audioListener = GetComponent<AudioListener>();
            if (audioListener != null)
                Destroy(audioListener);
        }
    }

    private void Update()
    {
        // Проверка IsOwner
        if (!IsOwner) return;

        var playerNetwork = GetComponent<PlayerNetwork>();
        if (playerNetwork != null && !playerNetwork.IsAlive.Value) return;

        HandleMovement();
        HandleMouseLook();
    }

    private void HandleMovement()
    {
        // Получаем ввод только если мы владелец
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // Если нет ввода - не двигаемся
        if (horizontal == 0 && vertical == 0 && _characterController.isGrounded)
        {
            if (_verticalVelocity < 0) _verticalVelocity = -2f;
        }

        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        _currentSpeed = isRunning ? _runSpeed : _walkSpeed;

        // Движение относительно поворота персонажа
        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        moveDirection.Normalize();

        Vector3 move = moveDirection * _currentSpeed;

        // Обработка гравитации и прыжков
        if (_characterController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f;

            if (Input.GetButtonDown("Jump"))
            {
                _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }
        }

        _verticalVelocity += _gravity * Time.deltaTime;
        move.y = _verticalVelocity;

        // Перемещаем персонажа
        _characterController.Move(move * Time.deltaTime);

        // Телепорт при падении
        if (transform.position.y < -10f)
        {
            TeleportToSpawnPoint();
        }
    }

    private void HandleMouseLook()
    {
        // Только для владельца
        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
        if (_cameraTransform != null)
            _cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
    }

    private void TeleportToSpawnPoint()
    {
        var spawnPoint = GameObject.FindGameObjectWithTag("SpawnPoint");
        if (spawnPoint != null)
        {
            transform.position = spawnPoint.transform.position;
        }
        else
        {
            transform.position = new Vector3(0, 1, 0);
        }

        _verticalVelocity = 0f;
    }
}