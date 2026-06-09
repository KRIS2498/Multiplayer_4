using FishNet.Object;
using UnityEngine;

public class RoomPickup : NetworkBehaviour
{
    public enum PickupType { Health, Ammo }

    [Header("Pickup Settings")]
    [SerializeField] private PickupType _type = PickupType.Health;
    [SerializeField] private int _healAmount = 40;
    [SerializeField] private int _ammoRestoreAmount = -1;
    [SerializeField] private GameObject _pickupEffectPrefab;

    private int _pickupIndex;
    private System.Action<int> _onPickedUp;

    public void Init(int index, System.Action<int> onPickedUp)
    {
        _pickupIndex = index;
        _onPickedUp = onPickedUp;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!base.IsServerInitialized) return;
        if (!other.TryGetComponent<PlayerNetwork>(out PlayerNetwork player)) return;
        if (!player.IsAlive.Value) return;

        bool pickedUp = false;

        if (_type == PickupType.Health)
        {
            if (player.HP.Value >= 100) return;
            int newHP = Mathf.Min(100, player.HP.Value + _healAmount);
            player.HP.Value = newHP;
            pickedUp = true;
        }
        else if (_type == PickupType.Ammo)
        {
            PlayerShooting shooting = other.GetComponent<PlayerShooting>();
            if (shooting == null) return;
            if (shooting._currentAmmo.Value >= shooting.MaxAmmo) return;

            int restore = _ammoRestoreAmount > 0 ? _ammoRestoreAmount : shooting.MaxAmmo;
            shooting._currentAmmo.Value = Mathf.Min(shooting.MaxAmmo, shooting._currentAmmo.Value + restore);
            pickedUp = true;
        }

        if (!pickedUp) return;

        if (_pickupEffectPrefab != null)
        {
            GameObject effect = Instantiate(_pickupEffectPrefab, transform.position, Quaternion.identity);
            NetworkObject effectNet = effect.GetComponent<NetworkObject>();
            if (effectNet != null)
                base.Spawn(effectNet);
            else
                Destroy(effect, 2f);
        }

        _onPickedUp?.Invoke(_pickupIndex);
        base.Despawn();
    }
}
