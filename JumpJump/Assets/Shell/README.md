# Shell — 타이틀 화면과 로비 (미니게임 껍데기)

이 폴더는 **게임이 아니라 게임들을 담는 그릇**입니다.
앱을 켜면 타이틀이 뜨고, START 를 누르면 로비가 뜨고, 로비에서 게임 칸을 누르면
그 미니게임 씬으로 들어갑니다. 미니게임이 늘어나도 이 폴더는 거의 그대로입니다.

```
타이틀(TitleScene) ──START──▶ 로비(LobbyScene) ──PLAY──▶ 미니게임 씬
                                   ▲                        │
                                   └───── ← (확인 팝업) ─────┘
```

지금 들어 있는 미니게임은 두 개입니다.

| 칸 | 게임 | 씬 | 폴더 |
| --- | --- | --- | --- |
| 1 (slot 0) | 점프점프! | `GameScene` | `Assets/JumpJump/` |
| 2 (slot 1) | 활쏘기! | `ArcheryScene` | `Assets/Archery/` |

## 만들고 확인하기

| 메뉴 | 하는 일 |
| --- | --- |
| **Tools > Arcade > Build All Scenes** | 타이틀 / 로비 / 미니게임 씬을 한 번에 굽고 Build Settings 까지 정리합니다. **평소엔 이것만 누르면 됩니다.** |
| Tools > Arcade > Import Shell Art | 작업 폴더의 `@` 그림들을 다시 가져옵니다 |
| Tools > Arcade > Capture Shell Preview PNG | 플레이 모드 없이 타이틀/로비 화면을 PNG 로 뽑습니다 |

배치 모드(에디터를 켜지 않고 확인)로도 같습니다.

```bash
UNITY="C:/Program Files/Unity Hub/6000.0.81f1/Editor/Unity.exe"
PROJ="D:/00.JumpJump/JumpJump"

"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Arcade.EditorTools.ShellSceneBuilder.BuildAll -logFile <로그>

"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod Arcade.EditorTools.ShellPreview.Capture -logFile <로그>
```

미리보기는 `JumpJump_Preview/` 에 네 장이 떨어집니다.

| 파일 | 화면 비율 |
| --- | --- |
| `00_title.png` / `00_lobby.png` | 9:16 — 기획서 목업과 같은 비율 |
| `00_title_tall.png` / `00_lobby_tall.png` | 20:9 — **요즘 폰의 실제 비율** |

두 장씩 찍는 이유는, 폰마다 세로 길이가 달라서 배경이 잘리거나 여백이 생기기 때문입니다.
아래 "화면 비율" 절을 보세요.

---

## 미니게임을 새로 붙이는 방법

코드를 고칠 필요가 없습니다. 4단계입니다.

1. **게임 씬을 만든다.** 예: `Assets/Games/Flappy/Scenes/FlappyScene.unity`
   씬 안에서 로비로 돌아가려면 버튼에 `Arcade.AppFlow.GoToLobby()` 를 연결합니다.
2. **그림 두 장을 작업 폴더에 넣는다.** 번호는 게임 순서대로 맞춥니다.
   - `D:\00.JumpJump\@Game_02_이름.png` — 로비 칸의 **동그란 아이콘**.
     `@Game_01_Jump.png` 처럼 동그란 그림이어야 배경의 동그란 자리에 딱 맞습니다.
   - `D:\00.JumpJump\@Name_02_이름.png` — 아이콘 아래의 **이름표**.
     **게임 이름이 그림 안에 그려져 있어야 합니다.** 코드는 글자를 따로 얹지 않습니다.
     (`@Name_01_점프점프.png` 을 그대로 흉내 내면 됩니다)
     **이름표를 아직 안 그렸으면 넣지 않아도 됩니다.** 그때는 껍데기가 만들어 두는
     빈 이름표(`Art/NamePlate_Blank.png`) 위에 `Display Name` 이 글자로 찍힙니다.
     **그림 비율은 가로:세로 약 2.15:1 로 맞춰 주세요.** 로비의 이름표는 게임마다
     크기가 같아야 해서 **정해진 칸 크기로 그려 넣기 때문**입니다 (아래 "이름표 크기" 절).
   - 주변 투명 여백은 가져오면서 자동으로 잘라 내므로 신경 쓰지 않아도 됩니다.
3. **Tools > Arcade > Build All Scenes** 를 누른다. (그림이 프로젝트로 들어옵니다)
4. **`Assets/Shell/GameCatalog.asset`** 를 열고 `Games` 목록에 한 줄 추가한다.
   `Icon` 과 `Name Plate` 를 비워 두면 빌드할 때 **목록 순서대로** 자동으로 채워집니다.

| 칸 | 뜻 |
| --- | --- |
| `Id` | 코드용 이름 (예: `flappy`) |
| `Display Name` | 게임 이름. 이름표 그림이 없을 때만 글자로 찍힙니다 (예: `날아라!`) |
| `Scene Name` | 씬 파일 이름, 확장자 없이 (예: `FlappyScene`) |
| `Icon` | 방금 가져온 `Art/Games/Game_02_이름.png` |
| `Name Plate` | 방금 가져온 `Art/Names/Name_02_이름.png`. 비워 두면 빈 이름표 + 글자로 대신 나옵니다 |
| `Slot` | 로비의 몇 번째 칸인지. **0 = 왼쪽 위, 8 = 오른쪽 아래** |
| `Available` | 끄면 그 칸은 자물쇠(잠김)로 남습니다 |

> **씬을 Build Settings 에 넣는 것을 잊지 마세요.** 여기만은 코드를 고쳐야 합니다.
> `ShellSceneBuilder` 의 씬 경로 상수에 한 줄, `RegisterBuildSettings()` 목록에 한 줄,
> `BuildAll()` 에 그 게임의 씬 빌더 호출 한 줄 — 활쏘기를 붙일 때 고친 곳이 그 세 군데입니다.
> (APK 에 넣으려면 `JumpJumpBuild.Scenes` 목록에도 추가해야 합니다)

### 로비 칸 번호

배경 그림에 번호가 **그려져 있습니다.** 코드의 `Slot` 번호(0~8)와는 다릅니다.

| 그림에 적힌 번호 | 1 | 2 | 3 | 4 | 5 | 6 | 8 | 9 | 10 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 코드의 `Slot` | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |

> 그림의 7번째 칸에 **7 대신 8** 이 적혀 있고 마지막이 10 으로 끝납니다.
> 그림 쪽 오타로 보이니, 고치실 거면 `@Lobby.png` 를 수정한 뒤
> Tools > Arcade > Import Shell Art 를 눌러 주세요.

---

## 화면이 어떻게 자리를 잡는가

이 앱의 UI 는 **픽셀 좌표를 쓰지 않습니다.** 전부 "배경 그림 안에서 몇 % 지점" 으로 붙습니다.

1. `AspectFitter` 가 배경 그림을 원본 비율 그대로 화면에 맞춥니다.
2. 버튼·아이콘·이름표는 그 배경 사각형의 **비율(앵커)** 로 붙습니다.

그래서 폰이 길든 짧든 아이콘이 배경에 그려진 동그란 자리 위에 정확히 얹힙니다.
숫자를 픽셀로 박아 두면 해상도마다 어긋나는데, 이 방식은 어긋날 수가 없습니다.

### 화면 비율

| 화면 | 맞추는 방식 | 이유 |
| --- | --- | --- |
| 타이틀 | **Cover** (화면을 꽉 채우고 넘치는 쪽은 자름) | 그냥 풍경이라 조금 잘려도 됩니다 |
| 로비 | **Contain** (그림 전체가 보이게, 남는 쪽은 여백) | 칸이 하나라도 잘리면 안 됩니다 |

> **딱 하나, 타이틀의 게임 이름 그림만은 배경이 아니라 화면에 붙습니다.**
> 타이틀 배경은 Cover 라서 요즘 폰(20:9)에서는 좌우가 20% 가까이 잘려 나갑니다.
> 이름을 배경 기준으로 크게 잡으면 그때 첫 글자와 끝 글자가 같이 잘립니다.
> 그래서 `TitleScreen.titleWidth` 는 **화면 폭 기준**입니다 (1.0 = 화면에 꽉 참).
> 높이는 계산하지 않고 `preserveAspect` 에 맡기므로 어떤 경우에도 찌그러지지 않습니다.

로비 그림(`1536x2730` ≈ 9:16)이 요즘 폰(20:9)보다 짧아서, **위아래에 어두운 띠가 생깁니다.**
배경 테두리 색을 어둡게 깐 것이라 액자처럼 보이긴 하지만, 없애고 싶다면
세로로 더 긴 로비 그림을 새로 그려서 `@Lobby.png` 를 교체하면 됩니다.
(그때는 `LobbyScreen` 의 `Slot Columns / Slot Rows / Slot Size` 를 다시 재야 합니다)

### 이름표 크기

로비의 이름표는 **그림의 원본 비율을 따르지 않고 정해진 칸(`LobbyScreen.plateWidth` x
`plateHeight`)에 맞춰 그려집니다.** 게임마다 그림이 달라도 로비에 늘어선 이름표 크기가
전부 같아야 하기 때문입니다. 그래서 **이름표 그림은 가로:세로 약 2.15:1** 로 그려 주세요.
(`Name_01_점프점프.png` 1761x821 = 2.145 가 기준입니다)

크기를 바꾸고 싶으면 `LobbyScreen` 인스펙터의 `Plate Width` / `Plate Height` 를 같이 고치면 됩니다.

### 나가기 / 설정 팝업 (2026-09-08)

앱을 나가는 길은 전부 **확인 팝업 한 번**을 거칩니다. 실수로 눌러서 판이 날아가지 않도록.

| 어디서 | 누르는 것 | 뜨는 팝업 | [확인] 하면 |
| --- | --- | --- | --- |
| 미니게임 | 오른쪽 맨 위 **←** | 로비로 나가시겠습니까? | 로비로 (진행 상황은 저장 안 함) |
| 로비 | 오른쪽 위 **톱니바퀴** | 설정 (빈 판 + [종료] [닫기]) | — |
| 로비 | 설정의 **[종료]** | 종료하시겠습니까? | 앱 종료 |
| 타이틀 · 로비 | 폰 **뒤로 버튼** | 종료하시겠습니까? | 앱 종료 |
| 미니게임 | 폰 **뒤로 버튼** | 로비로 나가시겠습니까? | 로비로 |

팝업이 떠 있을 때 뒤로 버튼을 누르면 **그 팝업만 닫힙니다.**

설정 팝업의 판 위쪽은 일부러 비워 두었습니다. **나중에 사운드 조절이 들어갈 자리**입니다.

#### 코드에서

- 팝업 한 장 = `PopupPanel` 컴포넌트 하나. 씬에 **꺼진 채로 미리 만들어져** 있다가
  `Open()` / `Close()` 로 켜고 꺼집니다. 실행 중에 만들지 않으므로 첫 등장이 끊기지 않습니다.
- 버튼이 부르는 함수는 전부 `ShellMenu` 에 있습니다. 씬마다 필요한 팝업만 연결되어 있어서
  **같은 컴포넌트가 타이틀·로비·미니게임에서 각각 다르게 동작합니다.**
  ([닫기]와 [취소]는 셋 다 `OnCancelPressed` = "맨 위 팝업 닫기" 하나를 씁니다)
- **미니게임에 붙일 때는 `ShellSceneBuilder.MakeExitToLobbyUI(root)` 한 줄이면 됩니다.**
  화살표 버튼 + 팝업 + `ShellMenu` 가 통째로 만들어집니다. **게임 쪽 코드는 필요 없습니다.**
  (예전에는 HUD 에 `OnLobbyPressed()` 가 있어야 했지만 이제 아닙니다)
- 팝업이 떠 있는 동안 게임이 멈추는 것은 각 게임 `Update` 의 `PopupPanel.AnyOpen` 검사입니다.
  **`Step(dt, tap)` 은 팝업을 전혀 모릅니다** — 그래서 배치 모드 플레이테스트가 그대로 돕니다.
- 팝업 뒤의 어두운 막이 터치를 전부 먹기 때문에, 뒤쪽 버튼이 눌리지 않게 따로 막을 필요가 없습니다.
- 폰의 하드웨어 뒤로 버튼은 Input System 에서 **Esc 키**로 들어옵니다.
  윈도우 에디터에서 Esc 를 눌러도 똑같이 확인할 수 있습니다.

버튼을 누른 것이 점프·발사로 세지 않는 것은 각 게임의 `TapInput.OverUI` 가 막아 줍니다.

### 그림의 투명 여백을 잘라 내는 이유

원본 버튼 PNG 는 그림 주위에 빈 공간이 넓습니다. 예를 들어 `@Start_Button.png` 는
2400x1519 인데 실제 버튼은 1529x483 뿐입니다. 그대로 쓰면 "버튼을 화면 폭의 46%로"
라고 지정해도 눈에 보이는 버튼은 29% 밖에 안 됩니다.

`ShellArt` 가 가져오면서 여백을 잘라 내므로 **사각형 크기 = 보이는 그림 크기** 가 되고,
배치 숫자가 그대로 맞아떨어집니다. 원본은 작업 폴더에 그대로 남아 있습니다.

> **작업 폴더를 거치지 않고 `Art/Games` · `Art/Names` 에 그림을 바로 넣어도 됩니다.**
> 그 경우에도 다음 빌드에서 여백을 잘라 냅니다(`ShellArt.TrimStrayArt`). 다만 이때는
> **프로젝트 안의 파일을 직접 고치므로 원본이 남지 않습니다.** 원본을 남기고 싶으면
> 작업 폴더에 `@Name_02_이름.png` 형태로 넣는 쪽이 안전합니다.

| 원본 | 프로젝트 안 | 잘린 뒤 |
| --- | --- | --- |
| `@Start_Button.png` 2400x1519 | `Art/Start_Button.png` | 1529x483 |
| `@Play_Button.png` 2400x1309 | `Art/Play_Button.png` | 1724x803 |
| `@Title_Text.png` 2400x1309 | `Art/Title_Text.png` | 2192x534 |
| `@Name_01_점프점프.png` 2400x1308 | `Art/Names/Name_01_점프점프.png` | 1761x821 |
| `@Game_01_Jump.png` 635x569 | `Art/Games/Game_01_Jump.png` | 492x510 |
| `@Title.jpg` / `@Lobby.png` | 그대로 복사 (배경은 자를 여백이 없음) | — |

---

## 구조

```
Assets/Shell/
  GameCatalog.asset       미니게임 목록 (여기를 고쳐서 게임을 추가합니다)
  Art/
    Title.jpg             타이틀 배경
    Title_Text.png        타이틀의 게임 이름 (글자가 아니라 그림입니다)
    Lobby.png             로비 배경 (동그란 자리 9개와 자물쇠가 그려져 있음)
    Start_Button.png      타이틀의 START
    Play_Button.png       로비 칸의 PLAY
    NamePlate_Blank.png   이름표 그림이 없는 게임에 깔리는 빈 이름표 (코드가 만듭니다)
    Setting_Button.png    로비 오른쪽 위 톱니바퀴          (원본 Setting.png)
    Back_Button.png       미니게임 오른쪽 위 뒤로가기 화살표 (원본 Undo.png)
    Popup_Panel.png       설정 팝업의 빈 판                (원본 Empty_Pannel.png)
    Popup_Ask_Exit.png    "로비로 나가시겠습니까?"          (원본 Popup_01.png)
    Popup_Ask_Quit.png    "종료하시겠습니까?"              (원본 Popup_02.png)
    Ok_Button.png / Cancel_Button.png / End_Button.png / Close_Button.png
    Games/                미니게임 아이콘 (Game_01_Jump.png / Game_02_활쏘기.png ...)
    Names/                미니게임 이름표 (Name_01_점프점프.png ...)
  Scenes/
    TitleScene.unity      앱 시작 지점
    LobbyScene.unity
  Scripts/
    AppFlow.cs            화면 이동의 유일한 통로 (씬 이름이 여기 한 곳에만 있음)
    GameCatalog.cs        미니게임 목록 에셋
    MiniGameEntry.cs      목록 한 줄
    TitleScreen.cs        타이틀 배치 + START
    LobbyScreen.cs        카탈로그를 읽어 칸을 채움
    AspectFitter.cs       배경을 원본 비율로 화면에 맞춤
    ShellUI.cs            비율 기반 배치 도구
    PopupPanel.cs         팝업 한 장 (켜기/끄기 + 열린 팝업 목록)
    ShellMenu.cs          나가기/설정 버튼이 부르는 함수들 + 폰 뒤로 버튼
    PulseScale.cs         버튼 숨쉬기 애니메이션
  Editor/
    ShellArt.cs           작업 폴더 그림 가져오기 + 여백 자르기 + 임포트 설정
    ShellSceneBuilder.cs  씬 자동 생성
    ShellPreview.cs       플레이 모드 없이 화면 PNG 캡처
```

## 설계 메모

- **씬을 손으로 편집하지 마세요.** `ShellSceneBuilder` 가 씬을 통째로 굽는 방식이라
  씬에 직접 넣은 변경은 다음 빌드에서 전부 사라집니다.
  화면 구성을 바꾸려면 `ShellSceneBuilder` 를, 배치 숫자만 바꾸려면
  `TitleScreen` / `LobbyScreen` 의 인스펙터 값을 고치세요.
- **로비 칸은 실행할 때 코드로 만듭니다.** 그래야 게임을 추가할 때
  `GameCatalog.asset` 한 줄만 늘리면 되고 씬을 다시 구울 필요가 없습니다.
- **껍데기와 게임은 서로 다른 네임스페이스입니다.** 껍데기는 `Arcade`,
  점프점프는 `JumpJump`, 활쏘기는 `Archery`. 게임이 껍데기를 부를 일은
  `AppFlow.GoToLobby()` 하나뿐이고, 게임끼리는 서로를 부르지 않습니다.
- 게임 안에서 "로비로" 버튼을 눌렀을 때 그게 점프로도 세지 않도록,
  `TapInput.OverUI` 가 UI 위의 터치를 걸러 냅니다.

## 아직 없는 것 / 알려진 제약

- **설정 팝업은 아직 [종료] / [닫기] 두 개뿐입니다.** 판 위쪽은 사운드 조절을 넣으려고
  비워 둔 자리입니다. (2026-09-07 에 로비 그림에서 지웠던 톱니바퀴를 2026-09-08 에
  진짜 버튼으로 다시 얹었습니다. 자리는 원래 그림에 그려져 있던 그 자리입니다)
- **한글 글자는 이제 화면에 거의 안 나옵니다.** 타이틀의 게임 이름과 로비의 이름표가
  모두 그림(`Title_Text.png` / `Names/Name_NN_*.png`)으로 바뀌었기 때문입니다.
  글자로 그리는 곳은 미니게임 안의 HUD(전부 영어·숫자)와, 이름표 그림이 없는 게임의 이름뿐입니다.
  **지금은 두 게임 다 이름표 그림이 있어서, 화면에 한글을 글자로 그리는 곳이 없습니다.**
  그 부분은 Unity 기본 LegacyRuntime 폰트가 운영체제 폰트로 대체(fallback)해서 그립니다.
  **안드로이드 기기에서 한글이 제대로 나오는지는 실제로 확인해 봐야 합니다.**
  깨져 보이면 무료 폰트(예: 나눔고딕)를 `Assets/Shell/Art/` 옆에 넣고
  `ShellUI.AddText` 의 폰트를 바꾸면 됩니다.
- 로비에서 폰 뒤로 버튼을 누르면 타이틀로 돌아가지 않고 **바로 종료 확인**이 뜹니다.
  (2026-09-08 에 사용자가 그렇게 정했습니다)
- 로비에서 최고 점수 같은 기록을 보여 주지 않습니다.
