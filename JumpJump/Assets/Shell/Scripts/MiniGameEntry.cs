using System;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// 로비에 올라가는 미니게임 한 칸의 정보입니다.
    /// 새 게임을 추가할 때 이 항목을 GameCatalog 에 한 줄 늘리면 됩니다.
    /// </summary>
    [Serializable]
    public class MiniGameEntry
    {
        [Tooltip("코드에서 게임을 구분하는 이름. 영문 소문자 권장 (예: jumpjump)")]
        public string id = "";

        [Tooltip("게임 이름. 이름표 그림이 없을 때 글자로 대신 찍히고, 로그·저장 키에도 쓰입니다")]
        public string displayName = "";

        [Tooltip("PLAY 를 누르면 열리는 씬 이름. Build Settings 에 등록되어 있어야 합니다")]
        public string sceneName = "";

        [Tooltip("로비 칸에 올라갈 동그란 아이콘. Art/Games/ 의 그림")]
        public Sprite icon;

        [Tooltip("아이콘 아래에 붙는 이름표 그림. Art/Names/ 의 그림으로, 게임 이름이 그려져 있습니다. " +
                 "비워 두면 displayName 을 글자로 찍습니다")]
        public Sprite namePlate;

        [Tooltip("로비에서 아이콘 크기 배율. 1 = 칸을 꽉 채움. " +
                 "아이콘 그림에 칸 테두리가 그려져 있지 않으면(과녁처럼 꽉 찬 그림) 0.8 쯤으로 줄여야 " +
                 "배경의 갈색 테두리가 보입니다 (수정사항_03)")]
        [Range(0.3f, 1.2f)] public float iconScale = 1f;

        [Tooltip("켜면 아이콘 그림을 **동그랗게 잘라서** 칸의 동그라미 안에 넣습니다. 네모난 그림(검 강화처럼 꽉 찬 그림)에 씁니다. " +
                 "원본은 그대로 두고 Build All Scenes 때 Art/GamesRound/ 에 잘린 사본을 만듭니다 (2026-09-13)")]
        public bool roundIcon = false;

        [Tooltip("roundIcon 이 켜져 있을 때 Build All Scenes 가 채우는 동그란 사본. 손으로 넣지 마세요")]
        public Sprite roundedIcon;

        /// <summary>로비 칸에 실제로 그리는 아이콘 — 동그랗게 자른 사본이 있으면 그것, 없으면 원본.</summary>
        public Sprite LobbyIcon => roundIcon && roundedIcon != null ? roundedIcon : icon;

        [Tooltip("로비의 몇 번째 칸에 놓을지. 0 = 왼쪽 위, 8 = 오른쪽 아래")]
        [Range(0, 8)] public int slot = 0;

        [Tooltip("끄면 그 칸은 자물쇠(잠김) 상태로 남습니다")]
        public bool available = true;

        public bool IsPlayable => available && !string.IsNullOrEmpty(sceneName) && icon != null;
    }
}
