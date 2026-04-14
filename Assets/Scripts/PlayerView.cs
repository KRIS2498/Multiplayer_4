using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TextMeshPro _nicknameText;
    [SerializeField] private TextMeshPro _hpText;

    public override void OnNetworkSpawn()
    {
        // Ищем PlayerNetwork
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
            if (_playerNetwork == null)
            {
                return;
            }
        }

        // Подписываемся на изменения сетевых переменных
        _playerNetwork.Nickname.OnValueChanged += OnNicknameChanged;
        _playerNetwork.HP.OnValueChanged += OnHpChanged;

        // Сразу отображаем текущие значения
        OnNicknameChanged(default, _playerNetwork.Nickname.Value);
        OnHpChanged(0, _playerNetwork.HP.Value);
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
    }

    private void OnHpChanged(int oldValue, int newValue)
    {
        string hpText = $"HP: {newValue}";

        // Обновляем 3D текст
        if (_hpText != null)
            _hpText.text = hpText;

        // Меняем цвет при низком HP
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