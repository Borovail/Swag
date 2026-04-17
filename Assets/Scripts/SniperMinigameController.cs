using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace SniperMinigame
{
    [DisallowMultipleComponent]
    public sealed class SniperMinigameController : GameController
    {
        [Header("Targets")]
        [SerializeField] private GameObject targetPrefab;
        [SerializeField] private Sprite[] decoySprites;
        [SerializeField] private Sprite correctSprite;
        [SerializeField] private string correctTargetName = "Target";
        [SerializeField] private int targetCount = 6;

        [Header("Spawn Area")]
        [SerializeField] private Vector2 spawnAreaMin = new Vector2(-7f, -3f);
        [SerializeField] private Vector2 spawnAreaMax = new Vector2(7f, 3f);
        [SerializeField] private float minSpacing = 1.8f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState { 
            WaitingForShot, 
            Won, 
            Lost 
        }

        private RoundState roundState = RoundState.WaitingForShot;
        private readonly List<GameObject> spawnedTargets = new List<GameObject>();
        private int correctTargetIndex;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;

        private void Start()
        {
            if (spawnedTargets.Count == 0)
                SpawnTargets();
        }

        private void Update() => HandleInput();

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

            if (roundState != RoundState.WaitingForShot || mouse == null)
                return;

            if (!mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 worldPoint = Camera.main.ScreenToWorldPoint(mouse.position.ReadValue());
            Collider2D hit = Physics2D.OverlapPoint(worldPoint);

            if (hit == null)
                return;

            int index = spawnedTargets.IndexOf(hit.gameObject);
            if (index < 0)
                return;

            if (index == correctTargetIndex)
            {
                roundState = RoundState.Won;
                onSuccess.Invoke();
                ReportRoundFinished(true);
            }
            else
            {
                roundState = RoundState.Lost;
                onFailure.Invoke();
                ReportRoundFinished(false);
            }
        }

        private void SpawnTargets()
        {
            targetPrefab.SetActive(true);
            correctTargetIndex = Random.Range(0, targetCount);

            List<Vector2> usedPositions = new List<Vector2>();

            for (int i = 0; i < targetCount; i++)
            {
                Vector2 pos = FindSpawnPosition(usedPositions);
                usedPositions.Add(pos);

                GameObject obj = Instantiate(targetPrefab, pos, Quaternion.identity, transform);
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();

                sr.sprite = (i == correctTargetIndex)
                    ? correctSprite
                    : PickDecoySprite(i);

                spawnedTargets.Add(obj);
            }

            targetPrefab.SetActive(false);
        }

        private Sprite PickDecoySprite(int targetIndex)
        {
            if (decoySprites == null || decoySprites.Length == 0)
                return correctSprite;

            // avoid accidentally assigning the same sprite as the correct one by index rotation
            return decoySprites[targetIndex % decoySprites.Length];
        }

        private Vector2 FindSpawnPosition(List<Vector2> existing)
        {
            Vector2 candidate;
            int attempts = 0;

            do
            {
                candidate = new Vector2(
                    Random.Range(spawnAreaMin.x, spawnAreaMax.x),
                    Random.Range(spawnAreaMin.y, spawnAreaMax.y));
                attempts++;
            }
            while (IsTooClose(candidate, existing) && attempts < 100);

            return candidate;
        }

        private bool IsTooClose(Vector2 candidate, List<Vector2> existing)
        {
            foreach (Vector2 p in existing)
                if (Vector2.Distance(candidate, p) < minSpacing)
                    return true;
            return false;
        }

        protected override void ResetRound()
        {
            roundState = RoundState.WaitingForShot;
            roundReported = false;

            foreach (GameObject obj in spawnedTargets)
                Destroy(obj);

            spawnedTargets.Clear();
            SpawnTargets();
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

            GUI.Label(titleRect, "Sniper Mini-Game", titleStyle);
            GUI.Label(bodyRect, $"Find and click: \"{correctTargetName}\".\nShooting the wrong target means failure.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private string GetStatusText() => roundState switch
        {
            RoundState.WaitingForShot => $"Status: Identify and shoot \"{correctTargetName}\"!",
            RoundState.Won            => "Status: Target eliminated! Press R to restart.",
            RoundState.Lost           => "Status: Wrong target! Press R to try again.",
            _                         => string.Empty
        };

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
