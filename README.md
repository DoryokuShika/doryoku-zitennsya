# 2026チャリ青春切符ゲッター

Unity で制作した自転車走行シミュレーションゲームです。  
プレイヤーは自転車を操作して街を走り回り、交通ルールを守りながら（または守らずに）ゴールを目指します。警察の視界に入った違反は警告・反則金表示の対象となり、車両や歩行者との接触でゲームオーバーになります。

| 項目 | 内容 |
|------|------|
| エンジン | Unity **2022.3.62f3** |
| 言語 | C# |
| 解像度（既定） | 1920 × 1080 |

---

## リポジトリ構成

```
2026AOKIPPU/
├── Assets/                    # ゲーム本体（モデル・シーン・スクリプトなど）
│   ├── Scenes/                # シーンファイルと UI 用スクリプト
│   ├── Images/                # 3D モデル・テクスチャ・ゲームロジック用スクリプト
│   ├── Editor/                # Unity エディタ拡張
│   └── Fantasy Skybox FREE/   # 外部アセット（空のスカイボックス）
├── ProjectSettings/           # Unity プロジェクト設定
├── Packages/                  # パッケージ依存関係
└── README.md                  # このファイル
```

> ※ `Library/`、`Logs/`、`UserSettings/`、`obj/` は Unity が自動生成するフォルダです。`.gitignore` により Git 管理外です。

---

## プログラムコードの格納場所

自作の C# スクリプトは主に次の 3 か所にあります。

### 1. `Assets/Scenes/script/` — シーン遷移・UI 共通

| ファイル | 役割 |
|----------|------|
| `SceneChanger.cs` | 指定秒数後に次のシーンへ自動遷移 |
| `Button_3d.cs` | 3D ボタン操作 |
| `BestTime.cs` / `BestTimeReset.cs` | ベストタイムの表示・リセット |
| `ScoreManager.cs` | ベストタイムの `PlayerPrefs` 保存・読み込み |
| `Exitgame.cs` | ゲーム終了処理 |
| `HoldEToReturnStart.cs` | E キー長押しでタイトル（Start）へ戻る |
| `LoopMoveBetweenTwoPoints.cs` | 2 点間を往復するオブジェクト移動 |

### 2. `Assets/Images/3Dモデル用script/` — メインゲームロジック

カテゴリ別に整理されています。

#### `プレイヤー/` — 自転車操作・状態

| ファイル | 役割 |
|----------|------|
| `PlayerMove.cs` | 前進・マウス左右で旋回・左/右クリックでブレーキ |
| `PlayerViolationStateHub.cs` | 逆走・歩道違反などを集約して `PlayerViolationState` を更新 |
| `PlayerRoadTravelState.cs` | 走行方向（逆走判定用） |
| `PlayerHandSignalState.cs` | ハンドサイン状態 |
| `BicycleLightToggle.cs` | 中クリックでライト ON/OFF、ホイールでベル |
| `ResetOutMap.cs` | マップ外落下時のリセット |
| `Gemeover.cs` | 衝突タグ（Car / Walker / Enemy）によるゲームオーバー |

#### `違反判定/` — 交通違反の検知

| ファイル | 役割 |
|----------|------|
| `WrongWayRoadMonitor.cs` | 逆走判定 |
| `SidewalkOnlyTextColor.cs` | 歩道走行判定 |
| `ShingouMushi.cs` | 信号無視（赤信号横断）判定 |
| `PlayerViolationState.cs` | 違反状態の静的集約（逆走・歩道・信号・無灯火など） |
| `ViolationTimes.cs` | 違反回数の管理 |
| `ViolationFineAmountDisplay.cs` | 反則金テキスト表示 |

#### `Police/` — 警察・パトロール

| ファイル | 役割 |
|----------|------|
| `PoliceLineOfSightCatch.cs` | 視界内＋違反中の捕獲、警告 UI・反則金表示 |
| `PoliceLineOfSightState.cs` | 警察視界状態の集約 |
| `PoliceTargetLineOfSightProbe.cs` | 視野角・レイキャストによる視界判定 |
| `RotatingPoliceVisionWatcher.cs` | 回転する警察の視界 |
| `MobTrafficPause.cs` | 捕獲中の車・歩行者の一時停止 |
| `PatrolTrafficResume.cs` | パトロール再開 |
| `UnlitBicyclePoliceWarningMark.cs` | 夜間無灯火の警告 |
| その他 | 警告アイコン、距離ベース SE など |

#### `歩行者など/` — NPC 歩行

| ファイル | 役割 |
|----------|------|
| `PatrolWaypoints.cs` | ウェイポイント巡回 |
| `PatrolWaypointsRandom.cs` | ランダム巡回 |
| `PatrolWaypointsBranchRandom.cs` | 分岐付きランダム巡回（歩行者ベル違反連携） |
| `PedestrianBellObjectiveUi.cs` | 歩行者ベル目標 UI |

#### `OBJ/` — 環境・オブジェクト

| ファイル | 役割 |
|----------|------|
| `DayNightCycleController.cs` | 昼→夕→夜→朝のサイクル（環境光・太陽・フォグ） |
| `CrosswalkFourWayTrafficController.cs` | 四方向交差点の信号制御 |
| `Shinngoukichenge.cs` | 信号機の表示切り替え |
| `NearestCarDistanceLoopVolume.cs` | 近くの車に応じた SE 音量 |

#### ルート直下

| ファイル | 役割 |
|----------|------|
| `Timer.cs` | ゲーム内経過時間（静的 `timer` / `isRunning`） |
| `TimeText.cs` | タイマーの UI 表示 |

### 3. `Assets/Editor/` — エディタツール

| ファイル | 役割 |
|----------|------|
| `MissingScriptCleaner.cs` | シーン内の Missing Script を一括削除するメニュー |

---

## シーン一覧

ビルド設定（`ProjectSettings/EditorBuildSettings.asset`）に登録されているシーンです。

| シーン | 想定用途 |
|--------|----------|
| `WarningNote.unity` | 注意事項・警告表示 |
| `Start.unity` | タイトル画面 |
| `GameDescription.unity` | ゲーム説明 |
| `SampleScene.unity` | **メインゲームプレイ** |
| `DiedScene.unity` | 車（`Car` タグ）との接触で死亡 |
| `GameOverScene.unity` | 歩行者・敵との接触でゲームオーバー |
| `Clear.unity` | クリア |
| `PoliceOver.unity` / `PoliceOver2.unity` | 警察に捕まった結果画面 |

---

## ゲームの基本ロジック

### 操作

| 操作 | 動作 |
|------|------|
| マウス左右移動 | 自転車の進行方向を変更（カーソルロック中） |
| 左/右クリック長押し | ブレーキ |
| マウス中クリック | 自転車ライト ON/OFF |
| マウスホイール | ベル |
| E キー長押し | タイトルへ戻る（`HoldEToReturnStart` 付きシーン） |

自転車は常に前進し、`PlayerMove` が Rigidbody で速度を制御します。

### 違反の種類

`PlayerViolationState` が以下を集約します。

- **逆走** — `WrongWayRoadMonitor`
- **歩道走行** — `SidewalkOnlyTextColor`
- **信号無視** — `ShingouMushi`（赤信号横断ゾーン）
- **歩行者へのベル** — パトロール NPC 連携
- **夜間無灯火** — `DayNightCycleController` の夜フェーズ中にライト OFF

### 警察システム

```
各違反スクリプト
    ↓ 違反中 ∧ 警察視界内
PoliceLineOfSightCatch.RequestTryCatchWhenViolationVisibleToPolice()
    ↓
警告 UI 表示・タイマー停止・Mob 一時停止・反則金表示
    ↓ 「戻る」ボタン
ゲーム再開（クールダウン後に再捕獲可能）
```

- 視界判定: `PoliceTargetLineOfSightProbe`（視野角・レイ）
- 捕獲中は `Timer.isRunning = false` でタイマー停止
- 反則金は違反種別ごとに Inspector で設定（例: 一般 6000 円、ベル 3000 円、無灯火 5000 円）

### ゲームオーバー条件

| 条件 | 遷移先 |
|------|--------|
| `Car` タグとの接触 | `DiedScene` |
| `Walker` / `Enemy` タグとの接触 | `GameOverScene` |
| 警察に捕獲（シーン遷移設定による） | `PoliceOver` 系 |

### タイマー・スコア

- `Timer` が `Update` で経過時間を加算
- クリア時などに `ScoreManager.SaveBestTime()` でベストタイムを `PlayerPrefs` に保存
- `BestTime` が UI に `MM:SS` 形式で表示

### 昼夜サイクル

`DayNightCycleController` が **昼 → 夕 → 夜 → 朝** をループします。  
夜フェーズ中はライトを消したまま走行すると無灯火違反として警察連携の対象になります。

---

## 開発環境のセットアップ

1. [Unity Hub](https://unity.com/download) から **Unity 2022.3.62f3** をインストール
2. 本リポジトリをクローンまたはダウンロード
3. Unity Hub で `2026AOKIPPU` フォルダを **Add project from disk** で開く
4. メインシーン `Assets/Scenes/SampleScene.unity` を開いて Play

### ビルド

**File → Build Settings** から対象プラットフォームを選びビルドしてください。  
上記「シーン一覧」のシーンが Build Settings に登録済みです。

---

## 補足

- 3D モデル・マテリアル・画像は `Assets/Images/` 配下
- スカイボックスは `Assets/Fantasy Skybox FREE/`（外部無料アセット）
- 地形データ: `Assets/New Terrain.asset`
- タグ（`Player` / `Car` / `Walker` / `Enemy` / `ATARI` など）は Unity の Tag 設定で定義されています。衝突・SE 判定に使用

---

## ライセンス・クレジット

- Fantasy Skybox FREE — サードパーティアセット（`Assets/Fantasy Skybox FREE/Readme.txt` 参照）
