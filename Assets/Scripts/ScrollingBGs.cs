using UnityEngine;

public class ScrollingBGs : MonoBehaviour
{
    [Header("Sprites (assign 3 per layer)")]
    [SerializeField] private GameObject[] bgs;
    [SerializeField] private GameObject[] fgs;

    [Header("Scroll settings")]
    [SerializeField, Min(0f)] private float bgMovespdMultiplier;
    [SerializeField, Min(0f)] private float fgMovespdMultiplier;
    [SerializeField, Min(0.01f)] private float bgOffset = 20f;
    [SerializeField, Min(0.01f)] private float fgOffset = 20f;

    private bool _hasPreviousPlayerY;
    private float _previousPlayerY;

    private void Update()
    {
        if (Player.Instance == null)
        {
            return;
        }

        Rigidbody2D playerRigidbody = Player.Instance.GetComponent<Rigidbody2D>();
        float playerMoveSpeed = playerRigidbody != null ? playerRigidbody.linearVelocityX : 0f;

        Vector3 playerPosition = Player.Instance.transform.position;
        float playerYMovement = _hasPreviousPlayerY ? playerPosition.y - _previousPlayerY : 0f;
        _previousPlayerY = playerPosition.y;
        _hasPreviousPlayerY = true;

        ScrollLayer(bgs, playerMoveSpeed * bgMovespdMultiplier, playerYMovement * bgMovespdMultiplier,
            bgOffset, playerPosition.x);
        ScrollLayer(fgs, playerMoveSpeed * fgMovespdMultiplier, playerYMovement * fgMovespdMultiplier,
            fgOffset, playerPosition.x);
    }

    private static void ScrollLayer(GameObject[] sprites, float moveSpeed, float verticalMovement,
        float offset, float playerX)
    {
        // REFACTOR: do nothing
    }
}
