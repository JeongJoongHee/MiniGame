using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// "여기를 누르세요" 를 말 없이 알려 주는 아주 작은 숨쉬기 애니메이션입니다.
    /// 애니메이션 클립 없이 코드로만 돌아갑니다.
    /// </summary>
    public class PulseScale : MonoBehaviour
    {
        [SerializeField] float amount = 0.04f;
        [SerializeField] float speed = 2.2f;

        Vector3 _base = Vector3.one;
        float _clock;

        void OnEnable()
        {
            _base = transform.localScale;
            _clock = 0f;
        }

        void Update()
        {
            _clock += Time.unscaledDeltaTime * speed;
            float s = 1f + Mathf.Sin(_clock) * amount;
            transform.localScale = new Vector3(_base.x * s, _base.y * s, _base.z);
        }
    }
}
