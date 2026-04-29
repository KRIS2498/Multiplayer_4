using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

// Структура данных для ввода (от клиента к серверу)
public struct MoveData : IReplicateData
{
    public float Horizontal;
    public float Vertical;
    public bool IsRunning;
    public bool IsJumping;

    private uint _tick;
    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

// Структура данных для сверки (от сервера к клиенту)
public struct ReconcileData : IReconcileData
{
    public Vector3 Position;
    public Vector3 Velocity;

    private uint _tick;
    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

public class PlayerMovementPredicted : NetworkBehaviour
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
    private Vector3 _velocity;
    private float _verticalRotation = 0f;
    private bool _isRunning;
    private bool _isJumping;

    public override void OnStartNetwork()
    {
        _characterController = GetComponent<CharacterController>();

        if (_cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                _cameraTransform = cam.transform;
        }

        if (base.Owner.IsLocalClient)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (_cameraTransform != null)
                _cameraTransform.gameObject.SetActive(false);
            enabled = false;
        }

        base.TimeManager.OnTick += OnTick;
        base.TimeManager.OnPostTick += OnPostTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnTick -= OnTick;
        base.TimeManager.OnPostTick -= OnPostTick;
    }

    private void OnTick()
    {
        if (base.Owner.IsLocalClient)
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            _isRunning = Input.GetKey(KeyCode.LeftShift);
            _isJumping = Input.GetButtonDown("Jump");

            MoveData md = new MoveData
            {
                Horizontal = horizontal,
                Vertical = vertical,
                IsRunning = _isRunning,
                IsJumping = _isJumping
            };

            Replicate(md);
        }
    }

    private void OnPostTick()
    {
        if (!base.Owner.IsLocalClient) return;
        HandleMouseLook();
    }

    // Этот метод обязателен для FishNet v4
    private ReconcileData CreateReconcile()
    {
        ReconcileData rd = new ReconcileData
        {
            Position = transform.position,
            Velocity = _velocity
        };

        // Вызываем метод Reconcile
        Reconcile(rd);

        return rd;
    }

    [Replicate]
    private void Replicate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        HandleMovement(md);
        HandleJump(md);
        HandleGravity();
    }

    [Reconcile]
    private void Reconcile(ReconcileData rd, Channel channel = Channel.Unreliable)
    {
        float distance = Vector3.Distance(transform.position, rd.Position);
        if (distance > 0.5f)
        {
            transform.position = rd.Position;
            _velocity = rd.Velocity;
        }
    }

    private void HandleMovement(MoveData md)
    {
        Vector3 moveDirection = _cameraTransform.right * md.Horizontal + _cameraTransform.forward * md.Vertical;
        moveDirection.y = 0;
        moveDirection.Normalize();

        float currentSpeed = md.IsRunning ? _runSpeed : _walkSpeed;

        Vector3 movement = moveDirection * currentSpeed * (float)base.TimeManager.TickDelta;
        movement.y = _velocity.y * (float)base.TimeManager.TickDelta;

        _characterController.Move(movement);
    }

    private void HandleJump(MoveData md)
    {
        if (md.IsJumping && _characterController.isGrounded)
        {
            _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        }
    }

    private void HandleGravity()
    {
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        _velocity.y += _gravity * (float)base.TimeManager.TickDelta;
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        _verticalRotation -= mouseY;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -_maxLookAngle, _maxLookAngle);
        _cameraTransform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
    }
}