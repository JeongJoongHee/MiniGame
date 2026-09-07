using System.Collections.Generic;
using UnityEngine;

namespace Arcade
{
    /// <summary>
    /// 이 앱에 들어 있는 미니게임 목록입니다. 로비 화면이 이 목록만 보고 칸을 채웁니다.
    ///
    /// 새 미니게임을 붙이는 순서:
    ///   1. 게임 씬을 만든다 (예: Assets/Games/Flappy/Scenes/FlappyScene.unity)
    ///   2. 작업 폴더에 @Game_02_이름.png (동그란 아이콘) 과
    ///      @Name_02_이름.png (게임 이름이 그려진 이름표) 를 넣는다
    ///   3. Tools > Arcade > Build All Scenes 를 눌러 그림을 프로젝트로 가져온다
    ///   4. 이 에셋(GameCatalog.asset)에 항목을 한 줄 추가한다
    /// 코드는 건드릴 필요가 없습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameCatalog", menuName = "Arcade/Game Catalog")]
    public class GameCatalog : ScriptableObject
    {
        [Tooltip("폰 홈 화면에 찍히는 앱 이름. APK 를 구울 때 쓰입니다. " +
                 "타이틀 화면의 이름은 글자가 아니라 그림(@Title_Text.png)입니다")]
        public string appTitle = "심심할 때 하는 게임";

        [Tooltip("로비 칸에 들어갈 미니게임 목록")]
        public List<MiniGameEntry> games = new List<MiniGameEntry>();

        /// <summary>slot 번호로 게임을 찾습니다. 없으면 null (= 잠긴 칸).</summary>
        public MiniGameEntry FindBySlot(int slot)
        {
            for (int i = 0; i < games.Count; i++)
                if (games[i] != null && games[i].slot == slot && games[i].IsPlayable)
                    return games[i];
            return null;
        }

        public MiniGameEntry FindById(string id)
        {
            for (int i = 0; i < games.Count; i++)
                if (games[i] != null && games[i].id == id)
                    return games[i];
            return null;
        }
    }
}
