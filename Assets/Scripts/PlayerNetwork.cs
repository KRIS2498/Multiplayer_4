using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class PlayerNetwork : NetworkBehaviour
{
    [Header("Network Stats")]
    public NetworkVariable<FixedString32Bytes> Nickname = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> HP = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("UI References")]
    [SerializeField] private TextMeshPro _nicknameText; // 3D текст над головой
    [SerializeField] private TextMeshPro _hpText;

    [Header("Settings")]
    [SerializeField] private float _textHeight = 2f;

    private void Start()
    {
        // Если текст не назначен, попробуем найти дочерние объекты
        if (_nicknameText == null)
            _nicknameText = GetComponentInChildren<TextMeshPro>();

        // Подписываемся на изменения сетевых переменных
        Nickname.OnValueChanged += OnNicknameChanged;
        HP.OnValueChanged += OnHPChanged;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Владелец отправляет свой ник на сервер
            SubmitNicknameServerRpc(ConnectionUI.PlayerNickname);
        }

        // Принудительно обновляем UI при спавне
        UpdateNicknameUI(Nickname.Value);
        UpdateHPUI(HP.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitNicknameServerRpc(string nickname)
    {
        // Сервер нормализует ник
        string safeValue = string.IsNullOrWhiteSpace(nickname)
            ? $"Player_{OwnerClientId}"
            : nickname.Trim();

        Nickname.Value = safeValue;
        Debug.Log($"Player {OwnerClientId} set nickname: {safeValue}");
    }

    private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        UpdateNicknameUI(newValue);
    }

    private void OnHPChanged(int oldValue, int newValue)
    {
        UpdateHPUI(newValue);

        // Дополнительно: эффект при получении урона
        if (newValue < oldValue && IsOwner)
        {
            // Визуальный эффект для владельца (можно добавить)
            Debug.Log($"You took {oldValue - newValue} damage! HP: {newValue}");
        }
    }

    private void UpdateNicknameUI(FixedString32Bytes nickname)
    {
        if (_nicknameText != null)
        {
            _nicknameText.text = nickname.ToString();
        }
    }

    private void UpdateHPUI(int hp)
    {
        if (_hpText != null)
        {
            _hpText.text = $"HP: {hp}";
        }
    }

    // Метод для нанесения урона (вызывается с сервера)
    [ServerRpc(RequireOwnership = false)]
    public void TakeDamageServerRpc(int damage)
    {
        if (!IsServer) return;

        int newHP = HP.Value - damage;
        HP.Value = Mathf.Max(0, newHP);

        Debug.Log($"Player {Nickname.Value} took {damage} damage. HP: {HP.Value}");
    }

    private void OnDestroy()
    {
        // Отписываемся от событий
        if (Nickname != null)
            Nickname.OnValueChanged -= OnNicknameChanged;
        if (HP != null)
            HP.OnValueChanged -= OnHPChanged;
    }
}