using UnityEngine;

namespace Archery
{
    /// <summary>
    /// 좌우로 오가는 과녁입니다.
    ///
    /// - 물리 엔진을 쓰지 않습니다. x 좌표를 직접 적분하고, 벽에 닿으면 반사시킵니다.
    /// - **명중 판정은 x 거리 하나로 끝납니다.** 화살이 수직으로만 날기 때문에,
    ///   화살 촉이 과녁 높이를 지나는 순간의 |화살x - 과녁x| 만 보면 어느 링인지 정해집니다.
    /// - 그림은 납작한 타원처럼 보이지만(targetFlatten) 판정에는 영향이 없습니다.
    /// </summary>
    public class TargetController : MonoBehaviour
    {
        [SerializeField] ArcheryConfig config;
        [SerializeField] SpriteRenderer spriteRenderer;

        float _x;
        int _direction = 1;
        float _punch;      // 명중했을 때 잠깐 커졌다가 돌아오는 연출값

        /// <summary>지금 과녁 크기(%). 100 에서 시작해 맞힐 때마다 줄어듭니다.</summary>
        public float SizePercent { get; private set; } = 100f;

        /// <summary>지금 과녁의 반지름(월드 단위). 명중 판정에 쓰는 값입니다.</summary>
        public float Radius => config.targetRadius * SizePercent * 0.01f;

        public float X => _x;

        /// <summary>지금 가고 있는 방향. +1 = 오른쪽. 자동 플레이테스트의 봇이 착탄 지점을 예측할 때 씁니다.</summary>
        public int Direction => _direction;

        public void ResetRun()
        {
            SizePercent = 100f;
            _x = 0f;
            _direction = 1;
            _punch = 0f;
            Apply();
        }

        /// <summary>한 프레임 진행. 속도는 누적 명중 수에 따라 구간표에서 옵니다.</summary>
        public void Tick(float dt, float speed)
        {
            float limit = Mathf.Max(0f, config.playHalfWidth - Radius);

            if (limit <= 0f)
            {
                _x = 0f;
            }
            else
            {
                _x += _direction * speed * dt;

                // 한 프레임에 여러 번 튕길 만큼 dt 가 커도 제자리를 찾도록 반복해서 접습니다.
                while (_x > limit || _x < -limit)
                {
                    if (_x > limit) { _x = limit - (_x - limit); _direction = -1; }
                    else if (_x < -limit) { _x = -limit - (_x + limit); _direction = 1; }
                }
            }

            // 움찔한 크기를 서서히 0 으로 되돌립니다. (targetHitRecovery 가 클수록 빨리 가라앉습니다)
            _punch -= _punch * Mathf.Min(1f, config.targetHitRecovery * dt);
            Apply();
        }

        /// <summary>
        /// 맞았을 때 호출합니다. 구간표의 축소율만큼 작아지고, 최소 크기 아래로는 내려가지 않습니다.
        /// </summary>
        public void OnHit(int hitsSoFar)
        {
            float shrink = config.ShrinkPercent(hitsSoFar);
            float min = config.SizeMinPercent(hitsSoFar);
            SizePercent = Mathf.Max(min, SizePercent - shrink);
            _punch = config.targetHitPunch;
            Apply();
        }

        void Apply()
        {
            transform.position = new Vector3(_x, config.targetY, 0f);

            if (spriteRenderer == null || spriteRenderer.sprite == null) return;

            // 스프라이트 한 장의 가로가 지름(반지름 x2)이 되도록 배율을 잡습니다.
            float spriteWidth = spriteRenderer.sprite.bounds.size.x;
            if (spriteWidth <= 0f) return;

            float scale = (Radius * 2f + _punch) / spriteWidth;
            transform.localScale = new Vector3(scale, scale * config.targetFlatten, 1f);
        }
    }
}
