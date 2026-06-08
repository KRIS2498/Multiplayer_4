using UnityEngine;

public class DifficultyManager : MonoBehaviour
{
    [Header("Difficulty Curve")]
    [SerializeField] private AnimationCurve _hpMultiplier = AnimationCurve.Linear(0, 1f, 50, 5f);
    [SerializeField] private AnimationCurve _damageMultiplier = AnimationCurve.Linear(0, 1f, 50, 3f);
    [SerializeField] private AnimationCurve _speedMultiplier = AnimationCurve.Linear(0, 1f, 50, 1.5f);
    [SerializeField] private AnimationCurve _spawnCountMultiplier = AnimationCurve.Linear(0, 1f, 50, 3f);

    [Header("Floor-based enemy HP")]
    [SerializeField] private int _baseEnemyHP = 30;
    [SerializeField] private int _baseEnemyDamage = 10;

    public int Floor { get; private set; }

    public void SetFloor(int floor)
    {
        Floor = floor;
    }

    public float GetHPMultiplier() => _hpMultiplier.Evaluate(Floor);
    public float GetDamageMultiplier() => _damageMultiplier.Evaluate(Floor);
    public float GetSpeedMultiplier() => _speedMultiplier.Evaluate(Floor);
    public float GetSpawnCountMultiplier() => _spawnCountMultiplier.Evaluate(Floor);

    public int GetEnemyHP() => Mathf.RoundToInt(_baseEnemyHP * GetHPMultiplier());
    public int GetEnemyDamage() => Mathf.RoundToInt(_baseEnemyDamage * GetDamageMultiplier());

    public int GetSpawnCount(int baseCount)
    {
        return Mathf.RoundToInt(baseCount * GetSpawnCountMultiplier());
    }
}
