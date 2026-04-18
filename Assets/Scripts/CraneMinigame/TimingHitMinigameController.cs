using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System;
using Random = UnityEngine.Random;

namespace CraneMinigame
{
    [DisallowMultipleComponent]
    public sealed class TimingHitMinigameController : GameController
    {

        [SerializeField] private Transform indicatorPivot;

        [Header("Gameplay")]
        [SerializeField] private float rotationSpeed = 230f;
        [SerializeField] private float targetAngle = 0f;
        [SerializeField] private float perfectWindow = 8f;
        [SerializeField] private float goodWindow = 22f;
        [SerializeField] private int totalAttempts = 5;
        [SerializeField] private int targetScore = 8;

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
        private float currentAngle;
        private int currentScore;
        private int attemptsUsed;
        private string lastResult = "Press Space to hit the timing window.";
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;

        private void Update()
        {
            HandleInput();

            if (roundState != RoundState.Playing)
                return;

            currentAngle += rotationSpeed * Time.deltaTime;
            ApplyIndicatorRotation();
        }

        private void OnGUI()
        {
            if (!enabled)
                return;

            EnsureGuiStyles();

            float panelWidth = Mathf.Min(430f, Screen.width - 30f);
            Rect panelRect = new Rect(16f, Screen.height - 136f, panelWidth, 120f);
            GUI.Box(panelRect, GUIContent.none);

            Rect titleRect = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 38f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 80f, panelRect.width - 28f, 26f);

            GUI.Label(titleRect, "Quick Timing Tap", titleStyle);
            GUI.Label(bodyRect, "Press Space when the hand crosses the hit zone.\nPERFECT = 3, GOOD = 1, MISS = 0.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            bool actionPressed = keyboard.spaceKey.wasPressedThisFrame;
            bool restartPressed = keyboard.rKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;

            if (roundState == RoundState.Playing)
            {
                if (!actionPressed)
                    return;

                RegisterHit();
                return;
            }

            if (!autoRestartEnabled)
                return;

            if (actionPressed || restartPressed)
                ResetRound();
        }

        private void RegisterHit()
        {
            attemptsUsed++;

            float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle));
            if (delta <= perfectWindow)
            {
                currentScore += 3;
                lastResult = "PERFECT! +3";
            }
            else if (delta <= goodWindow)
            {
                currentScore += 1;
                lastResult = "GOOD! +1";
            }
            else
                lastResult = "MISS! +0";

            JitterRotationPattern();

            if (attemptsUsed >= totalAttempts)
                FinishRound();
        }

        private void JitterRotationPattern()
        {
            float nextSpeed = Random.Range(180f, 320f);
            float direction = Random.value < 0.35f ? -1f : 1f;
            rotationSpeed = nextSpeed * direction;
        }

        private void FinishRound()
        {
            if (currentScore >= targetScore)
            {
                roundState = RoundState.Won;
                lastResult = $"Victory! Final score: {currentScore}";
                onSuccess.Invoke();
                ReportRoundFinished(true);
            }
            else
            {
                roundState = RoundState.Lost;
                lastResult = $"Failed. Final score: {currentScore}";
                onFailure.Invoke();
                ReportRoundFinished(false);
            }
        }

        protected override void ResetRound()
        {
            roundState = RoundState.Playing;
            currentScore = 0;
            attemptsUsed = 0;
            currentAngle = Random.Range(0f, 360f);
            rotationSpeed = Random.Range(190f, 280f) * (Random.value < 0.5f ? -1f : 1f);
            lastResult = "Press Space to hit the timing window.";
            roundReported = false;
            ApplyIndicatorRotation();
        }

        private void ApplyIndicatorRotation()
        {
            indicatorPivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
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
                normal = { textColor = new Color(0.14f, 0.12f, 0.3f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.19f, 0.18f, 0.28f) }
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.12f, 0.34f, 0.28f) }
            };
        }

        private string GetStatusText()
        {
            if (roundState == RoundState.Playing)
            {
                return $"Score {currentScore}/{targetScore} | Attempt {attemptsUsed + 1}/{totalAttempts} | {lastResult}";
            }

            if (roundState == RoundState.Won)
            {
                return $"{lastResult} Press Space or R to restart.";
            }

            return $"{lastResult} Press Space or R to retry.";
        }
    }
}
