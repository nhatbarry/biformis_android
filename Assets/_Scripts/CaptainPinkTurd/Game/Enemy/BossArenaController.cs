using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Core.Rendering;
using CaptainPinkTurd.Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>One open, fixed arena frame. Movement stops at its playable edge without wall geometry.</summary>
    [DefaultExecutionOrder(9000)]
    public class BossArenaController : MonoBehaviour
    {
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private PlagueDoctorBoss boss;
        [SerializeField] private Vector2 cameraCentre;
        [SerializeField] private Vector2 playAreaCentre;
        [SerializeField] private Vector2 playAreaSize = new(18f, 7f);
        [SerializeField] private float combatSize = 6.5f;
        [SerializeField] private float closeUpSize = 3f;
        [SerializeField] private float zoomSeconds = 0.65f;
        [SerializeField] private float revealHoldSeconds = 0.35f;

        private PlayerUnit player;
        private CameraFraming framing;
        private Vector2 viewCentre;
        private float viewSize;
        private RigidbodyConstraints2D playerConstraints;
        private Vector2 playerPosition;
        private readonly List<(Canvas canvas, GraphicRaycaster raycaster, bool raycasts)> hiddenUi = new();
        public bool IsPresentingPhaseTwo { get; private set; }
        public float CurrentAuthoredSize => viewSize;

        public Rect PlayArea
        {
            get
            {
                float aspect = arenaCamera ? arenaCamera.aspect : 16f / 9f;
                float halfHeight = Mathf.Min(combatSize, combatSize * (16f / 9f) / aspect);
                var view = new Rect(cameraCentre - new Vector2(halfHeight * aspect, halfHeight),
                    new Vector2(halfHeight * aspect * 2f, halfHeight * 2f));
                var intended = new Rect(playAreaCentre - playAreaSize * 0.5f, playAreaSize);
                return Rect.MinMaxRect(Mathf.Max(intended.xMin, view.xMin + 0.5f),
                    Mathf.Max(intended.yMin, view.yMin + 0.5f),
                    Mathf.Min(intended.xMax, view.xMax - 0.5f),
                    Mathf.Min(intended.yMax, view.yMax - 0.5f));
            }
        }

        private void Awake()
        {
            if (!arenaCamera) arenaCamera = Camera.main;
            if (!boss) boss = FindAnyObjectByType<PlagueDoctorBoss>();
            player = FindAnyObjectByType<PlayerUnit>();
            framing = FindAnyObjectByType<CameraFraming>();
            viewCentre = cameraCentre;
            viewSize = combatSize;
        }

        private void LateUpdate()
        {
            if (arenaCamera)
            {
                arenaCamera.transform.position = new Vector3(viewCentre.x, viewCentre.y, -10f);
                if (!framing) framing = FindAnyObjectByType<CameraFraming>();
                if (framing) framing.SetAuthoredSize(arenaCamera, viewSize);
                else arenaCamera.orthographicSize = viewSize;
            }
            if (player && player.gameObject.activeInHierarchy)
            {
                if (IsPresentingPhaseTwo)
                {
                    player.rb.position = playerPosition;
                    player.rb.linearVelocity = Vector2.zero;
                }
                else KeepInside(player.transform, player.rb, 0.2f);
            }
            if (!boss) return;
            KeepInside(boss.transform, boss.GetComponent<Rigidbody2D>(), 0.55f);
            foreach (var enemy in boss.SummonedEnemies)
                if (enemy && enemy.activeInHierarchy) KeepInside(enemy.transform, enemy.GetComponent<Rigidbody2D>(), 0.6f);
        }

        public Vector2 ClampPosition(Vector2 position, float margin = 0.6f)
        {
            var area = PlayArea;
            return new Vector2(Mathf.Clamp(position.x, area.xMin + margin, area.xMax - margin),
                Mathf.Clamp(position.y, area.yMin + margin, area.yMax - margin));
        }

        private void KeepInside(Transform actor, Rigidbody2D body, float margin)
        {
            Vector2 before = actor.position;
            Vector2 after = ClampPosition(before, margin);
            if ((before - after).sqrMagnitude < 0.000001f) return;
            if (body)
            {
                body.position = after;
                var velocity = body.linearVelocity;
                if (Mathf.Abs(before.x - after.x) > 0.0001f) velocity.x = 0f;
                if (Mathf.Abs(before.y - after.y) > 0.0001f) velocity.y = 0f;
                body.linearVelocity = velocity;
            }
            else actor.position = new Vector3(after.x, after.y, actor.position.z);
        }

        public IEnumerator ZoomInOnBoss()
        {
            if (!isActiveAndEnabled) yield break;
            BeginPhaseTwoPresentation();
            yield return Zoom((Vector2)boss.transform.position + Vector2.up * 1.7f, closeUpSize);
        }

        public void BeginPhaseTwoPresentation()
        {
            if (!isActiveAndEnabled) return;
            if (!IsPresentingPhaseTwo)
            {
                // Hide rendering and touches, keeping the virtual gamepad and its held controls alive.
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.enabled) continue;
                    var raycaster = canvas.GetComponent<GraphicRaycaster>();
                    hiddenUi.Add((canvas, raycaster, raycaster && raycaster.enabled));
                    canvas.enabled = false;
                    if (raycaster) raycaster.enabled = false;
                }
            }
            if (player && !IsPresentingPhaseTwo)
            {
                playerPosition = player.rb.position;
                playerConstraints = player.rb.constraints;
                player.rb.constraints |= RigidbodyConstraints2D.FreezePosition;
                player.rb.linearVelocity = Vector2.zero;
            }
            IsPresentingPhaseTwo = true;
        }

        public IEnumerator ZoomOutFromBoss()
        {
            if (!isActiveAndEnabled) { RestorePlayer(); yield break; }
            yield return new WaitForSeconds(revealHoldSeconds);
            yield return Zoom(cameraCentre, combatSize);
            RestorePlayer();
        }

        private IEnumerator Zoom(Vector2 target, float size)
        {
            Vector2 origin = viewCentre;
            float originalSize = viewSize;
            for (float elapsed = 0; elapsed < zoomSeconds && isActiveAndEnabled; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / zoomSeconds);
                viewCentre = Vector2.Lerp(origin, target, t);
                viewSize = Mathf.Lerp(originalSize, size, t);
                yield return null;
            }
            viewCentre = target;
            viewSize = size;
        }

        private void RestorePlayer()
        {
            foreach (var (canvas, raycaster, raycasts) in hiddenUi)
            {
                if (canvas) canvas.enabled = true;
                if (raycaster) raycaster.enabled = raycasts;
            }
            hiddenUi.Clear();
            if (IsPresentingPhaseTwo && player)
            {
                player.rb.constraints = playerConstraints;
                player.rb.linearVelocity = Vector2.zero;
            }
            IsPresentingPhaseTwo = false;
        }

        private void OnDisable() => RestorePlayer();
    }
}
