using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class SlotMachineMinigameController : GameController
    {
        [SerializeField] private Transform spinButton;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform[] heartSymbols;
        [SerializeField] private Transform[] skullSymbols;

        [Header("Spin")]
        [SerializeField] private float[] reelStopTimes = { 1.45f, 2.2f, 3f };
        [SerializeField] private float minimumShuffleInterval = 0.045f;
        [SerializeField] private float maximumShuffleInterval = 0.22f;
        [SerializeField] private float buttonIdleScale = 1f;
        [SerializeField] private float buttonPressedScale = 0.86f;
        [SerializeField] private float buttonScaleSpeed = 6.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onHeartJackpot = new UnityEvent();
        [SerializeField] private UnityEvent onSkullJackpot = new UnityEvent();

        private enum SymbolType
        {
            Heart,
            Skull
        }

        private enum RoundState
        {
            Ready,
            Spinning,
            Resolved
        }

        private RoundState roundState = RoundState.Ready;
        private readonly SymbolType[] currentSymbols = new SymbolType[3];
        private readonly SymbolType[] finalSymbols = new SymbolType[3];
        private readonly bool[] reelLocked = new bool[3];
        private float spinTimer;
        private float symbolSwapTimer;
        private string lastResult = "Press the button and hope for a line.";
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;
        private bool isConfigured;
        private Vector3 buttonBaseScale;

        public int LastHealthDelta { get; private set; }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            CacheState();
            ResetRound();
        }

        private void Update()
        {
            HandleInput();
            UpdateButtonVisual();

            if (roundState != RoundState.Spinning)
            {
                return;
            }

            spinTimer += Time.deltaTime;
            symbolSwapTimer += Time.deltaTime;

            float spinDuration = reelStopTimes[reelStopTimes.Length - 1];
            float spinProgress = Mathf.Clamp01(spinTimer / Mathf.Max(0.01f, spinDuration));
            float slowedProgress = spinProgress * spinProgress;
            float currentShuffleInterval = Mathf.Lerp(minimumShuffleInterval, maximumShuffleInterval, slowedProgress);

            if (symbolSwapTimer >= currentShuffleInterval)
            {
                symbolSwapTimer = 0f;
                ShuffleVisibleSymbols();
            }

            for (int i = 0; i < reelLocked.Length; i++)
            {
                if (reelLocked[i] || spinTimer < reelStopTimes[i])
                {
                    continue;
                }

                reelLocked[i] = true;
                SetSymbol(i, finalSymbols[i]);
            }

            if (spinTimer >= reelStopTimes[reelStopTimes.Length - 1] + 0.12f)
            {
                FinishSpin();
            }
        }

        private void OnGUI()
        {
            if (!enabled)
            {
                return;
            }

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(430f, Screen.width - 30f);
            Rect panelRect = new Rect(16f, 16f, panelWidth, 120f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 38f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 80f, panelRect.width - 28f, 24f);

            GUI.Label(titleRect, "Lucky Doom Slots", titleStyle);
            GUI.Label(bodyRect, "Click the button or press Space.\n3 hearts = heal, 3 skulls = lose HP, mixed = nothing.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool actionPressed = keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
            bool pointerPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool buttonClicked = pointerPressed && IsSpinButtonClicked(mouse);

            if (roundState == RoundState.Spinning)
            {
                return;
            }

            if (roundState == RoundState.Ready)
            {
                if (actionPressed || buttonClicked)
                {
                    StartSpin();
                }

                return;
            }

            if (!autoRestartEnabled)
            {
                return;
            }

            if (actionPressed || buttonClicked)
            {
                ResetRound();
            }
        }

        private bool IsSpinButtonClicked(Mouse mouse)
        {
            if (mouse == null || spinButton == null)
            {
                return false;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (targetCamera == null)
            {
                return false;
            }

            Vector2 cursorScreen = mouse.position.ReadValue();
            Vector3 cursorWorld = targetCamera.ScreenToWorldPoint(new Vector3(cursorScreen.x, cursorScreen.y, Mathf.Abs(targetCamera.transform.position.z)));
            cursorWorld.z = spinButton.position.z;
            return Vector2.Distance(cursorWorld, spinButton.position) <= 0.9f;
        }

        private void StartSpin()
        {
            roundState = RoundState.Spinning;
            roundReported = false;
            LastHealthDelta = 0;
            spinTimer = 0f;
            symbolSwapTimer = 0f;
            lastResult = "Spinning...";

            for (int i = 0; i < reelLocked.Length; i++)
            {
                reelLocked[i] = false;
                finalSymbols[i] = Random.value < 0.5f ? SymbolType.Heart : SymbolType.Skull;
                currentSymbols[i] = Random.value < 0.5f ? SymbolType.Heart : SymbolType.Skull;
                SetSymbol(i, currentSymbols[i]);
            }
        }

        private void ShuffleVisibleSymbols()
        {
            for (int i = 0; i < currentSymbols.Length; i++)
            {
                if (reelLocked[i])
                {
                    continue;
                }

                currentSymbols[i] = Random.value < 0.5f ? SymbolType.Heart : SymbolType.Skull;
                SetSymbol(i, currentSymbols[i]);
            }
        }

        private void FinishSpin()
        {
            roundState = RoundState.Resolved;

            bool allHearts = finalSymbols[0] == SymbolType.Heart && finalSymbols[1] == SymbolType.Heart && finalSymbols[2] == SymbolType.Heart;
            bool allSkulls = finalSymbols[0] == SymbolType.Skull && finalSymbols[1] == SymbolType.Skull && finalSymbols[2] == SymbolType.Skull;

            if (allHearts)
            {
                LastHealthDelta = 1;
                lastResult = "HEART JACKPOT! HP +1";
                onHeartJackpot.Invoke();
                ReportRoundFinished(true);
                return;
            }

            if (allSkulls)
            {
                LastHealthDelta = -1;
                lastResult = "SKULL JACKPOT! HP -1";
                onSkullJackpot.Invoke();
                ReportRoundFinished(false);
                return;
            }

            LastHealthDelta = 0;
            lastResult = "No line. HP stays the same.";
            ReportRoundFinished(true);
        }

        protected override void ResetRound()
        {
            roundState = RoundState.Ready;
            roundReported = false;
            LastHealthDelta = 0;
            spinTimer = 0f;
            symbolSwapTimer = 0f;
            lastResult = "Press the button and hope for a line.";

            for (int i = 0; i < currentSymbols.Length; i++)
            {
                currentSymbols[i] = i % 2 == 0 ? SymbolType.Heart : SymbolType.Skull;
                reelLocked[i] = false;
                SetSymbol(i, currentSymbols[i]);
            }
        }

        private void UpdateButtonVisual()
        {
            if (spinButton == null)
            {
                return;
            }

            float pressedWindow = 0.22f;
            bool showPressedPose = roundState == RoundState.Spinning && spinTimer <= pressedWindow;
            float targetScaleMultiplier = showPressedPose ? buttonPressedScale : buttonIdleScale;
            Vector3 targetScale = buttonBaseScale * targetScaleMultiplier;
            spinButton.localScale = Vector3.Lerp(spinButton.localScale, targetScale, buttonScaleSpeed * Time.deltaTime);
        }

        private void SetSymbol(int reelIndex, SymbolType symbol)
        {
            if (reelIndex < 0 || reelIndex >= heartSymbols.Length || reelIndex >= skullSymbols.Length)
            {
                return;
            }

            if (heartSymbols[reelIndex] != null)
            {
                heartSymbols[reelIndex].gameObject.SetActive(symbol == SymbolType.Heart);
            }

            if (skullSymbols[reelIndex] != null)
            {
                skullSymbols[reelIndex].gameObject.SetActive(symbol == SymbolType.Skull);
            }
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
                normal = { textColor = new Color(0.25f, 0.09f, 0.15f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.22f, 0.19f, 0.23f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.56f, 0.17f, 0.16f) }
            };
        }

        private string GetStatusText()
        {
            switch (roundState)
            {
                case RoundState.Ready:
                    return "Status: Click the button or press Space.";
                case RoundState.Spinning:
                    return "Status: Reels are spinning and slowing down...";
                case RoundState.Resolved:
                    return $"{lastResult} Press again to restart.";
                default:
                    return string.Empty;
            }
        }

        private void CacheState()
        {
            if (spinButton != null && buttonBaseScale == Vector3.zero)
            {
                buttonBaseScale = spinButton.localScale;
            }

            isConfigured =
                spinButton != null &&
                heartSymbols != null &&
                skullSymbols != null &&
                heartSymbols.Length == 3 &&
                skullSymbols.Length == 3 &&
                heartSymbols[0] != null &&
                heartSymbols[1] != null &&
                heartSymbols[2] != null &&
                skullSymbols[0] != null &&
                skullSymbols[1] != null &&
                skullSymbols[2] != null;
        }
    }
}
