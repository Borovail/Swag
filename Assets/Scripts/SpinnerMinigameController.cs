using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace SpinnerMinigame
{
    [DisallowMultipleComponent]
    public sealed class SpinnerMinigameController : GameController
    {
        [SerializeField] private Transform spinnerTransform;

        [Header("Spin Settings")]
        [SerializeField] private float timeLimit = 5f;
        [SerializeField] private float targetSpeed = 720f;        // deg/sec to win
        [SerializeField] private float speedPerRevolution = 120f; // deg/sec added per revolution
        [SerializeField] private float speedDecay = 60f;          // deg/sec lost per second when not holding

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState
        {
            WaitingToStart,
            Active,
            Won,
            Lost
        }

        private RoundState roundState = RoundState.WaitingToStart;
        private float spinSpeed;
        private float timeElapsed;
        private float accumulatedAngle;
        private float lastMouseAngle;
        private bool mouseAngleValid;
        private int revolutionCount;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;

        private void Awake()
        {
            if (spinnerTransform == null)
            {
                Debug.LogError($"[SpinnerMinigame] spinnerTransform is not assigned on {gameObject.name}.", this);
                return;
            }

            if (!spinnerTransform.IsChildOf(transform))
                Debug.LogWarning($"[SpinnerMinigame] {spinnerTransform.name} is not a child of {gameObject.name}. Move it under the SpinnerMinigame GameObject.", spinnerTransform);
        }

        private void Update()
        {
            HandleInput();

            if (roundState == RoundState.Active)
                UpdateActive();
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool restartPressed = keyboard != null &&
                (keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);

            if ((roundState == RoundState.Won || roundState == RoundState.Lost) && restartPressed)
            {
                if (!autoRestartEnabled)
                    return;

                ResetRound();
                return;
            }

            if (roundState == RoundState.WaitingToStart && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                roundState = RoundState.Active;
                timeElapsed = 0f;
                RecordMouseAngle(mouse);
            }
        }

        private void UpdateActive()
        {
            Mouse mouse = Mouse.current;
            bool holding = mouse != null && mouse.leftButton.isPressed;

            if (holding)
                TrackAnticlockwiseSpin(mouse);
            else
            {
                mouseAngleValid = false;
                spinSpeed = Mathf.Max(0f, spinSpeed - speedDecay * Time.deltaTime);
            }

            if (spinnerTransform != null)
                spinnerTransform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            timeElapsed += Time.deltaTime;

            if (spinSpeed >= targetSpeed)
            {
                roundState = RoundState.Won;
                onSuccess.Invoke();
                ReportRoundFinished(true);
                return;
            }

            if (timeElapsed >= timeLimit)
            {
                roundState = RoundState.Lost;
                onFailure.Invoke();
                ReportRoundFinished(false);
            }
        }

        private void TrackAnticlockwiseSpin(Mouse mouse)
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 delta = (Vector2)mouse.position.ReadValue() - center;

            float currentAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            if (!mouseAngleValid)
            {
                lastMouseAngle = currentAngle;
                mouseAngleValid = true;
                return;
            }

            float angleDelta = Mathf.DeltaAngle(lastMouseAngle, currentAngle);
            lastMouseAngle = currentAngle;

            // positive angleDelta = anti-clockwise in screen space (Atan2 convention)
            if (angleDelta <= 0f)
                return;

            accumulatedAngle += angleDelta;

            int newRevolutions = Mathf.FloorToInt(accumulatedAngle / 360f);
            if (newRevolutions > revolutionCount)
            {
                int gained = newRevolutions - revolutionCount;
                revolutionCount = newRevolutions;
                spinSpeed = Mathf.Min(spinSpeed + speedPerRevolution * gained, targetSpeed);
            }
        }

        private void RecordMouseAngle(Mouse mouse)
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 delta = (Vector2)mouse.position.ReadValue() - center;
            lastMouseAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            mouseAngleValid = true;
        }

        protected override void ResetRound()
        {
            roundState = RoundState.WaitingToStart;
            roundReported = false;
            spinSpeed = 0f;
            timeElapsed = 0f;
            accumulatedAngle = 0f;
            revolutionCount = 0;
            mouseAngleValid = false;

            if (spinnerTransform != null)
                spinnerTransform.rotation = Quaternion.identity;
        }

        private void OnGUI()
        {
            if (!enabled)
                return;

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(430f, Screen.width - 30f);
            Rect panelRect = new Rect(16f, 16f, panelWidth, 112f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 38f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 77f, panelRect.width - 28f, 24f);

            GUI.Label(titleRect, "Spinner Mini-Game", titleStyle);
            GUI.Label(bodyRect, "Hold LMB and move anti-clockwise around the center.\nComplete revolutions before time runs out!", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private string GetStatusText()
        {
            switch (roundState)
            {
                case RoundState.WaitingToStart:
                    return "Status: Hold LMB and start spinning anti-clockwise!";
                case RoundState.Active:
                    float remaining = Mathf.Max(0f, timeLimit - timeElapsed);
                    float progress = spinSpeed / targetSpeed * 100f;
                    return $"Status: Speed {progress:0}%  —  {remaining:0.0}s left  —  {revolutionCount} rev(s)";
                case RoundState.Won:
                    return "Status: Max speed reached! Press R to restart.";
                case RoundState.Lost:
                    return "Status: Too slow! Press R to try again.";
                default:
                    return string.Empty;
            }
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.13f, 0.18f, 0.29f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.2f, 0.24f, 0.32f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.56f, 0.17f, 0.16f) }
            };
        }
    }
}
