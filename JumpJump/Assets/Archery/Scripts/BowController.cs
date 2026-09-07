using UnityEngine;

namespace Archery
{
    /// <summary>
    /// 화면 아래 가운데에 고정된 활입니다. 좌우로 움직이지 않습니다 (기획서: "활 고정, 터치 시 화살 발사").
    /// 하는 일은 두 가지뿐입니다.
    ///   - 발사 순간 살짝 뒤로 밀렸다가 돌아오는 연출
    ///   - 시위에 걸린 화살을, 쏠 화살이 남아 있을 때만 보여 주기
    /// </summary>
    public class BowController : MonoBehaviour
    {
        [SerializeField] ArcheryConfig config;
        [SerializeField] Transform body;
        [SerializeField] GameObject nockedArrow;

        float _recoil;

        /// <summary>화살이 출발하는 x. 활이 고정이라 항상 0 입니다.</summary>
        public float MuzzleX => 0f;

        public void ResetRun()
        {
            _recoil = 0f;
            Apply();
        }

        public void Fire()
        {
            _recoil = config.bowRecoil;
            Apply();
        }

        public void Tick(float dt, bool hasAmmo)
        {
            _recoil -= _recoil * Mathf.Min(1f, config.bowRecovery * dt);
            if (nockedArrow != null && nockedArrow.activeSelf != hasAmmo)
                nockedArrow.SetActive(hasAmmo);
            Apply();
        }

        void Apply()
        {
            if (body == null) return;
            body.localPosition = new Vector3(0f, -_recoil, 0f);
        }
    }
}
