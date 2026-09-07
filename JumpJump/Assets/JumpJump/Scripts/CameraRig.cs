using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 카메라는 위로만 따라 올라갑니다. 절대 내려오지 않기 때문에,
    /// 점프에 실패해 아래로 떨어지면 화면 밖으로 벗어나 게임 오버가 됩니다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] Transform target;

        Camera _cam;

        public float HalfHeight => _cam != null ? _cam.orthographicSize : config.orthoSize;
        public float BottomY => transform.position.y - HalfHeight;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = config.orthoSize;
        }

        public void ResetRun()
        {
            if (_cam == null) Awake();
            float y = (target != null ? target.position.y : 0f) + config.cameraPlayerOffset;
            var p = transform.position;
            transform.position = new Vector3(p.x, y, p.z);
        }

        public void Tick(float dt)
        {
            if (target == null) return;

            float desiredY = target.position.y + config.cameraPlayerOffset;
            var p = transform.position;

            // 위로만 이동
            if (desiredY > p.y)
            {
                p.y = Mathf.Lerp(p.y, desiredY, 1f - Mathf.Exp(-config.cameraSmooth * dt));
                transform.position = p;
            }
        }
    }
}
