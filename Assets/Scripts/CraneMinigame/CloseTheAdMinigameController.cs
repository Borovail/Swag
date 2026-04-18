using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;
using System.Collections.Generic;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class CloseTheAdMinigameController : GameController
    {
        [SerializeField] private Transform closeButton;
        [SerializeField] private List<BoxCollider2D> ads = new List<BoxCollider2D>();

        private Camera targetCamera;

        [Header("Movement")]
        [SerializeField] private Vector2 localMinBounds = new Vector2(-1.7f, 0.75f);
        [SerializeField] private Vector2 localMaxBounds = new Vector2(1.7f, 2.25f);
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float idleSpeed = 0.6f;
        [SerializeField] private float clickRadius = 0.33f;
        [SerializeField] private float boundsInset = 0.24f;
        [SerializeField] private float boundsThickness = 0.08f;
        [SerializeField] private int boundsSortingOrder = 11;

        [Header("Round")]
        [SerializeField] private float timeLimit = 4f;
        [SerializeField] private bool randomizeStartPoint = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private AudioSource _audio;

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
        private readonly List<BoxCollider2D> runtimeAds = new List<BoxCollider2D>();
        private int currentAdIndex;
        private bool isConfigured;

        private void Awake()
        {
            targetCamera = Camera.main;
            if (closeButton != null)
                closeButtonStartLocalPosition = closeButton.localPosition;

            _audio = GetComponent<AudioSource>();

            CacheAds();
            RefreshCurrentAdBounds();
        }

        private void Start()
        {
            CacheAds();
            if (!isConfigured)
            {
                Debug.LogWarning($"{nameof(CloseTheAdMinigameController)} requires a close button and at least one ad collider.", this);
                enabled = false;
                return;
            }

            ResetRound();
        }

        private void OnValidate()
        {
            boundsInset = Mathf.Max(0f, boundsInset);
            boundsThickness = Mathf.Max(0.01f, boundsThickness);

            if (!Application.isPlaying)
                return;

            CacheAds();
            RefreshCurrentAdBounds();
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
                    CloseCurrentAd();
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
            timeRemaining = timeLimit;
            velocity = Random.insideUnitCircle.normalized;
            roundReported = false;
            currentAdIndex = 0;

            if (velocity.sqrMagnitude < 0.01f)
                velocity = new Vector2(1f, 0.8f);

            for (int i = 0; i < runtimeAds.Count; i++)
            {
                if (runtimeAds[i] != null)
                    runtimeAds[i].gameObject.SetActive(true);
            }

            RefreshCurrentAdBounds();
            PlaceCloseButton(randomizeStartPoint);
        }

        private void CloseCurrentAd()
        {
            _audio.Play();

            BoxCollider2D currentAd = GetCurrentAd();
            if (currentAd == null)
            {
                FinishAsWon();
                return;
            }

            currentAd.gameObject.SetActive(false);
            currentAdIndex++;

            if (currentAdIndex >= runtimeAds.Count)
            {
                FinishAsWon();
                return;
            }

            velocity = Random.insideUnitCircle.normalized;
            if (velocity.sqrMagnitude < 0.01f)
                velocity = new Vector2(1f, 0.8f);

            RefreshCurrentAdBounds();
            PlaceCloseButton(true);
        }

        private void FinishAsWon()
        {
            roundState = RoundState.Won;
            onSuccess.Invoke();
            ReportRoundFinished(true);
        }

        private void CacheAds()
        {
            runtimeAds.Clear();

            for (int i = 0; i < ads.Count; i++)
            {
                if (ads[i] != null)
                    runtimeAds.Add(ads[i]);
            }

            isConfigured = closeButton != null && runtimeAds.Count > 0;
        }

        private BoxCollider2D GetCurrentAd()
        {
            if (currentAdIndex < 0 || currentAdIndex >= runtimeAds.Count)
                return null;

            return runtimeAds[currentAdIndex];
        }

        private void RefreshCurrentAdBounds()
        {
            BoxCollider2D currentAd = GetCurrentAd();
            if (currentAd == null || closeButton == null)
                return;

            Transform space = closeButton.parent != null ? closeButton.parent : transform;

            Bounds worldBounds = currentAd.bounds;
            Vector3 localMin = space.InverseTransformPoint(new Vector3(worldBounds.min.x, worldBounds.min.y, closeButton.position.z));
            Vector3 localMax = space.InverseTransformPoint(new Vector3(worldBounds.max.x, worldBounds.max.y, closeButton.position.z));

            float paddingX = Mathf.Max(boundsInset, clickRadius * 0.75f);
            float paddingY = Mathf.Max(boundsInset, clickRadius * 0.75f);

            localMinBounds = new Vector2(localMin.x + paddingX, localMin.y + paddingY);
            localMaxBounds = new Vector2(localMax.x - paddingX, localMax.y - paddingY);

            if (localMaxBounds.x < localMinBounds.x)
            {
                float centerX = (localMin.x + localMax.x) * 0.5f;
                localMinBounds.x = centerX;
                localMaxBounds.x = centerX;
            }

            if (localMaxBounds.y < localMinBounds.y)
            {
                float centerY = (localMin.y + localMax.y) * 0.5f;
                localMinBounds.y = centerY;
                localMaxBounds.y = centerY;
            }
        }

        private void PlaceCloseButton(bool useRandomPosition)
        {
            if (closeButton == null)
                return;

            Vector3 nextPosition = closeButtonStartLocalPosition;
            nextPosition.x = Mathf.Clamp(nextPosition.x, localMinBounds.x, localMaxBounds.x);
            nextPosition.y = Mathf.Clamp(nextPosition.y, localMinBounds.y, localMaxBounds.y);

            if (useRandomPosition)
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
            int adsLeft = Mathf.Max(0, runtimeAds.Count - currentAdIndex);
            switch (roundState)
            {
                case RoundState.Playing:
                    return $"Status: {timeRemaining:0.0}s left. Ads left: {adsLeft}. Click the X.";
                case RoundState.Won:
                    return "Status: All ads closed. Click or press R to restart.";
                case RoundState.Lost:
                    return "Status: Too slow. Click or press R to try again.";
                default:
                    return string.Empty;
            }
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider2D currentAd = GetCurrentAd();
            if (currentAd == null)
            {
                for (int i = 0; i < ads.Count; i++)
                {
                    if (ads[i] != null)
                    {
                        currentAd = ads[i];
                        break;
                    }
                }
            }

            if (closeButton == null || currentAd == null)
                return;

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(currentAd.bounds.center, currentAd.bounds.size);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(closeButton.position, clickRadius);
        }
    }
}
