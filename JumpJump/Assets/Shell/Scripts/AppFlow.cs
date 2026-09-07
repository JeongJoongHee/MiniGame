using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arcade
{
    /// <summary>
    /// 화면 사이를 오가는 유일한 통로입니다.
    /// 타이틀 -> 로비 -> 미니게임 -> (뒤로) 로비.
    ///
    /// 씬 이름을 여기 한 곳에만 적어 두면 나중에 씬을 옮기거나 이름을 바꿔도
    /// 고칠 데가 한 군데뿐입니다.
    /// </summary>
    public static class AppFlow
    {
        public const string TitleScene = "TitleScene";
        public const string LobbyScene = "LobbyScene";

        /// <summary>로비에서 마지막으로 고른 게임 id. 게임 씬에서 참고할 수 있습니다.</summary>
        public static string CurrentGameId { get; private set; } = "";

        public static void GoToTitle()
        {
            CurrentGameId = "";
            SceneManager.LoadScene(TitleScene);
        }

        public static void GoToLobby()
        {
            CurrentGameId = "";
            SceneManager.LoadScene(LobbyScene);
        }

        public static void GoToGame(MiniGameEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.sceneName))
            {
                Debug.LogError("[Arcade] 열 수 있는 게임 씬이 없습니다.");
                return;
            }

            CurrentGameId = entry.id;
            SceneManager.LoadScene(entry.sceneName);
        }
    }
}
