using UnityEngine;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class TimingHitMinigameBootstrap : MonoBehaviour
    {
        private static Sprite cachedSquareSprite;

        private void Awake()
        {
            if (transform.Find("TimingRoot") != null)
            {
                return;
            }

            ConfigureCamera();
            BuildDemo();
        }

        private void ConfigureCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindFirstObjectByType<Camera>();
            }

            if (mainCamera == null)
            {
                return;
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5.4f;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            mainCamera.backgroundColor = new Color(0.82f, 0.9f, 0.98f, 1f);
        }

        private void BuildDemo()
        {
            Transform root = CreateEmpty("TimingRoot", transform, Vector3.zero);

            CreateBlock("Backdrop", root, new Vector3(0f, 0f, 0f), new Vector2(13.5f, 9.4f), new Color(0.94f, 0.96f, 1f), -10);
            CreateBlock("CardShadow", root, new Vector3(0.16f, -0.18f, 0f), new Vector2(7.2f, 6.1f), new Color(0.16f, 0.14f, 0.28f, 0.24f), -2);
            CreateBlock("CardBody", root, new Vector3(0f, 0f, 0f), new Vector2(7f, 5.9f), new Color(1f, 0.99f, 0.97f), -1);
            CreateBlock("Header", root, new Vector3(0f, 2.25f, 0f), new Vector2(7f, 0.78f), new Color(0.45f, 0.36f, 0.95f), 0);
            CreateBlock("Footer", root, new Vector3(0f, -2.05f, 0f), new Vector2(5.4f, 0.34f), new Color(0.87f, 0.9f, 1f), 0);

            Transform dial = CreateEmpty("Dial", root, new Vector3(0f, -0.05f, 0f));
            CreateBlock("DialFill", dial, Vector3.zero, new Vector2(3.2f, 3.2f), new Color(0.95f, 0.97f, 1f), 1);
            CreateBlock("DialInner", dial, Vector3.zero, new Vector2(2.4f, 2.4f), new Color(1f, 0.99f, 0.97f), 2);

            for (int i = 0; i < 24; i++)
            {
                float angle = i * 15f;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 position = new Vector3(Mathf.Sin(radians) * 1.65f, Mathf.Cos(radians) * 1.65f, 0f);
                Transform tick = CreateBlock($"Tick_{i:00}", dial, position, new Vector2(0.12f, 0.34f), new Color(0.75f, 0.8f, 0.95f), 3);
                tick.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }

            Transform goodLeft = CreateBlock("GoodLeft", dial, new Vector3(-0.42f, 1.68f, 0f), new Vector2(0.38f, 0.24f), new Color(1f, 0.79f, 0.25f), 4);
            goodLeft.localRotation = Quaternion.Euler(0f, 0f, 20f);
            Transform goodMid = CreateBlock("GoodMiddle", dial, new Vector3(0f, 1.76f, 0f), new Vector2(0.42f, 0.24f), new Color(1f, 0.79f, 0.25f), 4);
            goodMid.localRotation = Quaternion.identity;
            Transform goodRight = CreateBlock("GoodRight", dial, new Vector3(0.42f, 1.68f, 0f), new Vector2(0.38f, 0.24f), new Color(1f, 0.79f, 0.25f), 4);
            goodRight.localRotation = Quaternion.Euler(0f, 0f, -20f);

            CreateBlock("PerfectZone", dial, new Vector3(0f, 1.82f, 0f), new Vector2(0.28f, 0.2f), new Color(0.19f, 0.86f, 0.57f), 5);

            Transform indicatorPivot = CreateEmpty("IndicatorPivot", dial, Vector3.zero);
            CreateBlock("IndicatorArm", indicatorPivot, new Vector3(0f, 0.82f, 0f), new Vector2(0.12f, 1.62f), new Color(0.16f, 0.18f, 0.3f), 7);
            CreateBlock("IndicatorTip", indicatorPivot, new Vector3(0f, 1.72f, 0f), new Vector2(0.28f, 0.28f), new Color(0.95f, 0.35f, 0.38f), 8);
            CreateBlock("IndicatorHub", dial, Vector3.zero, new Vector2(0.34f, 0.34f), new Color(0.16f, 0.18f, 0.3f), 9);

            TimingHitMinigameController controller = GetComponent<TimingHitMinigameController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<TimingHitMinigameController>();
            }

            controller.SetupDemo(indicatorPivot);
        }

        private Transform CreateEmpty(string objectName, Transform parent, Vector3 localPosition)
        {
            GameObject node = new GameObject(objectName);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition;
            node.transform.localRotation = Quaternion.identity;
            node.transform.localScale = Vector3.one;
            return node.transform;
        }

        private Transform CreateBlock(string objectName, Transform parent, Vector3 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            GameObject block = new GameObject(objectName);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return block.transform;
        }

        private Sprite GetSquareSprite()
        {
            if (cachedSquareSprite != null)
            {
                return cachedSquareSprite;
            }

            cachedSquareSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            cachedSquareSprite.name = "RuntimeSquare";
            return cachedSquareSprite;
        }
    }
}
