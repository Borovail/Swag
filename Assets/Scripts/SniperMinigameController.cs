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
        [SerializeField] private string targetsFolder = "Sprites/Targets";
        [SerializeField] private string decoysFolder  = "Sprites/Decoys";

        [Header("Spawn Area")]
        [SerializeField] private Vector2 spawnAreaMin = new Vector2(-7f, -3f);
        [SerializeField] private Vector2 spawnAreaMax = new Vector2(7f, 3f);
        [SerializeField] private float minSpacing = 1.8f;
        [SerializeField] private Vector2 maxSpriteSize = new(1.5f, 1.5f);

        [Header("Rifle")]
        [SerializeField] private Transform rifleTransform;
        [SerializeField] private AudioSource shootAudio;
        [SerializeField] private float recoilSpeed = 800f;
        [SerializeField] private float returnSpeed = 50f;
        [SerializeField] private Vector3 recoilEuler = new(0f, 40f, -30f);

        [Header("Game Settings")]
        [SerializeField] private float timeLimit = 10f;

        [Header("Events")]
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onFailure = new UnityEvent();

        private enum RoundState
        {
            WaitingForShot,
            Won,
            Lost
        }

        private enum RifleState
        {
            Idle,
            Recoiling,
            Returning
        }

        private const int SpawnCount = 7;

        private RoundState roundState = RoundState.WaitingForShot;
        private RifleState rifleState = RifleState.Idle;
        private float timeElapsed;
        private float baseTimeLimit;
        private readonly List<GameObject> spawnedTargets = new();
        private Sprite[] targetSprites;
        private Sprite[] decoySprites;
        private int correctTargetIndex;
        private string correctTargetName;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle statusStyle;

        private void Awake()
        {
            baseTimeLimit = timeLimit;
            targetSprites = LoadSprites(targetsFolder);
            decoySprites  = LoadSprites(decoysFolder);

            if (targetSprites.Length == 0)
                Debug.LogError($"[SniperMinigame] No textures found in Resources/{targetsFolder}", this);

            if (decoySprites.Length == 0)
                Debug.LogError($"[SniperMinigame] No textures found in Resources/{decoysFolder}", this);

            if (shootAudio != null)
            {
                shootAudio.playOnAwake = false;
                shootAudio.Stop();
            }
        }

        private void Start()
        {
            if (spawnedTargets.Count == 0)
                SpawnTargets();
        }

        private void Update()
        {
            HandleInput();
            UpdateRifle();

            if (roundState == RoundState.WaitingForShot)
            {
                timeElapsed += Time.deltaTime;
                if (timeElapsed >= timeLimit)
                {
                    roundState = RoundState.Lost;
                    onFailure.Invoke();
                    ReportRoundFinished(false);
                }
            }
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

            rifleState = RifleState.Recoiling;

            if (shootAudio != null)
                shootAudio.Play();

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

        private void UpdateRifle()
        {
            if (rifleTransform == null || rifleState == RifleState.Idle)
                return;

            Quaternion target = rifleState == RifleState.Recoiling
                ? Quaternion.Euler(recoilEuler)
                : Quaternion.identity;
            float speed = rifleState == RifleState.Recoiling ? recoilSpeed : returnSpeed;

            rifleTransform.localRotation = Quaternion.RotateTowards(
                rifleTransform.localRotation, target, speed * Time.deltaTime);

            if (Quaternion.Angle(rifleTransform.localRotation, target) < 0.01f)
            {
                rifleTransform.localRotation = target;
                rifleState = rifleState == RifleState.Recoiling ? RifleState.Returning : RifleState.Idle;
            }
        }

        private void SpawnTargets()
        {
            if (targetSprites.Length == 0 || decoySprites.Length == 0)
                return;

            Sprite chosenTarget = targetSprites[Random.Range(0, targetSprites.Length)];
            Sprite[] chosenDecoys = PickRandom(decoySprites, SpawnCount - 1);

            correctTargetName = chosenTarget.name;

            Sprite[] allSlots = new Sprite[SpawnCount];
            correctTargetIndex = Random.Range(0, SpawnCount);

            int decoyIdx = 0;
            for (int i = 0; i < SpawnCount; i++)
                allSlots[i] = (i == correctTargetIndex) ? chosenTarget : chosenDecoys[decoyIdx++];

            targetPrefab.SetActive(true);

            List<Vector2> usedPositions = new();

            for (int i = 0; i < SpawnCount; i++)
            {
                Vector2 pos = FindSpawnPosition(usedPositions);
                usedPositions.Add(pos);

                GameObject obj = Instantiate(targetPrefab, pos, Quaternion.identity, transform);
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                sr.sprite = allSlots[i];

                float scale = CalcScale(allSlots[i]);
                obj.transform.localScale = Vector3.one * scale;

                if (obj.TryGetComponent(out CircleCollider2D col))
                {
                    float worldW = allSlots[i].rect.width  / allSlots[i].pixelsPerUnit;
                    float worldH = allSlots[i].rect.height / allSlots[i].pixelsPerUnit;
                    col.radius = Mathf.Max(worldW, worldH) * 0.5f;
                }

                spawnedTargets.Add(obj);
            }

            targetPrefab.SetActive(false);
        }

        private static Sprite[] LoadSprites(string folder)
        {
            Texture2D[] textures = Resources.LoadAll<Texture2D>(folder);
            Sprite[] sprites = new Sprite[textures.Length];

            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D t = textures[i];
                Sprite s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                s.name = t.name;
                sprites[i] = s;
            }

            return sprites;
        }

        private static Sprite[] PickRandom(Sprite[] pool, int count)
        {
            count = Mathf.Min(count, pool.Length);
            List<Sprite> remaining = new(pool);
            Sprite[] result = new Sprite[count];

            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(0, remaining.Count);
                result[i] = remaining[pick];
                remaining.RemoveAt(pick);
            }

            return result;
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

        private float CalcScale(Sprite sprite)
        {
            float worldW = sprite.rect.width / sprite.pixelsPerUnit;
            float worldH = sprite.rect.height / sprite.pixelsPerUnit;
            return Mathf.Min(maxSpriteSize.x / worldW, maxSpriteSize.y / worldH, 1f);
        }

        private bool IsTooClose(Vector2 candidate, List<Vector2> existing)
        {
            foreach (Vector2 p in existing)
                if (Vector2.Distance(candidate, p) < minSpacing)
                    return true;
            return false;
        }

        public override void SetTimeLimit(float seconds) => timeLimit = seconds;
        public override float GetBaseTimeLimit() => baseTimeLimit;

        protected override void ResetRound()
        {
            roundState = RoundState.WaitingForShot;
            rifleState = RifleState.Idle;
            roundReported = false;
            timeElapsed = 0f;

            if (rifleTransform != null)
                rifleTransform.localRotation = Quaternion.identity;

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

            Rect titleRect  = new Rect(panelRect.x + 14f, panelRect.y + 10f, panelRect.width - 28f, 24f);
            Rect bodyRect   = new Rect(panelRect.x + 14f, panelRect.y + 38f, panelRect.width - 28f, 38f);
            Rect statusRect = new Rect(panelRect.x + 14f, panelRect.y + 77f, panelRect.width - 28f, 24f);

            GUI.Label(titleRect, "Sniper Mini-Game", titleStyle);
            GUI.Label(bodyRect, $"Find and shoot: \"{correctTargetName}\".\nShooting the wrong target means failure.", bodyStyle);
            GUI.Label(statusRect, GetStatusText(), statusStyle);
        }

        private string GetStatusText() => roundState switch
        {
            RoundState.WaitingForShot => $"Status: Find \"{correctTargetName}\"!  —  {Mathf.Max(0f, timeLimit - timeElapsed):0.0}s left",
            RoundState.Won            => "Status: Target eliminated! Press R to restart.",
            RoundState.Lost           => "Status: Time's up / wrong target! Press R to try again.",
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
