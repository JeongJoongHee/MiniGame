using UnityEngine;

namespace Archery
{
    /// <summary>화살 한 발이 과녁 높이를 지났을 때의 결과.</summary>
    public struct ArrowResult
    {
        /// <summary>맞았으면 점수(1~5), 빗나갔으면 0.</summary>
        public int points;
        /// <summary>과녁 높이를 지날 때의 화살 x. 명중 표시를 띄울 자리입니다.</summary>
        public float x;
    }

    /// <summary>
    /// 화살을 미리 만들어 두고 돌려 씁니다. (매번 Instantiate 하면 폰에서 끊깁니다)
    ///
    /// 화살은 수직으로만 날아갑니다. 촉(화살 위끝)이 과녁 높이를 **이번 프레임에 통과했는지**를
    /// 수식으로 판정하기 때문에, 프레임이 튀어도 과녁을 뚫고 지나가는 일이 없습니다.
    /// 점프점프의 착지 판정(PlatformManager.FindLanding)과 같은 방식입니다.
    /// </summary>
    public class ArrowPool : MonoBehaviour
    {
        [SerializeField] ArcheryConfig config;
        [SerializeField] SpriteRenderer arrowPrefabRenderer;   // 0번 화살. 이걸 복제해서 풀을 채웁니다

        Transform[] _arrows;
        bool[] _alive;
        bool[] _resolved;   // 과녁 높이를 이미 지나간 화살 (결과가 정해진 화살)

        /// <summary>아직 과녁 높이에 닿지 않은 화살 수. 0 이어야 판이 끝날 수 있습니다.</summary>
        public int PendingCount { get; private set; }

        void EnsurePool()
        {
            int size = Mathf.Max(1, config.arrowPoolSize);
            if (_arrows != null && _arrows.Length == size) return;

            _arrows = new Transform[size];
            _alive = new bool[size];
            _resolved = new bool[size];

            _arrows[0] = arrowPrefabRenderer.transform;
            for (int i = 1; i < size; i++)
            {
                var copy = Instantiate(arrowPrefabRenderer.gameObject, transform);
                copy.name = "Arrow_" + i.ToString("00");
                _arrows[i] = copy.transform;
            }

            for (int i = 0; i < size; i++)
                Hide(i);
        }

        public void ResetRun()
        {
            EnsurePool();
            for (int i = 0; i < _arrows.Length; i++) Hide(i);
            PendingCount = 0;
        }

        /// <summary>x 자리에서 화살 한 발을 쏩니다. 풀이 꽉 찼으면 false.</summary>
        public bool Fire(float x)
        {
            EnsurePool();

            for (int i = 0; i < _arrows.Length; i++)
            {
                if (_alive[i]) continue;

                _alive[i] = true;
                _resolved[i] = false;
                _arrows[i].position = new Vector3(x, config.arrowStartY, 0f);
                _arrows[i].gameObject.SetActive(true);
                PendingCount++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 한 프레임 진행. 과녁 높이를 지난 화살이 있으면 onResolved 로 결과를 알려 줍니다.
        /// targetX / targetRadius 는 이번 프레임의 과녁 상태입니다.
        /// </summary>
        public void Tick(float dt, float speed, float targetX, float targetRadius, System.Action<ArrowResult> onResolved)
        {
            EnsurePool();

            float ceiling = config.targetY + config.arrowRecycleMargin;

            for (int i = 0; i < _arrows.Length; i++)
            {
                if (!_alive[i]) continue;

                var pos = _arrows[i].position;
                float tipBefore = pos.y + config.arrowLength;
                pos.y += speed * dt;
                float tipAfter = pos.y + config.arrowLength;
                _arrows[i].position = pos;

                // 이번 프레임에 촉이 과녁 높이를 통과했는가
                if (!_resolved[i] && tipBefore < config.targetY && tipAfter >= config.targetY)
                {
                    _resolved[i] = true;
                    PendingCount--;

                    int points = config.PointsForOffset(pos.x - targetX, targetRadius);
                    onResolved?.Invoke(new ArrowResult { points = points, x = pos.x });

                    // 맞은 화살은 과녁에 꽂힌 셈 치고 치웁니다. 빗나간 화살은 계속 날아갑니다.
                    if (points > 0) { Hide(i); continue; }
                }

                if (pos.y > ceiling) Hide(i);
            }
        }

        void Hide(int index)
        {
            // 결과가 정해지기 전에 치워지는 화살이 있어도 대기 수가 어긋나지 않게 합니다.
            if (_alive[index] && !_resolved[index]) PendingCount = Mathf.Max(0, PendingCount - 1);

            _alive[index] = false;
            _resolved[index] = false;
            _arrows[index].gameObject.SetActive(false);
            _arrows[index].position = new Vector3(0f, config.arrowStartY - 20f, 0f);
        }
    }
}
