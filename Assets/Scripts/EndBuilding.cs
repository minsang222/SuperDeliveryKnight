using UnityEngine;

public class EndBuilding : MonoBehaviour
{
    private bool _hasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasTriggered)
        {
            return;
        }

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsSlashHitbox(other))
        {
            return;
        }

        _hasTriggered = true;
        player.NotifyLevelComplete();
    }
}
