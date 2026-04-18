using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System;
using Random = UnityEngine.Random;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class CloseTheAdMinigameController : GameController
    {

        [SerializeField] private Transform closeButton;
        [SerializeField] private GameObject itemToDisableOnWin;
        private Camera targetCamera;

        [Header("Movement")]
        [SerializeField] private Vector2 localMinBounds = new Vector2(-1.7f, 0.75f);
        [SerializeField] private Vector2 localMaxBounds = new Vector2(1.7f, 2.25f);
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float idleSpeed = 0.6f;
        [SerializeField] private float clickRadius = 0.33f;

        [Header("Round")]
        [SerializeField] private float timeLimit = 4f;
        [SerializeField] private bool randomizeStartPoint = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState
        {
            Playing,
            Won,
            Lost
        }

        private RoundState roundState = RoundState.Playing;
        private Vector2 velocity = new Vector2(1f, 1f);
        private Vector3 closeButtonStartLocalPosition;
        private float timeRemaining;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;

        private void Awake()
        {
            targetCamera = Camera.main;

            if (itemToDisableOnWin != null)
                itemToDisableOnWin.SetActive(true);
        }

        private void Update()
        {
            HandleInput();

            if (roundState != RoundState.Playing)
                return;

            MoveCloseButton();
            timeRemaining -= Time.deltaTime;

            if (timeRemaining > 0f) return;

            timeRemaining = 0f;
            roundState = RoundState.Lost;
            onFailure.Invoke();
            ReportRoundFinished(false);
        }

        private void OnGUI()
        {
            if (!enabled)
                return;

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(420f, Screen.width - 30f);
            Rect panelRect = new Rect(Screen.width - panelWidth - 16f, 16f, panelWidth, 120f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 40f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 82f, panelRect.width - 28f, 24f);

            GUI.Label(titleRect, "Close The Ad", titleStyle);
            GUI.Label(bodyRect, "Click the moving X before the timer runs out.\nIt keeps sliding around, so you need to react fast.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private void HandleInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            bool restartPressed = keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
            if (roundState == RoundState.Playing)
            {
                if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                    return;

                if (targetCamera == null)
                    targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();

                if (targetCamera == null)
                    return;

                Vector2 cursorScreen = mouse.position.ReadValue();
                Vector3 cursorWorld = targetCamera.ScreenToWorldPoint(new Vector3(cursorScreen.x, cursorScreen.y, Mathf.Abs(targetCamera.transform.position.z)));
                cursorWorld.z = closeButton.position.z;

                if (Vector2.Distance(cursorWorld, closeButton.position) <= clickRadius)
                {
                    roundState = RoundState.Won;
                    if (itemToDisableOnWin != null)
                        itemToDisableOnWin.SetActive(false);
                    onSuccess.Invoke();
                    ReportRoundFinished(true);
                }

                return;
            }

            if (!autoRestartEnabled)
                return;

            if (restartPressed || (mouse != null && mouse.leftButton.wasPressedThisFrame))
                ResetRound();
        }

        private void MoveCloseButton()
        {
            Vector3 nextLocalPosition = closeButton.localPosition;
            nextLocalPosition += (Vector3)(velocity.normalized * moveSpeed * Time.deltaTime);

            if (nextLocalPosition.x <= localMinBounds.x || nextLocalPosition.x >= localMaxBounds.x)
            {
                velocity.x *= -1f;
                nextLocalPosition.x = Mathf.Clamp(nextLocalPosition.x, localMinBounds.x, localMaxBounds.x);
                velocity.y += Random.Range(-idleSpeed, idleSpeed);
            }

            if (nextLocalPosition.y <= localMinBounds.y || nextLocalPosition.y >= localMaxBounds.y)
            {
                velocity.y *= -1f;
                nextLocalPosition.y = Mathf.Clamp(nextLocalPosition.y, localMinBounds.y, localMaxBounds.y);
                velocity.x += Random.Range(-idleSpeed, idleSpeed);
            }

            closeButton.localPosition = nextLocalPosition;
            ClampVelocity();
        }

        private void ClampVelocity()
        {
            if (velocity.sqrMagnitude < 0.2f)
            {
                velocity = new Vector2(1f, 0.8f);
                return;
            }

            velocity = Vector2.ClampMagnitude(velocity, 1.75f);
        }

        protected override void ResetRound()
        {
            roundState = RoundState.Playing;

            if (itemToDisableOnWin != null)
                itemToDisableOnWin.SetActive(true);
            timeRemaining = timeLimit;
            velocity = Random.insideUnitCircle.normalized;
            roundReported = false;

            if (velocity.sqrMagnitude < 0.01f)
                velocity = new Vector2(1f, 0.8f);

            Vector3 nextPosition = closeButtonStartLocalPosition;
            if (randomizeStartPoint)
            {
                nextPosition.x = Random.Range(localMinBounds.x, localMaxBounds.x);
                nextPosition.y = Random.Range(localMinBounds.y, localMaxBounds.y);
            }

            closeButton.localPosition = nextPosition;
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.19f, 0.07f, 0.1f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.22f, 0.17f, 0.19f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.54f, 0.1f, 0.16f) }
            };
        }

        private string GetStatusText()
        {
            switch (roundState)
            {
                case RoundState.Playing:
                    return $"Status: {timeRemaining:0.0}s left. Click the X.";
                case RoundState.Won:
                    return "Status: Ad closed. Click or press R to restart.";
                case RoundState.Lost:
                    return "Status: Too slow. Click or press R to try again.";
                default:
                    return string.Empty;
            }
        }
    }
}
