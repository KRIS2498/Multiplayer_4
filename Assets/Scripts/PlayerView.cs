using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TextMeshPro _nicknameText;  // 3D текст над головой
    [SerializeField] private TextMeshPro _hpText;       // 3D текст над головой

    [Header("Optional - Canvas UI")]
    [SerializeField] private TMP_Text _uiNicknameText;  // Если нужен UI Canvas
    [SerializeField] private TMP_Text _uiHpText;        // Если нужен UI Canvas

    public override void OnNetworkSpawn()
    {
        // Ищем PlayerNetwork если не назначен
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
            if (_playerNetwork == null)
            {
                Debug.LogError("PlayerNetwork не найден на объекте!");
                return;
            }
        }

        // Подписываемся на изменения сетевых переменных
        _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged += OnHpChanged;

        // Сразу отображаем текущие значения
        OnNicknameChanged(default, _playerNetwork.Nickname.Value);
        OnHpChanged(0, _playerNetwork.HP.Value);

        Debug.Log($"PlayerView инициализирован для {(_playerNetwork.Nickname.Value.IsEmpty ? "unnamed" : _playerNetwork.Nickname.Value.ToString())}");
    }

    public override void OnNetworkDespawn()
    {
        // Отписываемся от событий
        if (_playerNetwork != null)
        {
            _playerNetwork.Nickname.OnValueChanged -= OnNicknameChanged;
            _playerNetwork.HP.OnValueChanged -= OnHpChanged;
        }
    }

    private void OnNicknameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        string nickname = newValue.ToString();

        // Обновляем 3D текст
        if (_nicknameText != null)
            _nicknameText.text = nickname;

        // Обновляем UI Canvas текст (если используется)
        if (_uiNicknameText != null)
            _uiNicknameText.text = nickname;
    }

    private void OnHpChanged(int oldValue, int newValue)
    {
        string hpText = $"HP: {newValue}";

        // Обновляем 3D текст
        if (_hpText != null)
            _hpText.text = hpText;

        // Обновляем UI Canvas текст (если используется)
        if (_uiHpText != null)
            _uiHpText.text = hpText;

        // Дополнительно: меняем цвет при низком HP
        if (_hpText != null)
        {
            if (newValue <= 30)
                _hpText.color = Color.red;
            else if (newValue <= 60)
                _hpText.color = Color.yellow;
            else
                _hpText.color = Color.green;
        }
    }
}