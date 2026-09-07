using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 배경을 세로(그리고 가로)로 무한 반복시키고, 높이에 따라 다른 배경으로 서서히 넘깁니다.
    ///
    /// 반복 원리: SpriteRenderer 의 Tiled 드로우 모드로 한 장을 격자처럼 깔아 두고,
    /// 카메라를 따라다니되 위치를 "타일 높이의 정수배" 만큼만 어긋나게 잡습니다.
    /// 타일이 전부 같은 그림이라 그 어긋남은 눈에 보이지 않고,
    /// 결과적으로 이음매 없이 끝없이 이어지는 배경이 됩니다.
    /// (그러려면 그림 자체의 위끝과 아래끝이 이어져 있어야 합니다.)
    ///
    /// 구간 전환: 겹쳐 놓은 두 장 중 아래(current)가 지금 구간, 위(next)가 다음 구간입니다.
    /// 경계에 가까워지면 next 의 투명도를 0 -> 1 로 올려서 서서히 갈아탑니다.
    /// 높이별 배경 목록과 겹치는 구간 길이는 GameConfig 에 있습니다.
    /// </summary>
    public class BackdropTiler : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] Transform cameraTransform;
        [SerializeField] SpriteRenderer current;   // 지금 구간
        [SerializeField] SpriteRenderer next;      // 다음 구간 (경계에서 서서히 나타납니다)

        int _currentIndex = -1;
        int _nextIndex = -1;

        void Awake()
        {
            Setup();
        }

        public void Setup()
        {
            _currentIndex = _nextIndex = -1;
            if (config == null) return;

            Apply(current, SpriteAt(0));
            Apply(next, null);
            _currentIndex = 0;
        }

        public void Tick()
        {
            Tick(0f);
        }

        /// <summary>meters = 지금 플레이어가 올라온 높이. 배경 구간을 고르는 데 씁니다.</summary>
        public void Tick(float meters)
        {
            if (config == null || cameraTransform == null) return;

            int index = config.BackdropIndexForMeters(meters);
            if (index < 0) return;

            float blend = config.BackdropBlend(meters, index);
            int upper = index + 1;

            if (index != _currentIndex)
            {
                Apply(current, SpriteAt(index));
                _currentIndex = index;
            }

            // 다음 구간은 겹치기 시작할 때만 준비합니다 (평소에는 꺼 둡니다).
            int wanted = blend > 0f ? upper : -1;
            if (wanted != _nextIndex)
            {
                Apply(next, wanted >= 0 ? SpriteAt(wanted) : null);
                _nextIndex = wanted;
            }

            if (next != null && next.sprite != null)
            {
                var c = next.color;
                c.a = blend;
                next.color = c;
            }

            float camY = cameraTransform.position.y;
            float camX = cameraTransform.position.x;
            Place(current, camX, camY);
            Place(next, camX, camY);
        }

        Sprite SpriteAt(int index)
        {
            var bands = config != null ? config.backdropBands : null;
            if (bands == null || index < 0 || index >= bands.Length) return null;
            return bands[index].sprite;
        }

        /// <summary>스프라이트를 갈아 끼우고 타일 격자 크기를 카메라에 맞춥니다.</summary>
        void Apply(SpriteRenderer sr, Sprite sprite)
        {
            if (sr == null) return;

            sr.sprite = sprite;
            if (sprite == null)
            {
                sr.enabled = false;
                return;
            }

            sr.enabled = true;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;

            // 스프라이트 한 장이 config.backdropTileWidth 만큼의 폭을 갖도록 맞춥니다.
            var size = sprite.bounds.size;
            float scale = config.backdropTileWidth / Mathf.Max(0.0001f, size.x);
            sr.transform.localScale = new Vector3(scale, scale, 1f);

            // 카메라를 넉넉히 덮도록 타일 두 장씩 여유를 둡니다. (localScale 기준이라 나눠 줍니다)
            float halfHeight = config.orthoSize;
            float halfWidth = halfHeight * Mathf.Max(1f, (float)Screen.width / Mathf.Max(1, Screen.height));
            sr.size = new Vector2(
                halfWidth * 2f / scale + size.x * 2f,
                halfHeight * 2f / scale + size.y * 2f);
        }

        /// <summary>카메라 근처로 끌어오되, 타일 높이의 정수배만 움직여 보이는 그림을 유지합니다.</summary>
        void Place(SpriteRenderer sr, float camX, float camY)
        {
            if (sr == null || sr.sprite == null || !sr.enabled) return;

            // 시차(parallax): 0 이면 카메라에 붙어 정지, 1 이면 발판과 같은 속도로 흐릅니다.
            float desiredY = camY * (1f - config.backdropParallax);

            float tileHeight = sr.sprite.bounds.size.y * sr.transform.localScale.y;
            if (tileHeight > 0.0001f)
                desiredY += Mathf.Round((camY - desiredY) / tileHeight) * tileHeight;

            var t = sr.transform;
            t.position = new Vector3(camX, desiredY, t.position.z);
        }
    }
}
