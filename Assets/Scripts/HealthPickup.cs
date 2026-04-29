using FishNet.Object;
using UnityEngine;

public class HealthPickup : NetworkBehaviour
{
    [Header("Healing Settings")]
    [SerializeField] private int _healAmount = 40;
    [SerializeField] private GameObject _pickupEffectPrefab;

    private PickupManager _manager;
    private Vector3 _spawnPosition;

    public void Init(PickupManager manager, Vector3 spawnPosition)
    {
        _manager = manager;
        _spawnPosition = spawnPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Только сервер обрабатывает подбор
        if (!base.IsServerInitialized) return;

        // Проверяем, что это игрок
        if (!other.TryGetComponent<PlayerNetwork>(out PlayerNetwork player)) return;

        // Мёртвый игрок не подбирает аптечки (используем .Value)
        if (!player.IsAlive.Value) return;

        // Не лечить при полном HP (используем .Value)
        if (player.HP.Value >= 100) return;

        // Лечим игрока
        int newHP = Mathf.Min(100, player.HP.Value + _healAmount);

        // Присваиваем через .Value
        player.HP.Value = newHP;

        // Эффект подбора
        if (_pickupEffectPrefab != null)
        {
            GameObject effect = Instantiate(_pickupEffectPrefab, transform.position, Quaternion.identity);
            NetworkObject effectNetwork = effect.GetComponent<NetworkObject>();
            if (effectNetwork != null)
                base.Spawn(effectNetwork);
            else
                Destroy(effect, 2f);
        }

        // Сообщаем о подборе
        if (_manager != null)
        {
            _manager.OnPickedUp(_spawnPosition);
        }

        // Уничтожаем аптечку
        base.Despawn();
    }
}