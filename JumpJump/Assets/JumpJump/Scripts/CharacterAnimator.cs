using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 캐릭터 스프라이트가 1프레임짜리라 애니메이션 클립이 없습니다.
    /// 대신 스쿼시&amp;스트레치 / 대기 바운스 / 진행 방향 반전을 코드로 만들어
    /// 점프-상승-하강-착지가 눈에 보이게 합니다.
    ///
    /// 스프라이트 피벗이 캐릭터의 "발"에 맞춰져 있어서 스케일이 발을 기준으로 걸립니다.
    /// (땅에 파묻히지 않고 위로 늘어나거나 아래로 눌립니다)
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Sprite[] characterSprites;

        float _impulse;      // 착지(-) / 점프(+) 순간에 들어오는 한 방
        float _bobPhase;
        int _facing = 1;

        public int SpriteCount => characterSprites != null ? characterSprites.Length : 0;

        /// <summary>판을 시작할 때 5종 중 하나를 무작위로 고릅니다.</summary>
        public int PickRandomCharacter()
        {
            if (SpriteCount == 0) return -1;

            int index = Random.Range(0, characterSprites.Length);
            spriteRenderer.sprite = characterSprites[index];
            return index;
        }

        public void ResetRun()
        {
            _impulse = 0f;
            _bobPhase = 0f;
            _facing = 1;
            transform.localScale = Vector3.one;
            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }

        public void OnJump() => _impulse = config.jumpStretch;

        public void OnLand() => _impulse = -config.landSquash;

        /// <summary>진행 방향을 바라보게 합니다. (발판을 타고 흐를 때)</summary>
        public void SetFacing(float velocityX)
        {
            if (Mathf.Abs(velocityX) < 0.05f) return;

            _facing = velocityX > 0f ? 1 : -1;
            if (spriteRenderer != null) spriteRenderer.flipX = _facing < 0;
        }

        public void Tick(float dt, bool grounded, float verticalVelocity)
        {
            // 1) 수직 속도에 비례한 늘어남 (올라갈 때도 떨어질 때도 길쭉해집니다)
            float speedStretch = grounded
                ? 0f
                : Mathf.Clamp(Mathf.Abs(verticalVelocity) * config.squashStrength, 0f, config.squashMax);

            // 2) 점프/착지 순간의 한 방은 시간에 따라 사그라듭니다
            _impulse = Mathf.Lerp(_impulse, 0f, 1f - Mathf.Exp(-config.squashRecovery * dt));

            // 3) 발판 위에서 대기할 때의 숨쉬기
            float bob = 0f;
            if (grounded)
            {
                _bobPhase += dt * config.idleBobSpeed;
                bob = Mathf.Sin(_bobPhase) * config.idleBobAmount;
            }
            else
            {
                _bobPhase = 0f;
            }

            float stretch = speedStretch + _impulse + bob;

            // 부피를 유지해서 늘어나면 홀쭉해지고 눌리면 넓어집니다
            float scaleY = 1f + stretch;
            float scaleX = 1f / Mathf.Max(0.2f, scaleY);

            // 좌우 반전은 SpriteRenderer.flipX 가 처리하므로 스케일은 항상 양수입니다.
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }
}
