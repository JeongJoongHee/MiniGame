using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 캐릭터가 발판에 착지할 때 **발밑에서 풀잎 몇 장이 톡 튀었다가 살랑 떨어지는 연출**입니다 (2026-09-14).
    ///
    /// 파티클 시스템 대신 스프라이트 몇 장을 직접 움직입니다. GameManager.Step 이 굴리므로
    /// 배치 미리보기 캡처에도 그대로 찍히고, 물리 엔진도 쓰지 않습니다 (이 프로젝트의 원칙).
    ///
    /// - 수치(개수 · 크기 · 튀는 세기 · 색 · 수명)는 전부 GameConfig 의 "착지 풀잎 이펙트".
    /// - 그림은 `Art/Effects/Leaf.png`. 흰색 · 회색으로 그려 두고 leafColors 로 물들입니다.
    ///   **덮어쓰기만 하면 되고 해상도는 상관없습니다** — 가로 폭이 leafSize 가 되도록 맞춰 그립니다.
    /// </summary>
    public class LandingLeaves : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] Sprite leafSprite;

        struct Leaf
        {
            public SpriteRenderer renderer;
            public Vector2 position;
            public Vector2 velocity;
            public float angle;
            public float spin;
            public float age;
            public float life;
            public float scale;
            public Color color;
        }

        Leaf[] _leaves = new Leaf[0];

        // 게임의 다른 무작위(캐릭터 고르기)와 섞이지 않게 따로 둡니다.
        readonly System.Random _rng = new System.Random(20260914);

        /// <summary>지금 화면에 떠 있는 풀잎 수 (미리보기 로그용).</summary>
        public int ActiveCount { get; private set; }

        /// <summary>판을 새로 시작할 때 떠 있던 풀잎을 전부 치웁니다.</summary>
        public void Clear()
        {
            for (int i = 0; i < _leaves.Length; i++)
                if (_leaves[i].renderer != null) _leaves[i].renderer.enabled = false;
            ActiveCount = 0;
        }

        /// <summary>발 가운데 x, 발판 윗면 y 에서 풀잎을 튀깁니다. PlayerController.Land 가 부릅니다.</summary>
        public void Burst(float x, float surfaceY)
        {
            if (config == null || leafSprite == null || config.leafCount <= 0) return;
            EnsurePool(config.leafCount * 3);   // 연달아 착지해도 앞 풀잎이 끊기지 않을 만큼

            for (int n = 0; n < config.leafCount; n++)
            {
                int i = FreeSlot();
                var leaf = _leaves[i];

                float side = Range(-1f, 1f);   // 왼쪽(-) ~ 오른쪽(+). 바깥쪽에서 난 잎이 바깥으로 더 멀리 퍼집니다
                leaf.position = new Vector2(x + side * config.leafSpread, surfaceY);
                leaf.velocity = new Vector2((side + Range(-0.35f, 0.35f)) * config.leafSpeedX,
                                            Range(config.leafSpeedYMin, config.leafSpeedYMax));
                leaf.angle = Range(0f, 360f);
                leaf.spin = Range(-1f, 1f) * config.leafSpin;
                leaf.age = 0f;
                leaf.life = Mathf.Max(0.05f, Range(config.leafLifeMin, config.leafLifeMax));
                leaf.scale = Range(0.75f, 1.2f);
                leaf.color = config.leafColors != null && config.leafColors.Length > 0
                    ? config.leafColors[_rng.Next(config.leafColors.Length)]
                    : Color.white;

                leaf.renderer.enabled = true;
                Apply(ref leaf);
                _leaves[i] = leaf;
            }
        }

        /// <summary>GameManager.Step 이 매 프레임 부릅니다 (게임 오버 화면에서도 남은 잎이 마저 떨어지게).</summary>
        public void Tick(float dt)
        {
            int active = 0;
            for (int i = 0; i < _leaves.Length; i++)
            {
                var leaf = _leaves[i];
                if (leaf.renderer == null || !leaf.renderer.enabled) continue;

                leaf.age += dt;
                if (leaf.age >= leaf.life)
                {
                    leaf.renderer.enabled = false;
                    _leaves[i] = leaf;
                    continue;
                }

                leaf.velocity.y -= config.leafGravity * dt;
                leaf.velocity.x *= Mathf.Exp(-config.leafDrag * dt);
                leaf.position += leaf.velocity * dt;
                leaf.angle += leaf.spin * dt;

                Apply(ref leaf);
                _leaves[i] = leaf;
                active++;
            }
            ActiveCount = active;
        }

        void Apply(ref Leaf leaf)
        {
            float t = leaf.age / leaf.life;
            float fadeStart = Mathf.Clamp(config.leafFadeStart, 0f, 0.99f);
            float alpha = t <= fadeStart ? 1f : 1f - (t - fadeStart) / (1f - fadeStart);

            float spriteWidth = Mathf.Max(0.0001f, leafSprite.bounds.size.x);
            float size = config.leafSize / spriteWidth * leaf.scale * Mathf.Lerp(1f, 0.7f, t);   // 사라지며 살짝 작아짐

            var tr = leaf.renderer.transform;
            tr.position = new Vector3(leaf.position.x, leaf.position.y, 0f);
            tr.rotation = Quaternion.Euler(0f, 0f, leaf.angle);
            tr.localScale = new Vector3(size, size, 1f);

            var color = leaf.color;
            color.a *= alpha;
            leaf.renderer.color = color;
        }

        void EnsurePool(int count)
        {
            if (_leaves.Length >= count && _leaves[0].renderer != null) return;

            // 이미 만들어 둔 잎(씬에 남아 있는 것)부터 다시 씁니다.
            var existing = GetComponentsInChildren<SpriteRenderer>(true);
            int total = Mathf.Max(count, existing.Length);
            var pool = new Leaf[total];

            for (int i = 0; i < total; i++)
            {
                SpriteRenderer renderer;
                if (i < existing.Length)
                {
                    renderer = existing[i];
                }
                else
                {
                    var go = new GameObject("Leaf_" + i.ToString("00"));
                    go.transform.SetParent(transform, false);
                    renderer = go.AddComponent<SpriteRenderer>();
                }

                renderer.sprite = leafSprite;
                renderer.sortingOrder = config.leafSortingOrder;
                renderer.enabled = false;
                pool[i] = new Leaf { renderer = renderer };
            }

            _leaves = pool;
        }

        /// <summary>쉬고 있는 잎. 전부 날고 있으면 가장 오래된 잎을 다시 씁니다.</summary>
        int FreeSlot()
        {
            int oldest = 0;
            float oldestT = -1f;
            for (int i = 0; i < _leaves.Length; i++)
            {
                if (!_leaves[i].renderer.enabled) return i;
                float t = _leaves[i].age / Mathf.Max(0.05f, _leaves[i].life);
                if (t > oldestT) { oldestT = t; oldest = i; }
            }
            return oldest;
        }

        float Range(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    }
}
