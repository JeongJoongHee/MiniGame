using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 캐릭터. Rigidbody 없이 직접 적분하는 커스텀 이동이라 결과가 항상 결정적입니다.
    /// - 발판 위에서는 발판을 타고 좌우로 함께 움직입니다.
    /// - 터치하면 수직으로만 점프합니다. (공중에서는 좌우 속도 없음 = 타이밍 게임)
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] PlatformManager platforms;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] CharacterAnimator characterAnimator;

        float _vy;
        PlatformRow _row;
        float _rideOffsetX;
        float _bufferedTapAt = -99f;
        float _clock;
        float _prevRowX;
        bool _hasPrevRowX;

        public bool IsGrounded { get; private set; }
        public int CurrentRow => _row != null ? _row.RowIndex : 0;
        public float HalfHeight => config.playerSize.y * 0.5f;
        public CharacterAnimator Animator => characterAnimator;
        public float FootY => transform.position.y - HalfHeight;

        public void ResetRun()
        {
            _vy = 0f;
            _rideOffsetX = 0f;
            _bufferedTapAt = -99f;
            _clock = 0f;
            _row = platforms.GetRow(0);
            IsGrounded = _row != null;

            float y = _row != null ? _row.SurfaceY + HalfHeight : HalfHeight;
            float x = _row != null ? _row.X : 0f;
            transform.position = new Vector3(x, y, 0f);

            _hasPrevRowX = false;

            if (characterAnimator != null)
            {
                characterAnimator.ResetRun();
                characterAnimator.PickRandomCharacter();
            }
        }

        /// <summary>
        /// GameManager 가 매 프레임 호출합니다. 입력을 직접 읽지 않고 넘겨받기 때문에
        /// 에디터 자동 플레이테스트에서 그대로 재생할 수 있습니다.
        /// </summary>
        public void Tick(float dt, bool tapped, bool allowInput)
        {
            if (allowInput && tapped) _bufferedTapAt = _clock;
            _clock += dt;

            if (IsGrounded) TickGrounded(dt, allowInput);
            else TickAirborne(dt, allowInput);

            if (characterAnimator != null) characterAnimator.Tick(dt, IsGrounded, _vy);
        }

        void TickGrounded(float dt, bool allowInput)
        {
            if (_row == null) { IsGrounded = false; return; }

            // 발판을 타고 함께 이동. 흐르는 방향을 바라보게 합니다.
            float rowX = _row.X;
            if (_hasPrevRowX && dt > 0f && characterAnimator != null)
                characterAnimator.SetFacing((rowX - _prevRowX) / dt);
            _prevRowX = rowX;
            _hasPrevRowX = true;

            transform.position = new Vector3(rowX + _rideOffsetX, _row.SurfaceY + HalfHeight, 0f);

            if (allowInput && _clock - _bufferedTapAt <= config.jumpBuffer)
            {
                _bufferedTapAt = -99f;
                Jump();
            }
        }

        void TickAirborne(float dt, bool allowInput)
        {
            float prevFootY = FootY;

            _vy -= config.gravity * dt;
            var p = transform.position;
            p.y += _vy * dt;
            transform.position = p;

            // 화면 아래끝. 여기보다 낮은 발판은 이미 보이지 않으므로 밟을 수 없습니다.
            float screenBottomY = cameraRig.BottomY;

            if (_vy <= 0f)
            {
                var landing = platforms.FindLanding(p.x, prevFootY, FootY, screenBottomY);
                if (landing != null)
                {
                    Land(landing);
                    return;
                }
            }

            if (allowInput && transform.position.y < screenBottomY - config.deathMargin)
            {
                GameManager.Instance.EndRun("FELL DOWN");
            }
        }

        void Jump()
        {
            IsGrounded = false;
            _row = null;
            _vy = config.JumpVelocity;
            _hasPrevRowX = false;
            if (characterAnimator != null) characterAnimator.OnJump();
        }

        void Land(PlatformRow row)
        {
            IsGrounded = true;
            _row = row;
            _vy = 0f;

            // 발판 위 어디에 착지했는지 기억해서, 발판 중앙으로 순간이동하지 않게 합니다.
            float maxOffset = Mathf.Max(0f, row.HalfWidth - config.playerSize.x * 0.35f);
            _rideOffsetX = Mathf.Clamp(transform.position.x - row.X, -maxOffset, maxOffset);
            transform.position = new Vector3(row.X + _rideOffsetX, row.SurfaceY + HalfHeight, 0f);

            if (characterAnimator != null) characterAnimator.OnLand();
            GameManager.Instance.OnLanded(row.RowIndex);
        }
    }
}
