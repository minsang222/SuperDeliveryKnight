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
    [SerializeField] private float fixedY = 0f;

    private void Start()
    {
        SetLayerY(bgs, fixedY);
        SetLayerY(fgs, fixedY);
    }

    private void Update()
    {
        if (Player.Instance == null)
        {
            return;
        }

        Rigidbody2D playerRigidbody = Player.Instance.GetComponent<Rigidbody2D>();
        float playerMoveSpeed = playerRigidbody != null ? playerRigidbody.linearVelocityX : 0f;

        float playerX = Player.Instance.transform.position.x;
        ScrollLayer(bgs, playerMoveSpeed * bgMovespdMultiplier, bgOffset, playerX);
        ScrollLayer(fgs, playerMoveSpeed * fgMovespdMultiplier, fgOffset, playerX);
    }

    private static void SetLayerY(GameObject[] sprites, float y)
    {
        if (sprites == null)
        {
            return;
        }

        foreach (GameObject sprite in sprites)
        {
            if (sprite == null)
            {
                continue;
            }

            Vector3 position = sprite.transform.position;
            position.y = y;
            sprite.transform.position = position;
        }
    }

    private static void ScrollLayer(GameObject[] sprites, float moveSpeed, float offset, float playerX)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return;
        }

        foreach (GameObject sprite in sprites)
        {
            if (sprite == null)
            {
                continue;
            }

            Transform spriteTransform = sprite.transform;
            Vector3 position = spriteTransform.position;
            position.x -= moveSpeed * Time.deltaTime;
            spriteTransform.position = position;
        }

        float recycleX = playerX - offset;
        foreach (GameObject sprite in sprites)
        {
            if (sprite == null || sprite.transform.position.x >= recycleX)
            {
                continue;
            }

            float rightMostX = float.NegativeInfinity;
            foreach (GameObject otherSprite in sprites)
            {
                if (otherSprite != null && otherSprite != sprite)
                {
                    rightMostX = Mathf.Max(rightMostX, otherSprite.transform.position.x);
                }
            }

            sprite.transform.position = new Vector3(
                float.IsNegativeInfinity(rightMostX) ? playerX + offset : rightMostX + offset,
                sprite.transform.position.y,
                sprite.transform.position.z);
        }
    }
}
