using UnityEngine;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class CloseTheAdMinigameBootstrap : MonoBehaviour
    {
        private static Sprite cachedSquareSprite;

        private void Awake()
        {
            if (transform.Find("AdRoot") != null)
            {
                return;
            }

            ConfigureCamera();

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindFirstObjectByType<Camera>();
            }

            BuildDemo(mainCamera);
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

        private void BuildDemo(Camera mainCamera)
        {
            Transform root = CreateEmpty("AdRoot", transform, Vector3.zero);

            CreateBlock("Dimmer", root, new Vector3(0f, 0f, 0f), new Vector2(13.5f, 9.5f), new Color(0f, 0f, 0f, 0.42f), -10);

            Transform window = CreateEmpty("AdWindow", root, new Vector3(0f, 0f, 0f));
            CreateBlock("WindowShadow", window, new Vector3(0.18f, -0.18f, 0f), new Vector2(7.6f, 5.3f), new Color(0.18f, 0.12f, 0.18f, 0.35f), -2);
            CreateBlock("WindowBody", window, Vector3.zero, new Vector2(7.4f, 5.1f), new Color(1f, 0.98f, 0.95f), -1);
            CreateBlock("HeaderBar", window, new Vector3(0f, 2.18f, 0f), new Vector2(7.4f, 0.72f), new Color(0.93f, 0.23f, 0.36f), 0);
            CreateBlock("PromoStrip", window, new Vector3(0f, -0.28f, 0f), new Vector2(6.3f, 0.9f), new Color(1f, 0.82f, 0.26f), 1);
            CreateBlock("PromoButton", window, new Vector3(0f, -1.55f, 0f), new Vector2(2.45f, 0.72f), new Color(0.2f, 0.69f, 0.43f), 1);
            CreateBlock("ImageLeft", window, new Vector3(-1.55f, 0.82f, 0f), new Vector2(2.15f, 1.3f), new Color(0.44f, 0.77f, 0.95f), 1);
            CreateBlock("ImageRight", window, new Vector3(1.55f, 0.82f, 0f), new Vector2(2.15f, 1.3f), new Color(0.95f, 0.52f, 0.58f), 1);

            Transform closeButton = CreateEmpty("CloseButton", window, new Vector3(2.65f, 2.05f, 0f));
            CreateBlock("CloseBg", closeButton, Vector3.zero, new Vector2(0.56f, 0.56f), new Color(0.93f, 0.96f, 1f), 4);
            Transform slashA = CreateBlock("SlashA", closeButton, Vector3.zero, new Vector2(0.12f, 0.52f), new Color(0.15f, 0.2f, 0.28f), 5);
            slashA.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Transform slashB = CreateBlock("SlashB", closeButton, Vector3.zero, new Vector2(0.12f, 0.52f), new Color(0.15f, 0.2f, 0.28f), 5);
            slashB.localRotation = Quaternion.Euler(0f, 0f, -45f);

            CloseTheAdMinigameController controller = GetComponent<CloseTheAdMinigameController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<CloseTheAdMinigameController>();
            }

            controller.SetupDemo(closeButton, mainCamera);
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
