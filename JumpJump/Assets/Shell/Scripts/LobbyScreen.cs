using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade
{
    /// <summary>
    /// 게임 선택 화면(로비)입니다.
    ///
    /// 배경 그림(Lobby.png)에는 동그란 자리 9개와 자물쇠가 **이미 그려져 있습니다.**
    /// 그래서 이 코드가 하는 일은 "게임이 들어 있는 칸에만" 그 자물쇠 위에
    /// 아이콘 / 이름표 / PLAY 버튼을 덮어 씌우는 것뿐입니다. 게임이 없는 칸은
    /// 아무것도 하지 않으므로 배경에 그려진 자물쇠가 그대로 보입니다.
    ///
    /// 칸의 위치(slotColumns / slotRows)는 Lobby.png 에서 동그라미를 실제로 재서 넣은
    /// 비율값입니다. 배경 그림을 바꾸면 이 값도 다시 재야 합니다.
    ///
    /// 이름표는 게임마다 따로입니다. 게임 이름이 그림 안에 그려져 있기 때문에
    /// (Art/Names/Name_01_*.png) 글자를 따로 얹지 않습니다.
    /// </summary>
    public class LobbyScreen : MonoBehaviour
    {
        [SerializeField] GameCatalog catalog;
        [SerializeField] RectTransform backdrop;

        [Header("칸에 올릴 공용 그림")]
        [Tooltip("게임에 이름표 그림이 없을 때 대신 깔리는 빈 이름표. 비워 두면 글자만 찍습니다")]
        [SerializeField] Sprite fallbackNamePlate;
        [SerializeField] Sprite playButton;

        [Header("배경 그림 안에서의 칸 위치 (0~1, 왼쪽 아래가 0,0)")]
        [Tooltip("세로줄 3개의 가운데 x")]
        [SerializeField] float[] slotColumns = { 0.1953f, 0.5114f, 0.8333f };
        [Tooltip("가로줄 3개의 가운데 y (위에서부터)")]
        [SerializeField] float[] slotRows = { 0.6390f, 0.3866f, 0.1800f };
        [Tooltip("동그란 자리 한 칸의 크기")]
        [SerializeField] Vector2 slotSize = new Vector2(0.2852f, 0.1601f);

        [Header("칸 한 개 안에서의 배치 (칸 크기를 1 로 본 값)")]
        [SerializeField] float iconWidth = 1.03f;
        [Tooltip("이름표 가로 폭. 게임마다 그림이 달라도 이 값으로 통일됩니다")]
        [SerializeField] float plateWidth = 0.78f;
        [Tooltip("이름표 세로 높이. **그림의 원본 비율을 쓰지 않고 이 값으로 고정**하기 때문에 " +
                 "게임이 늘어나도 이름표 크기가 전부 똑같습니다. " +
                 "그래서 이름표 그림은 가로:세로 약 2.15:1 로 그려 주셔야 찌그러지지 않습니다")]
        [SerializeField] float plateHeight = 0.364f;
        [SerializeField] float plateOffsetY = -0.40f;
        [SerializeField] float playWidth = 0.46f;
        [SerializeField] float playOffsetY = -0.715f;

        [Header("게임 칸마다 붙는 랭킹 아이콘 (수정사항_02, 2026-09-11)")]
        [Tooltip("누르면 그 게임의 랭킹이 골라진 채로 랭킹 창이 열립니다. 비워 두면 아이콘을 붙이지 않습니다")]
        [SerializeField] Sprite rankIcon;
        [Tooltip("아이콘 뒤에 까는 동그란 받침. 배경에 그려진 칸 번호를 가립니다. 비워 두면 아이콘만 얹습니다")]
        [SerializeField] Sprite rankBadge;
        [SerializeField] RankingPopup ranking;
        [Tooltip("칸 안에서 아이콘의 가운데 (칸 크기를 1 로 본 값). 기본값은 배경에 그려진 칸 번호(1, 2 ...) 자리입니다")]
        [SerializeField] Vector2 rankCenter = new Vector2(0.5f, 1.10f);
        [Tooltip("아이콘 가로 폭 (칸 폭을 1 로 본 값)")]
        [SerializeField] float rankWidth = 0.22f;
        [Tooltip("받침 지름 (칸 폭을 1 로 본 값). 칸 번호가 두 자리(10)여도 가려지는 크기입니다")]
        [SerializeField] float rankBadgeWidth = 0.32f;
        [Tooltip("손가락으로 누를 영역 (칸 크기를 1 로 본 값). 그림보다 넉넉하게 잡되, " +
                 "아래로 너무 내려가면 게임 칸을 누른 것까지 먹으므로 세로는 짧게 둡니다")]
        [SerializeField] Vector2 rankHitSize = new Vector2(0.44f, 0.30f);

        readonly List<GameObject> _cards = new List<GameObject>();

        void Start()
        {
            Build();
        }

        /// <summary>
        /// 카탈로그를 보고 칸을 다시 만듭니다. 여러 번 불러도 안전합니다.
        /// (에디터 미리보기도 이 함수를 그대로 씁니다)
        /// </summary>
        public void Build()
        {
            ClearCards();
            if (catalog == null || backdrop == null) return;

            var fitter = backdrop.GetComponent<AspectFitter>();
            float frameAspect = fitter != null ? fitter.Aspect : 0.5625f;

            // 칸 하나가 화면에서 갖는 가로/세로 비율. 그림을 찌그러뜨리지 않으려면 이 값이 필요합니다.
            float slotAspect = (slotSize.x * frameAspect) / Mathf.Max(0.0001f, slotSize.y);

            int slots = slotColumns.Length * slotRows.Length;
            for (int slot = 0; slot < slots; slot++)
            {
                var entry = catalog.FindBySlot(slot);
                if (entry == null) continue;   // 게임이 없는 칸 = 배경의 자물쇠를 그대로 둡니다

                BuildCard(slot, entry, slotAspect);
            }
        }

        void BuildCard(int slot, MiniGameEntry entry, float slotAspect)
        {
            int column = slot % slotColumns.Length;
            int row = slot / slotColumns.Length;

            var center = new Vector2(slotColumns[column], slotRows[row]);

            // 칸 하나를 사각형으로 만들어 두고, 그 안에서 다시 비율로 배치합니다.
            var cardGo = new GameObject("Slot_" + (slot + 1) + "_" + entry.id, typeof(RectTransform));
            cardGo.transform.SetParent(backdrop, false);
            var card = (RectTransform)cardGo.transform;
            ShellUI.Place(card, center, slotSize);
            _cards.Add(cardGo);

            // --- 아이콘 : 배경에 그려진 동그라미를 그대로 덮습니다 ---
            var icon = ShellUI.AddImage(card, "Icon", entry.icon);
            float iconHeight = ShellUI.HeightForWidth(entry.icon, iconWidth, slotAspect);
            ShellUI.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(iconWidth, iconHeight));

            // --- 이름표 ---
            // 게임 이름은 이름표 그림 안에 이미 그려져 있습니다 (Art/Names/Name_01_*.png).
            // 그림이 없는 게임만 빈 이름표 + 글자로 대신 찍습니다.
            var plateSprite = entry.namePlate != null ? entry.namePlate : fallbackNamePlate;
            if (plateSprite != null)
            {
                // 이름표만은 그림의 원본 비율을 따르지 않습니다. 게임마다 그림이 달라도
                // 로비에 늘어선 이름표 크기가 전부 같아야 하기 때문에 폭·높이를 고정합니다.
                var plate = ShellUI.AddImage(card, "NamePlate", plateSprite);
                ShellUI.Place(plate.rectTransform, new Vector2(0.5f, 0.5f + plateOffsetY), new Vector2(plateWidth, plateHeight));

                if (entry.namePlate == null)
                {
                    var label = ShellUI.AddText(plate.rectTransform, "Label", 64, new Color(0.13f, 0.18f, 0.30f));
                    label.text = entry.displayName;
                    ShellUI.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.84f, 0.58f));
                }
            }

            // --- PLAY 버튼 ---
            float playHeight = ShellUI.HeightForWidth(playButton, playWidth, slotAspect);
            var play = ShellUI.AddImage(card, "PlayButton", playButton);
            ShellUI.Place(play.rectTransform, new Vector2(0.5f, 0.5f + playOffsetY), new Vector2(playWidth, playHeight));
            play.gameObject.AddComponent<PulseScale>();

            // --- 터치 영역 : 아이콘부터 PLAY 까지 통째로 누를 수 있게 합니다 ---
            var hit = ShellUI.AddHitArea(card, "Hit");
            FitToChildren((RectTransform)hit.transform, card);
            hit.transform.SetAsLastSibling();

            var captured = entry;
            hit.onClick.AddListener(() => AppFlow.GoToGame(captured));

            // --- 랭킹 아이콘 : 칸 번호 자리에 얹습니다 ---
            // 게임 칸 터치 영역을 계산한 **뒤에** 만들어야 그 영역에 섞이지 않고, 위에 올라와 먼저 눌립니다.
            BuildRankButton(card, entry, slotAspect);
        }

        void BuildRankButton(RectTransform card, MiniGameEntry entry, float slotAspect)
        {
            if (rankIcon == null || ranking == null) return;

            if (rankBadge != null)
            {
                var badge = ShellUI.AddImage(card, "RankBadge", rankBadge);
                ShellUI.Place(badge.rectTransform, rankCenter,
                              new Vector2(rankBadgeWidth, ShellUI.HeightForWidth(rankBadge, rankBadgeWidth, slotAspect)));
            }

            var icon = ShellUI.AddImage(card, "RankIcon", rankIcon);
            ShellUI.Place(icon.rectTransform, rankCenter,
                          new Vector2(rankWidth, ShellUI.HeightForWidth(rankIcon, rankWidth, slotAspect)));

            var hit = ShellUI.AddHitArea(card, "RankButton");
            ShellUI.Place((RectTransform)hit.transform, rankCenter, rankHitSize);
            hit.transform.SetAsLastSibling();

            string gameId = entry.id;
            var view = ranking;
            hit.onClick.AddListener(() => view.ShowFor(gameId));
        }

        /// <summary>형제 요소들을 전부 감싸는 크기로 사각형을 넓힙니다.</summary>
        static void FitToChildren(RectTransform target, RectTransform card)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < card.childCount; i++)
            {
                var child = card.GetChild(i) as RectTransform;
                if (child == null || child == target) continue;
                min = Vector2.Min(min, child.anchorMin);
                max = Vector2.Max(max, child.anchorMax);
            }

            if (min.x > max.x) return;

            target.anchorMin = min;
            target.anchorMax = max;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        void ClearCards()
        {
            // 에디터에서 다시 만들 때를 대비해 이름으로도 한 번 훑습니다.
            if (backdrop != null)
            {
                for (int i = backdrop.childCount - 1; i >= 0; i--)
                {
                    var child = backdrop.GetChild(i);
                    if (child.name.StartsWith("Slot_")) DestroyNow(child.gameObject);
                }
            }

            foreach (var card in _cards)
                if (card != null) DestroyNow(card);
            _cards.Clear();
        }

        static void DestroyNow(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
