# DesignData 圖檔命名與放置說明

本文件說明倉庫根目錄下 `DesignData` 內各 JSON 與 **Unity 用圖檔** 的對應方式。  
圖檔實體請放在 **`GameClient/Assets/`** 底下；JSON 裡的路徑通常寫成以 `Assets/` 開頭的專案內相對路徑（正斜線 `/`）。

---

## 1. 路徑與載入慣例

- **相對於 Unity 專案**：`GameClient` 即 Unity 專案根目錄，故 `Assets/Textures/...` 對應磁碟上的 `GameClient/Assets/Textures/...`。
- **字串格式**：建議統一 **`Assets/.../檔名.png`**、`/` 為路徑分隔，並附上副檔名（支援 `.png` / `.jpg` / `.jpeg`）。客戶端以 `SafeSpriteLoader` 依此字串載入 Sprite。
- **`DesignData` 本身**：純資料（JSON），**不放貼圖**；貼圖一律在 `GameClient/Assets`。

---

## 2. 各 JSON 與圖檔對照總覽

| 路徑 | JSON 是否含「圖片路徑」類欄位 | 圖檔如何對上 |
|------|------------------------------|--------------|
| `01_Monsters/monsters.json` | ✅ `圖片路徑`、`圖示路徑` | 以欄位內容為準；見下文「魔物」 |
| `01_Monsters/drop_rates.json` | ✅ `圖片路徑` | **每列一素材**；見 §5：`Assets/Textures/Items/{素材編號}.png` |
| `02_Equipment/equipment.json` | ✅ `圖片路徑`、`圖示路徑` | **武器**：見 §4；合成走較難掉落 **MAT_xx_03〜05** |
| `02_Equipment/armor.json` | ✅ `圖片路徑`、`圖示路徑` | **護甲**：見 §4；合成走較易得 **MAT_xx_01、02**；圖 **`ARM_{三位}.png`** |
| `02_Equipment/skills.json` | ✅ `圖片路徑` | 見 §5：`Assets/Textures/Skills/{技能編號}.png` |
| `02_Equipment/upgrade_rules.json` | ✅ `圖片路徑` | 見 §5：預設與裝備同檔 **`Assets/Textures/Equipment/{裝備編號}.png`** |
| `03_Combat/battle_background_labels.json` | ❌（僅 HUD 文案對照） | **不決定檔名**；`labels` 的 **key** 須與地圖名或 `CS□□R□□` 等一致才可替換抬頭顯示，見「戰鬥遠景標籤」 |
| `03_Combat/combat_tuning.json` | ❌ | 無 |
| `03_Combat/weapon_movesets.json` | ❌ | 無 |
| `04_Items/materials.json` | ✅ `圖片路徑` | 見 §5.1：`Assets/Textures/Items/{素材編號}.png`（與掉落表同名素材可走同檔） |
| `04_Items/paintballs.json` | ✅ `圖片路徑` | 見 §5.1：`Assets/Textures/Items/{道具編號}.png` |
| `04_Items/monster_traces.json` | ✅ `圖片路徑` | 見 §5.1：`Assets/Textures/Traces/{痕跡編號}.png` |
| `05_Systems/quests.json` | ❌ | **`地圖` 字串** 對應戰鬥遠景檔名，見「任務 × 戰鬥背景」 |
| `05_Systems/pets.json` | ✅ `圖片路徑` | 見 §5.1：`Assets/Textures/Pets/{寵物編號}.png` |
| `05_Systems/canteen.json` | ✅ `圖片路徑` | 見 §5.1：`Assets/Textures/Canteen/{料理編號}.png` |

---

## 3. JSON 明確載明的圖：`monsters.json`

每筆魔物可取兩張圖：

| 欄位 | 目前資料中的慣例 | 建議實際放置（在 `GameClient/Assets/` 下） |
|------|------------------|--------------------------------------------|
| `圖片路徑` | `Assets/Textures/Monsters/MON_{三位}_全身圖.png` | `Textures/Monsters/MON_001_全身圖.png`（例） |
| `圖示路徑` | `Assets/UI/Icons/Monsters/MON_{三位}_圖示.png` | `UI/Icons/Monsters/MON_001_圖示.png`（例） |

- **`魔物編號`**（如 `MON_001`）應與檔名中的編號一致。  
- 部分預覽程式在 `圖片路徑` 載入失敗時，會再嘗試 **`Assets/Textures/Monsters/{魔物編號}.png`**（例如 `MON_001.png`）。若要依賴此 fallback，可額外放一張與編號同名的 PNG；正式美術仍以 JSON 內 **`圖片路徑`** 為準較清楚。

---

## 4. 武器：`equipment.json` 與護甲：`armor.json`

兩張表 **`MON_k`／`MAT_k_xx` 對齊**（`WEP_{k三位}`、`ARM_{k三位}`）。

### 4.1 與掉落表的對應（企劃邏輯）

| 表 | 武器 `equipment.json` | 護甲 `armor.json` |
|---|-------------------------|-------------------|
| 合成 **主要**消耗 | **MAT_xx_03／04／05**（多半是「破壞部位」「切尾巴」與稀有） | **MAT_xx_01／02**（多半是「基本擊殺」） |
| 圖檔路徑欄（預設） | `Assets/Textures/Equipment/WEP_{三位}.png` | `Assets/Textures/Equipment/ARM_{三位}.png` |

- **武器強化** `upgrade_rules.json`：**僅對應 WEP**。花費中的素材已由 **易得尾碼（01／02）映到難得尾碼（03／04）**，並合併同一步驟內重複素材列。  

### 4.2 重新產生配方（資料維護）

若之後調整武器列再跑批次，請用：

**`tools/sync_weapon_armor_material_tiers.py`**

可依 `monsters.json` 與當時的 `equipment.json` 規則，重寫武器合成、護甲 `armor.json`、以及武器的 `upgrade_rules` 素材尾碼邏輯。

---

## 5. `drop_rates.json`／`skills.json`／`upgrade_rules.json` 的 `圖片路徑`

三份表根陣列的**每一列**皆可填 `圖片路徑`（已由資料產生完備，可自行改路徑或檔名以配合美術資產）：

| 檔案 | 欄位位置 | 預設規則（可自行覆寫） | 建在 `GameClient/Assets/` 下 |
|------|----------|------------------------|------------------------------|
| `01_Monsters/drop_rates.json` | 每列一筆素材掉落 | `Assets/Textures/Items/{素材編號}.png` | `Textures/Items/MAT_001_01.png`（例） |
| `02_Equipment/skills.json` | 每個技能條目前段 | `Assets/Textures/Skills/{技能編號}.png` | `Textures/Skills/SKL_001.png`（例） |
| `02_Equipment/upgrade_rules.json` | 每把武器的升級規則條目前段 | `Assets/Textures/Equipment/{裝備編號}.png`（與 `equipment.json` 武器圖同路徑，可共用一張 PNG） | `Textures/Equipment/WEP_001.png`（例） |

- **資料筆數**：掉落表列數與表中列數一致（600 素材列／40 技能／120 武器規則，隨資料表增減而定）。同一 `素材編號` 若在掉落表重複列出，可多列指向同一 `圖片路徑`；實際裝 PNG 仍以檔案路徑為準。

---

## 5.1 素材、染色球、痕跡、寵物、貓飯（`materials`／`paintballs`／`monster_traces`／`pets`／`canteen`）

每筆資料在 **`名稱`** 之後帶 **`圖片路徑`**（可自改字串以配合美術）：

| 檔案 | 預設 `圖片路徑` 規則 | 建在 `GameClient/Assets/` 下（例） |
|------|----------------------|-------------------------------------|
| `04_Items/materials.json` | `Assets/Textures/Items/{素材編號}.png` | `Textures/Items/MAT_001_01.png`、`ITM_001.png` |
| `04_Items/paintballs.json` | `Assets/Textures/Items/{道具編號}.png` | `Textures/Items/ITM_PB_001.png` |
| `04_Items/monster_traces.json` | `Assets/Textures/Traces/{痕跡編號}.png` | `Textures/Traces/TRC_001.png` |
| `05_Systems/pets.json` | `Assets/Textures/Pets/{寵物編號}.png` | `Textures/Pets/PET_CAT_001.png` |
| `05_Systems/canteen.json` | `Assets/Textures/Canteen/{料理編號}.png` | `Textures/Canteen/FD_001.png` |

- **`materials.json` 與 `drop_rates.json`**：若 `素材編號` 相同，建議兩處 **`圖片路徑` 指向同一路徑**並共用同一 PNG，以免背包與結算圖不一致。

---


## 6. 任務 × 戰鬥遠景（`quests.json` 的 `地圖`）

`quests.json` 沒有圖片欄位，但 **`地圖`** 會用來組出戰鬥遠景檔名。

- **預設規則**（`BattleBackgroundDisplay`）：  
  - 相對路徑格式：`UI/Backgrounds/Battle/{0}_背景.png`  
  - 實際載入會補上 `Assets/` →  
    **`Assets/UI/Backgrounds/Battle/{地圖}_背景.png`**
- **範例**：`"地圖": "古代樹森林"` → 檔案 **`GameClient/Assets/UI/Backgrounds/Battle/古代樹森林_背景.png`**
- **星級構圖遠景**（與任務輪播搭配時）：檔名 **`CS{星級兩位}R{構圖兩位}_背景.png`**，例如星級 1、第 3 張構圖 → **`CS01R03_背景.png`**，完整路徑：  
  **`Assets/UI/Backgrounds/Battle/CS01R03_背景.png`**

新增地區時：**`quests.json` 的 `地圖` 字串** 必須與 **`{地圖}_背景.png`** 的主檔名（不含副檔名）完全一致。

---

## 7. 戰鬥遠景 HUD 標籤（`battle_background_labels.json`）

- 檔案內 `labels` 為 **對照表**：key → 畫面上顯示的名稱。  
- **key** 請使用與載入邏輯相同的識別，例如：`古代樹森林`、`CS01R01`（與上節檔名／星級構圖碼對應）。  
- **不負責**指定 PNG 放在哪裡；圖檔仍依 **§6（任務 × 戰鬥遠景）**命名與放置。

---

## 8. 村莊／介面全畫面背景（多數未定義在此 JSON）

`VillageBackgroundUiDisplay` 等元件預設格式為：

- **`Assets/UI/Backgrounds/Village/{場景鍵}_背景.png`**

例如：`遊戲標題` → `UI/Backgrounds/Village/遊戲標題_背景.png`。  
此類鍵不一定出現在 `DesignData` JSON 中，但命名規則與戰鬥背景相同：**鍵 + `_背景.png`**。

---

## 9. 其他未定義於 JSON 的清單資料

前述 §2、§5、§5.1 所列檔案已含 **`圖片路徑`** 或可推導圖檔（如 **`quests.json` 的 `地圖`**）。尚未在資料表設圖檔路徑的（例如 **`weapon_movesets.json`**、**`combat_tuning.json`**）仍由程式／Prefab 側處理；若要為更多表加美術對照，比照 **`圖片路徑`** 欄即可。

先前若只靠「慣例路徑」推導：`materials`／染色球／魔物掉落（`MAT_`／`ITM_`）已統一走 **`Textures/Items/`**，痕跡另用 **`Textures/Traces/`**。

---

## 10. 快速檢查清單

- [ ] PNG 是否在 **`GameClient/Assets/...`** 下，且已讓 Unity 匯入（看得到 `.meta`）  
- [ ] JSON 內路徑是否為 **`Assets/...`** 且分隔為 **`/`**  
- [ ] 武器／裝備：`WEP_XXX.png` 與 **`裝備編號`** 一致  
- [ ] 背包素材／共通道具圖示：`Textures/Items/{素材編號}.png` ↔ `materials.json`；與 `drop_rates` 同 **`素材編號`** 請對齊同一 `圖片路徑`  
- [ ] 染色球：`Textures/Items/{道具編號}.png` ↔ `paintballs.json`  
- [ ] 掉落素材（結算）：`Textures/Items/{素材編號}.png` ↔ `drop_rates.json`  
- [ ] 技能圖：`Textures/Skills/{技能編號}.png` ↔ `skills.json`  
- [ ] 痕跡圖：`Textures/Traces/{痕跡編號}.png` ↔ `monster_traces.json`  
- [ ] 寵物：`Textures/Pets/{寵物編號}.png` ↔ `pets.json`  
- [ ] 貓飯：`Textures/Canteen/{料理編號}.png` ↔ `canteen.json`  
- [ ] 魔物：`MON_XXX_全身圖.png`、`MON_XXX_圖示.png` 與 **`魔物編號`** 一致  
- [ ] 任務戰場：任務的 **`地圖`** ↔ `Battle/{地圖}_背景.png`  
- [ ] 星級遠景：`Battle/CS{S星兩位}R{R構圖兩位}_背景.png`

若需對照載入程式，可參考 `GameClient/Assets/Scripts/UI/SafeSpriteLoader.cs`、`BattleBackgroundDisplay.cs`、`VillageBackgroundUiDisplay.cs`。
