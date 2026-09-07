using System.Collections.Generic;
using UnityEngine;

namespace JumpJump
{
    /// <summary>
    /// 발판을 무한 생성 / 재활용하고, 착지 판정을 담당합니다.
    /// </summary>
    public class PlatformManager : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] Sprite blockSprite;
        [SerializeField] Color blockColor = new Color(0.36f, 0.78f, 0.98f);
        [SerializeField] Color groundColor = new Color(0.55f, 0.60f, 0.72f);

        readonly List<PlatformRow> _active = new List<PlatformRow>();
        readonly Stack<PlatformRow> _pool = new Stack<PlatformRow>();

        int _nextRow;

        public void ResetRun()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Release(_active[i]);
            _active.Clear();
            _nextRow = 0;
            EnsureRowsUpTo(config.rowsAhead + 4);
        }

        public void Tick(float dt, float cameraY, float cameraHalfHeight)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var row = _active[i];
                row.Tick(dt);

                if (row.transform.position.y < cameraY - cameraHalfHeight - 4f)
                {
                    Release(row);
                    _active.RemoveAt(i);
                }
            }

            int topVisibleRow = Mathf.CeilToInt((cameraY + cameraHalfHeight) / config.rowSpacing);
            EnsureRowsUpTo(topVisibleRow + config.rowsAhead);
        }

        void EnsureRowsUpTo(int topRow)
        {
            while (_nextRow <= topRow)
            {
                Spawn(_nextRow);
                _nextRow++;
            }
        }

        void Spawn(int rowIndex)
        {
            PlatformRow row = _pool.Count > 0 ? _pool.Pop() : CreateRow();

            // 칸마다 방향을 번갈아 주고 시작 위치를 흩뿌려서 패턴이 단조롭지 않게 합니다.
            int dir = (rowIndex % 2 == 0) ? 1 : -1;
            float startX01 = rowIndex == 0 ? 0.5f : Random.value;

            var color = rowIndex == 0 ? groundColor : blockColor;
            row.Configure(rowIndex, config, blockSprite, color, dir, startX01);
            row.name = "Row_" + rowIndex;
            _active.Add(row);
        }

        PlatformRow CreateRow()
        {
            var go = new GameObject("Row");
            go.transform.SetParent(transform, false);
            return go.AddComponent<PlatformRow>();
        }

        void Release(PlatformRow row)
        {
            row.Recycle();
            _pool.Push(row);
        }

        public PlatformRow GetRow(int rowIndex)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].RowIndex == rowIndex) return _active[i];
            return null;
        }

        /// <summary>
        /// 이번 프레임에 발이 prevFootY -> footY 로 내려오면서 통과한 발판 중
        /// 가장 높은 것을 돌려줍니다. (아래에서 위로 통과할 때는 뚫고 지나갑니다)
        ///
        /// 화면 밖으로 내려간 발판은 밟히지 않습니다. 발판은 화면 아래로 사라진 뒤에도
        /// 재활용될 때까지 잠시 살아 있어서, 그냥 두면 **보이지도 않는 발판 위에 착지해
        /// 살아남는** 일이 생깁니다. minSurfaceY(화면 아래끝)로 그것들을 걸러 냅니다.
        /// </summary>
        /// <param name="minSurfaceY">이 높이보다 아래에 있는 발판은 무시합니다 (보통 화면 아래끝)</param>
        public PlatformRow FindLanding(float x, float prevFootY, float footY, float minSurfaceY)
        {
            PlatformRow best = null;
            const float epsilon = 0.001f;

            for (int i = 0; i < _active.Count; i++)
            {
                var row = _active[i];
                float surface = row.SurfaceY;

                if (surface < minSurfaceY) continue;          // 화면 밖(아래)이라 보이지 않는 발판
                if (surface > prevFootY + epsilon) continue;  // 이번 프레임 시작 시 이미 발판 아래였음
                if (surface < footY) continue;                // 아직 발판까지 내려오지 않음
                if (!row.Covers(x, config.landPad)) continue; // 발판 밖으로 빗나감

                if (best == null || surface > best.SurfaceY) best = row;
            }

            return best;
        }
    }
}
