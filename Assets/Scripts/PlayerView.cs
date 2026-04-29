using TMPro;
using FishNet.Object;
using UnityEngine;

public class PlayerView : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerNetwork _playerNetwork;
    [SerializeField] private TextMeshPro _nicknameText;
    [SerializeField] private TextMeshPro _hpText;

    private void Start()
    {
        if (_playerNetwork == null)
        {
            _playerNetwork = GetComponent<PlayerNetwork>();
            if (_playerNetwork == null) return;
        }
    }

    public override void OnStartNetwork()
    {
        if (_playerNetwork == null) return;

        // Используем .Value для доступа к SyncVar
        UpdateNicknameUI(_playerNetwork.Nickname.Value);
        UpdateHPUI(_playerNetwork.HP.Value);
    }

    private void UpdateNicknameUI(string nickname)
    {
        if (_nicknameText != null)
            _nicknameText.text = nickname;
    }

    private void UpdateHPUI(int hp)
    {
        if (_hpText != null)
        {
            _hpText.text = $"HP: {hp}";

            if (hp <= 30)
                _hpText.color = Color.red;
            else if (hp <= 60)
                _hpText.color = Color.yellow;
            else
                _hpText.color = Color.green;
        }
    }
}