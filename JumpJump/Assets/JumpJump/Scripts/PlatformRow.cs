using System.Collections.Generic;
using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 한 칸(row)에 해당하는 발판. 여러 개의 블록이 이어진 형태이며 좌우로 왕복합니다.
    /// 물리 콜라이더를 쓰지 않고, 착지 판정은 PlatformManager 가 수식으로 처리합니다.
    /// </summary>
    public class PlatformRow : MonoBehaviour
    {
        readonly List<SpriteRenderer> _blocks = new List<SpriteRenderer>();

        float _speed;
        int _dir = 1;
        float _minX;
        float _maxX;

        public int RowIndex { get; private set; }
        /// <summary>발판 절반 폭.</summary>
        public float HalfWidth { get; private set; }
        /// <summary>플레이어의 발이 닿는 윗면 높이.</summary>
        public float SurfaceY { get; private set; }
        public float X => transform.position.x;

        public void Configure(int rowIndex, GameConfig cfg, Sprite sprite, Color color, int startDir, float startX01)
        {
            RowIndex = rowIndex;

            int blocks = cfg.BlocksForRow(rowIndex);
            float width = blocks * cfg.blockWidth;
            HalfWidth = width * 0.5f;

            float y = rowIndex * cfg.rowSpacing;
            SurfaceY = y + cfg.blockHeight * 0.5f;

            _speed = cfg.SpeedForRow(rowIndex);
            _dir = startDir >= 0 ? 1 : -1;

            float limit = Mathf.Max(0f, cfg.playHalfWidth - HalfWidth);
            _minX = -limit;
            _maxX = limit;

            float x = Mathf.Lerp(_minX, _maxX, startX01);
            transform.position = new Vector3(x, y, 0f);

            BuildBlocks(blocks, cfg, sprite, color);
            gameObject.SetActive(true);
        }

        void BuildBlocks(int blocks, GameConfig cfg, Sprite sprite, Color color)
        {
            while (_blocks.Count < blocks)
            {
                var go = new GameObject("Block");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.drawMode = SpriteDrawMode.Simple;
                sr.sortingOrder = 5;
                _blocks.Add(sr);
            }

            // Base.png 한 장이 정확히 blockWidth 가 되도록 PPU 가 설정돼 있으므로
            // 스케일 1 그대로 옆으로 이어 붙이면 타일이 딱 맞물립니다.
            for (int i = 0; i < _blocks.Count; i++)
            {
                var sr = _blocks[i];
                bool used = i < blocks;
                sr.gameObject.SetActive(used);
                if (!used) continue;

                sr.sprite = sprite;
                sr.color = color;
                sr.transform.localPosition = new Vector3(-HalfWidth + (i + 0.5f) * cfg.blockWidth, 0f, 0f);
                sr.transform.localScale = Vector3.one;
            }
        }

        public void Tick(float dt)
        {
            if (_speed <= 0f || _maxX <= _minX) return;

            var p = transform.position;
            p.x += _dir * _speed * dt;

            if (p.x > _maxX) { p.x = _maxX; _dir = -1; }
            else if (p.x < _minX) { p.x = _minX; _dir = 1; }

            transform.position = p;
        }

        /// <summary>주어진 x 좌표가 이 발판 위에 있는지.</summary>
        public bool Covers(float x, float pad)
        {
            return Mathf.Abs(x - X) <= HalfWidth + pad;
        }

        public void Recycle()
        {
            gameObject.SetActive(false);
        }
    }
}
