using UnityEngine;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class CraneMinigameDemoBootstrap : MonoBehaviour
    {
        private static Sprite cachedSquareSprite;

        private void Awake()
        {
            if (transform.Find("DemoRoot") != null)
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
            Transform demoRoot = CreateEmpty("DemoRoot", transform, Vector3.zero);

            CreateBlock("Backdrop", demoRoot, new Vector3(0f, -0.1f, 0f), new Vector2(14f, 9f), new Color(0.9f, 0.95f, 1f), -5);
            CreateBlock("Floor", demoRoot, new Vector3(0f, -4.45f, 0f), new Vector2(13f, 1.5f), new Color(0.24f, 0.28f, 0.38f), -1);
            CreateBlock("FloorTop", demoRoot, new Vector3(0f, -3.84f, 0f), new Vector2(13f, 0.18f), new Color(0.97f, 0.76f, 0.29f), 0);

            Transform frameRoot = CreateEmpty("Frame", demoRoot, Vector3.zero);
            Color frameColor = new Color(0.2f, 0.26f, 0.39f);
            CreateBlock("TopBeam", frameRoot, new Vector3(0f, 4.2f, 0f), new Vector2(12.2f, 0.36f), frameColor, 1);
            CreateBlock("LeftFrame", frameRoot, new Vector3(-5.75f, 3.15f, 0f), new Vector2(0.38f, 1.7f), frameColor, 1);
            CreateBlock("RightFrame", frameRoot, new Vector3(5.75f, 3.15f, 0f), new Vector2(0.38f, 1.7f), frameColor, 1);

            Transform carriage = CreateEmpty("Carriage", demoRoot, new Vector3(-5.2f, 3.3f, 0f));
            CreateBlock("CarriageBody", carriage, Vector3.zero, new Vector2(1.55f, 0.8f), new Color(0.98f, 0.7f, 0.23f), 3);
            CreateBlock("CarriageCab", carriage, new Vector3(0f, 0.12f, 0f), new Vector2(0.72f, 0.24f), new Color(1f, 0.92f, 0.73f), 4);
            Transform rope = CreateBlock("Rope", carriage, new Vector3(0f, -0.72f, 0f), new Vector2(0.12f, 1.4f), new Color(0.15f, 0.18f, 0.24f), 2);

            Transform hook = CreateEmpty("Hook", carriage, new Vector3(0f, -1f, 0f));
            Color hookColor = new Color(0.12f, 0.16f, 0.23f);
            CreateBlock("HookCore", hook, new Vector3(0f, -0.02f, 0f), new Vector2(0.22f, 0.72f), hookColor, 5);
            Transform leftJaw = CreateBlock("LeftJaw", hook, new Vector3(-0.2f, -0.42f, 0f), new Vector2(0.18f, 0.68f), hookColor, 5);
            leftJaw.localRotation = Quaternion.Euler(0f, 0f, 33f);
            Transform rightJaw = CreateBlock("RightJaw", hook, new Vector3(0.2f, -0.42f, 0f), new Vector2(0.18f, 0.68f), hookColor, 5);
            rightJaw.localRotation = Quaternion.Euler(0f, 0f, -33f);

            Transform prize = CreateEmpty("Prize", demoRoot, new Vector3(0f, -3.22f, 0f));
            CreateBlock("PrizeBody", prize, Vector3.zero, new Vector2(1.1f, 1.1f), new Color(0.91f, 0.41f, 0.33f), 6);
            CreateBlock("PrizeInset", prize, new Vector3(0f, 0.02f, 0f), new Vector2(0.72f, 0.72f), new Color(0.98f, 0.82f, 0.52f), 7);
            CreateBlock("PrizeLatch", prize, new Vector3(0f, 0.42f, 0f), new Vector2(0.28f, 0.18f), new Color(0.96f, 0.94f, 0.88f), 8);

            CraneMinigameController controller = GetComponent<CraneMinigameController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<CraneMinigameController>();
            }

            controller.SetupDemo(carriage, hook, rope, prize);
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
