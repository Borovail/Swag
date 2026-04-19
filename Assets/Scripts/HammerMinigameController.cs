using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace HammerMinigame
{
    [DisallowMultipleComponent]
    public sealed class HammerMinigameController : GameController
    {
        [Header("Hammer")]
        [SerializeField] private Transform hammerTransform;
        [SerializeField] private float hammerLocalY = 2f;

        [Header("Targets")]
        [SerializeField] private GameObject targetPrefab;
        [SerializeField] private float spawnLocalY = -5f;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float moveDistance = 5f;
        [SerializeField] private float appearanceInterval = 1.5f;
        [SerializeField] private Vector2 spawnXRange = new(-6f, 6f);

        [Header("Strike Settings")]
        [SerializeField] private float strikeDistance = 2f;
        [SerializeField] private float strikeSpeed = 15f;
        [SerializeField] private float returnSpeed = 8f;

        [Header("Audio")]
        [SerializeField] private AudioSource hitAudio;

        [Header("Game Settings")]
        [SerializeField] private int hitsRequired = 5;
        [SerializeField] private float timeLimit = 15f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState
        {
            Active,
            Won,
            Lost
        }

        private enum HammerState
        {
            Idle,
            Striking,
            Returning
        }

        private sealed class ActiveTarget
        {
            public GameObject Object;
            public bool MovingUp = true;
            public float PeakLocalY;
            public float BaseLocalY;
        }

        private float baseMoveSpeed;
        private RoundState roundState = RoundState.Active;
        private HammerState hammerState = HammerState.Idle;
        private readonly List<ActiveTarget> targets = new();
        private int hitCount;
        private float timeElapsed;
        private float spawnTimer;
        private float hammerRestLocalY;
        private float strikeTargetLocalY;
        private bool hitRegisteredThisStrike;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;

        private void Awake()
        {
            baseMoveSpeed = moveSpeed;
            if (hammerTransform == null)
            {
                Debug.LogError($"[HammerMinigame] hammerTransform is not assigned on {gameObject.name}.", this);
                return;
            }

            if (!hammerTransform.IsChildOf(transform))
                Debug.LogWarning($"[HammerMinigame] {hammerTransform.name} is not a child of {gameObject.name}.", hammerTransform);

            if (targetPrefab != null && !targetPrefab.transform.IsChildOf(transform))
                Debug.LogWarning($"[HammerMinigame] targetPrefab {targetPrefab.name} is not a child of {gameObject.name}.", targetPrefab);

            hammerRestLocalY = hammerLocalY;

            Vector3 lp = hammerTransform.localPosition;
            lp.y = hammerRestLocalY;
            hammerTransform.localPosition = lp;

            if (hitAudio != null)
            {
                hitAudio.playOnAwake = false;
                hitAudio.Stop();
            }
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

            if (roundState != RoundState.Active || mouse == null || hammerTransform == null)
                return;

            TrackMouseX(mouse);

            if (mouse.leftButton.wasPressedThisFrame && hammerState == HammerState.Idle)
            {
                hammerState = HammerState.Striking;
                hitRegisteredThisStrike = false;
                strikeTargetLocalY = hammerRestLocalY - strikeDistance;
            }
        }

        private void TrackMouseX(Mouse mouse)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            Vector3 worldPos = hammerTransform.position;
            worldPos.x = mouseWorld.x;
            hammerTransform.position = worldPos;
        }

        private void UpdateActive()
        {
            UpdateHammerAnimation();
            UpdateTargets();
            UpdateSpawnTimer();

            timeElapsed += Time.deltaTime;

            if (timeElapsed >= timeLimit)
            {
                roundState = RoundState.Lost;
                onFailure.Invoke();
                ReportRoundFinished(false);
            }
        }

        private void UpdateHammerAnimation()
        {
            if (hammerTransform == null)
                return;

            Vector3 localPos = hammerTransform.localPosition;

            switch (hammerState)
            {
                case HammerState.Striking:
                    localPos.y = Mathf.MoveTowards(localPos.y, strikeTargetLocalY, strikeSpeed * Time.deltaTime);
                    hammerTransform.localPosition = localPos;

                    if (!hitRegisteredThisStrike && Mathf.Approximately(localPos.y, strikeTargetLocalY))
                    {
                        hitRegisteredThisStrike = true;
                        TryRegisterHit();
                    }

                    if (Mathf.Approximately(localPos.y, strikeTargetLocalY))
                        hammerState = HammerState.Returning;

                    break;

                case HammerState.Returning:
                    localPos.y = Mathf.MoveTowards(localPos.y, hammerRestLocalY, returnSpeed * Time.deltaTime);
                    hammerTransform.localPosition = localPos;

                    if (Mathf.Approximately(localPos.y, hammerRestLocalY))
                        hammerState = HammerState.Idle;

                    break;
            }
        }

        private void UpdateSpawnTimer()
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = appearanceInterval;
                SpawnTarget();
            }
        }

        private void SpawnTarget()
        {
            if (targetPrefab == null)
                return;

            targetPrefab.SetActive(true);

            float randomX = Random.Range(spawnXRange.x, spawnXRange.y);
            GameObject obj = Instantiate(targetPrefab, transform);
            obj.transform.localPosition = new Vector3(randomX, spawnLocalY, 0f);

            targetPrefab.SetActive(false);

            targets.Add(new ActiveTarget
            {
                Object = obj,
                MovingUp = true,
                BaseLocalY = spawnLocalY,
                PeakLocalY = spawnLocalY + moveDistance
            });
        }

        private void UpdateTargets()
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                ActiveTarget t = targets[i];

                if (t.Object == null)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                Vector3 lp = t.Object.transform.localPosition;

                if (t.MovingUp)
                {
                    lp.y = Mathf.MoveTowards(lp.y, t.PeakLocalY, moveSpeed * Time.deltaTime);
                    t.Object.transform.localPosition = lp;

                    if (Mathf.Approximately(lp.y, t.PeakLocalY))
                        t.MovingUp = false;
                }
                else
                {
                    lp.y = Mathf.MoveTowards(lp.y, t.BaseLocalY, moveSpeed * 2f * Time.deltaTime);
                    t.Object.transform.localPosition = lp;

                    if (Mathf.Approximately(lp.y, t.BaseLocalY))
                    {
                        Destroy(t.Object);
                        targets.RemoveAt(i);
                    }
                }
            }
        }

        private void TryRegisterHit()
        {
            if (hammerTransform == null)
                return;

            Vector2 hammerTip = hammerTransform.position;
            Collider2D[] hits = Physics2D.OverlapPointAll(hammerTip);

            foreach (Collider2D col in hits)
            {
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    if (targets[i].Object != col.gameObject)
                        continue;

                    Destroy(targets[i].Object);
                    targets.RemoveAt(i);
                    hitCount++;

                    if (hitAudio != null)
                        hitAudio.Play();

                    if (hitCount >= hitsRequired)
                    {
                        roundState = RoundState.Won;
                        onSuccess.Invoke();
                        ReportRoundFinished(true);
                    }

                    return;
                }
            }
        }

        public override ControlScheme RequiredControls => ControlScheme.Mouse;
        public override string ControlDescription => "Smash Tung Tung Tung Sahur";

        public override void ApplyDifficulty(Difficulty difficulty)
        {
            float multiplier = difficulty switch
            {
                Difficulty.Easy   => 1f,
                Difficulty.Medium => 1.35f,
                Difficulty.Hard   => 1.8f,
                Difficulty.Insane => 2.4f,
                _                 => 1f
            };

            moveSpeed = baseMoveSpeed * multiplier;
        }

        protected override void ResetRound()
        {
            roundState = RoundState.Active;
            hammerState = HammerState.Idle;
            roundReported = false;
            hitCount = 0;
            timeElapsed = 0f;
            spawnTimer = 0f;
            hitRegisteredThisStrike = false;

            foreach (ActiveTarget t in targets)
                if (t.Object != null)
                    Destroy(t.Object);

            targets.Clear();

            if (hammerTransform != null)
            {
                Vector3 lp = hammerTransform.localPosition;
                lp.y = hammerRestLocalY;
                hammerTransform.localPosition = lp;
            }
        }

        // private void OnGUI()
        // {
        //     if (!enabled)
        //         return;

        //     EnsureGuiStyles();

        //     float panelWidth = Mathf.Min(430f, Screen.width - 30f);
        //     Rect panelRect = new Rect(16f, 16f, panelWidth, 112f);
        //     GUI.Box(panelRect, GUIContent.none);

        //     Rect titleRect = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
        //     Rect bodyRect = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 38f);
        //     Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 77f, panelRect.width - 28f, 24f);

        //     GUI.Label(titleRect, "Hammer Mini-Game", titleStyle);
        //     GUI.Label(bodyRect, "Move mouse to aim. Click to strike the rising targets.\nHit enough before time runs out!", bodyStyle);
        //     GUI.Label(statusRect, GetStatusText(), statusStyle);
        // }

        private string GetStatusText()
        {
            switch (roundState)
            {
                case RoundState.Active:
                    float remaining = Mathf.Max(0f, timeLimit - timeElapsed);
                    return $"Status: Hits {hitCount}/{hitsRequired}  —  {remaining:0.0}s left";
                case RoundState.Won:
                    return "Status: All targets smashed! Press R to restart.";
                case RoundState.Lost:
                    return "Status: Time's up! Press R to try again.";
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
