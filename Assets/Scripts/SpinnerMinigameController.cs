using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpinnerMinigame
{
    [DisallowMultipleComponent]
    public sealed class SpinnerMinigameController : GameController
    {
        [SerializeField] private Transform catTransform;
        [SerializeField] private Image progressBar;

        [Header("Spin Settings")]
        [SerializeField] private float timeLimit = 5f;
        [SerializeField] private float targetSpeed = 720f;
        [SerializeField] private float speedPerRevolution = 120f;
        [SerializeField] private float speedDecay = 60f;

        [Header("Audio")]
        [SerializeField] private AudioSource spinAudio;
        [SerializeField] private float minPitch = 0.4f;
        [SerializeField] private float maxPitch = 2f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState
        {
            WaitingToStart,
            Spinning,
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
            if (catTransform == null)
            {
                Debug.LogError($"[SpinnerMinigame] catTransform is not assigned on {gameObject.name}.", this);
                return;
            }

            if (!catTransform.IsChildOf(transform))
                Debug.LogWarning($"[SpinnerMinigame] {catTransform.name} is not a child of {gameObject.name}.", catTransform);

            if (spinAudio != null && !spinAudio.transform.IsChildOf(transform))
                Debug.LogWarning($"[SpinnerMinigame] {spinAudio.name} is not a child of {gameObject.name}.", spinAudio);
        }

        private void OnDisable()
        {
            if (spinAudio != null && spinAudio.isPlaying)
                spinAudio.Stop();
        }

        private void Update()
        {
            HandleInput();

            if (roundState == RoundState.Spinning)
                UpdateSpinning();
            else if (roundState == RoundState.Won || roundState == RoundState.Lost)
                UpdateCoasting();
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
                roundState = RoundState.Spinning;
                timeElapsed = 0f;
                RecordMouseAngle(mouse);

                if (spinAudio != null)
                    spinAudio.Play();
            }
        }

        private void UpdateSpinning()
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

            if (catTransform != null)
                catTransform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

            UpdateAudioPitch();
            UpdateProgressBar();

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

        private void UpdateAudioPitch()
        {
            if (spinAudio == null || !spinAudio.isPlaying)
                return;

            spinAudio.pitch = Mathf.Lerp(minPitch, maxPitch, spinSpeed / targetSpeed);
        }

        private void UpdateProgressBar()
        {
            if (progressBar != null)
                progressBar.fillAmount = Mathf.Clamp01(spinSpeed / targetSpeed);
        }

        private void UpdateCoasting()
        {
            if (catTransform != null)
                catTransform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);

            UpdateAudioPitch();
            UpdateProgressBar();
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

            if (spinAudio != null && spinAudio.isPlaying)
                spinAudio.Stop();

            if (catTransform != null)
                catTransform.localRotation = Quaternion.identity;
        }

        private void OnGUI()
        {
            if (!enabled)
                return;

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(430f, Screen.width - 30f);
            Rect panelRect = new Rect(16f, 16f, panelWidth, 90f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect  = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect   = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 24f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 62f, panelRect.width - 28f, 20f);

            GUI.Label(titleRect, "Spinner Mini-Game", titleStyle);
            GUI.Label(bodyRect, "Hold LMB and move anti-clockwise to spin the cat.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }


        private string GetStatusText()
        {
            switch (roundState)
            {
                case RoundState.WaitingToStart:
                    return "Status: Hold LMB and start spinning anti-clockwise!";
                case RoundState.Spinning:
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
