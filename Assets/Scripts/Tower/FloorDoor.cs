using UnityEngine;

public class FloorDoor : MonoBehaviour
{
    private bool _isOpen;

    public void OpenDoor()
    {
        _isOpen = true;
        GetComponent<Collider>().enabled = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isOpen) return;
        if (other.GetComponent<PlayerNetwork>() == null) return;

        TowerManager tower = TowerManager.Instance;
        if (tower != null)
            tower.OnPlayerEnteredExit();
    }
}
