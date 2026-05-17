import fs from 'fs';
import path from 'path';

// 1. Define 20 Passive Ailments (被動屬性)
const PASSIVE_NAMES = [
    { name: "火傷", desc: "引燃獵人本體造成持續灼燒" },
    { name: "水流窒息", desc: "水流纏身減緩耐性並持續窒息" },
    { name: "雷電感電", desc: "電流麻痺神經造成持續電擊" },
    { name: "冰凍遲緩", desc: "極寒凍結肢體並造成凍傷" },
    { name: "狂龍病毒", desc: "感染狂龍病毒，持續侵蝕血量" },
    { name: "裂傷", desc: "劇烈撕裂傷口，移動時大量流血" },
    { name: "劇毒", desc: "猛烈毒素入骨，造成持續劇毒傷害" },
    { name: "睡眠", desc: "強烈催眠花粉，使獵人陷入昏睡" },
    { name: "麻痺", desc: "高壓電流或神經毒素，造成完全麻痺" },
    { name: "爆破異常", desc: "黏著易爆黏菌，積累到一定程度爆炸" },
    { name: "防禦下降", desc: "腐蝕性酸液，大幅降低獵人防禦力" },
    { name: "龍蝕", desc: "古龍威壓侵蝕，削弱所有屬性抗性" },
    { name: "龍壓", desc: "重力龍壓，令獵人行動困難並窒息" },
    { name: "蝕傷", desc: "黑暗侵蝕傷口，持續造成暗屬性扣血" },
    { name: "瘴蝕", desc: "瘴氣山谷特有瘴氣，持續削減生命上限" },
    { name: "重力壓制", desc: "重力場改變，強行壓迫心肺扣血" },
    { name: "畏懼", desc: "魔物咆哮產生的恐懼，削弱意志扣血" },
    { name: "流血", desc: "普通利爪撕裂，造成持續性流血" },
    { name: "精神衰弱", desc: "精神受到震盪，持續衰弱扣血" },
    { name: "灼燒", desc: "高溫熱浪席捲，使獵人體溫持續上升" }
];

// Define 5 Elements with 20 Active Skills each (Total 100 skills)
const ELEMENT_SKILLS = {
    "火": [
        "火之吐息", "烈焰奔騰", "炎能炮", "流星火雨", "煉獄衝擊", "灼熱之咬", "岩漿噴射", "日蝕光輪", "赤焰橫掃", "火爪突襲",
        "大爆發", "烈火強襲", "鳳凰衝天", "地獄火柱", "熔岩怒吼", "熔岩重擊", "燃燒巨爪", "爆炎亂舞", "焚天之怒", "業火咆哮"
    ],
    "水": [
        "水之波動", "驚濤駭浪", "水炮轟擊", "潮汐旋渦", "激流衝撞", "噴泉激射", "暴雨狂瀾", "海潮重擊", "激流護盾", "深淵之噬",
        "怒濤巨瀾", "狂瀾掃蕩", "水刃連斬", "冰泉爆發", "潮汐重槌", "水瀑吐息", "湧泉突刺", "激流水流", "深海重壓", "狂濤怒哮"
    ],
    "雷": [
        "落雷術", "雷霆萬鈞", "電光一閃", "閃電射線", "雷神之錘", "高壓電網", "雷電狂飆", "磁暴風圈", "電能奔湧", "雷爪撕裂",
        "雷鳴電閃", "極光電擊", "天雷怒號", "電磁風暴", "疾風迅雷", "雷光重槌", "雷電亂舞", "落雷風暴", "宙斯之怒", "雷爆重擊"
    ],
    "冰": [
        "冰霜新星", "寒冰箭", "極寒暴風", "冰刺突襲", "冰崩地裂", "絕對零度", "霜凍吐息", "冰刃橫掃", "碎冰穿擊", "寒冬咆哮",
        "冰封王座", "極光吐息", "冰晶爆轟", "狂風暴雪", "冰山重擊", "霜凍強襲", "雪崩怒吼", "冰柱連射", "凍土之噬", "冰帝重錘"
    ],
    "龍": [
        "龍之怒吼", "龍星群", "逆鱗之怒", "龍之吐息", "滅世衝擊", "龍牙撕咬", "暗黑射線", "混沌之爪", "破滅之光", "狂暴突襲",
        "古龍威壓", "毀滅風暴", "龍爪粉碎", "深淵重擊", "暗龍突刺", "終焉射線", "狂龍亂舞", "霸王衝擊", "龍息重槌", "黑龍咆哮"
    ],
    "無": [
        "飛撲壓制", "熊掌橫掃", "岩塊投擲", "泰山壓頂", "迴旋甩尾", "瘋狂抓撓", "蠻牛衝撞", "裂地猛擊", "震天咆哮", "狂暴突刺",
        "野蠻撕咬", "致命碎骨", "巨力踐踏", "鋼鐵護盾", "毀滅衝擊", "死亡翻滾", "音速突進", "無情痛擊", "狂怒連打", "地裂衝擊"
    ],
    "毒": [
        "劇毒吐息", "猛毒射線", "毒液噴灑", "致命毒霧", "劇毒泥沼", "毒刺連射", "腐蝕毒雨", "瘟疫爆發", "紫毒風暴", "毒液炸彈",
        "猛毒陷阱", "毒刺突襲", "腐化之觸", "劇毒之潮", "猛毒噴射", "毒牙撕咬", "瘴氣爆破", "致死毒霧", "毒液飛濺", "致命紫斑"
    ]
};

// Generate monster_skills.json master list
function generateMasterSkills() {
    const master = {
        "特殊被動屬性": [],
        "特殊招式列表": []
    };

    // Generate Passives (LV1 - LV4)
    PASSIVE_NAMES.forEach((item, index) => {
        const id = `PAS_${String(index + 1).padStart(3, '0')}`;
        const levels = {};
        
        for (let lv = 1; lv <= 4; lv++) {
            // Half of the status effects have linked image paths, the other half have empty image paths
            // to demonstrate: "未連結圖片時 先以文字替代 有圖片時 以圖片加文字"
            const hasImage = index < 10; 
            const imagePath = hasImage ? `Assets/Textures/Skills/${id}_LV${lv}.png` : "";

            levels[`LV${lv}`] = {
                "異常屬性": `${item.name} LV${lv}`,
                "每秒傷害": Math.round((10 + index * 2) * (1 + lv * 0.4)),
                "觸發機率": parseFloat((0.15 + lv * 0.05).toFixed(2)),
                "持續時間秒": parseFloat((5.0 + lv * 1.5).toFixed(1)),
                "傷害加成比例": parseFloat((1.0 + lv * 0.05).toFixed(2)),
                "圖片路徑": imagePath,
                "描述": `${item.desc} (等級 ${lv})`
            };
        }

        master["特殊被動屬性"].push({
            "編號": id,
            "名稱": item.name,
            "分級內容": levels
        });
    });

    // Generate Active Skills (LV1 - LV4)
    let activeIdCounter = 1;
    const placeholderVisuals = ["球", "方塊", "閃電", "菱形"];

    Object.entries(ELEMENT_SKILLS).forEach(([element, skills]) => {
        skills.forEach((skillName) => {
            const id = `ACT_${String(activeIdCounter++).padStart(3, '0')}`;
            const levels = {};

            // Random but deterministic characteristics based on skill name hash
            const nameHash = skillName.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
            // ✦ 無屬性技能預設為近戰肉搏招式，不給予投射物，迫使 AI 貼身攻擊
            const isMelee = element === "無"; 
            const visualType = isMelee ? "" : placeholderVisuals[nameHash % placeholderVisuals.length];
            const baseSpeed = 4.5 + (nameHash % 5) * 1.2;
            const baseRadius = 0.18 + (nameHash % 4) * 0.08;
            const baseDist = isMelee ? 2.5 + (nameHash % 3) * 0.5 : 3.0 + (nameHash % 7) * 1.2; // 近戰距離縮短

            for (let lv = 1; lv <= 4; lv++) {
                levels[`LV${lv}`] = {
                    "名稱": `${skillName} LV${lv}`,
                    "屬性": element,
                    "傷害對普攻倍率": parseFloat((0.65 + lv * 0.35 + (nameHash % 3) * 0.1).toFixed(2)),
                    "攻擊距離": parseFloat((baseDist + lv * 0.5).toFixed(1)),
                    "使用權重": 10 + (nameHash % 15) + lv * 2,
                    "冷卻秒": parseFloat((4.5 - lv * 0.5 + (nameHash % 3) * 0.5).toFixed(1)),
                    "投射物型別": visualType,
                    "投射物速度": isMelee ? 0 : parseFloat((baseSpeed * (1 + lv * 0.15)).toFixed(1)),
                    "投射物半徑": isMelee ? 0 : parseFloat((baseRadius * (1 + lv * 0.2)).toFixed(2)),
                    "圖片路徑": `Assets/Textures/Skills/${id}_LV${lv}.png`
                };
            }

            master["特殊招式列表"].push({
                "編號": id,
                "屬性": element,
                "招式名稱": skillName,
                "分級內容": levels
            });
        });
    });

    return master;
}

// Main logic to update monsters.json and output monster_skills.json
function main() {
    const masterSkills = generateMasterSkills();
    const skillsPath = 'C:/Users/liskyo/Desktop/MonsterHunter_2026/DesignData/01_Monsters/monster_skills.json';
    fs.writeFileSync(skillsPath, JSON.stringify(masterSkills, null, 2), 'utf8');
    console.log(`[Success] Master skills list generated at: ${skillsPath}`);

    // Load monsters.json
    const monstersPath = 'C:/Users/liskyo/Desktop/MonsterHunter_2026/DesignData/01_Monsters/monsters.json';
    const monsters = JSON.parse(fs.readFileSync(monstersPath, 'utf8'));

    // Process each monster based on its Star Rating (星級)
    monsters.forEach((monster, mIndex) => {
        const star = monster.星級 || 1;
        const elements = monster.屬性 || ["無"];
        // Pick primary element
        let primaryElement = elements[0];
        if (!ELEMENT_SKILLS[primaryElement]) {
            // Cycle element deterministically if not standard
            const keys = Object.keys(ELEMENT_SKILLS);
            primaryElement = keys[mIndex % keys.length];
        }

        // Get matching elements skills and passives
        const matchActives = masterSkills.特殊招式列表.filter(s => s.屬性 === primaryElement);
        const allPassives = masterSkills.特殊被動屬性;

        // Deterministic offset based on monster index
        let actOffset = mIndex;
        let pasOffset = mIndex;

        let passivesToInject = [];
        let activesToInject = [];

        if (star >= 1 && star <= 2) {
            // 1-2星: 1 * Passive LV1 + 3 * Active LV1
            const p1 = allPassives[pasOffset % allPassives.length].分級內容["LV1"];
            passivesToInject.push(p1);

            for (let i = 0; i < 3; i++) {
                const s = matchActives[(actOffset + i) % matchActives.length].分級內容["LV1"];
                activesToInject.push(s);
            }
        } 
        else if (star >= 3 && star <= 4) {
            // 3-4星: 1 * Passive LV2 + 1 * Active LV1 + 2 * Active LV2
            const p1 = allPassives[pasOffset % allPassives.length].分級內容["LV2"];
            passivesToInject.push(p1);

            const s1 = matchActives[actOffset % matchActives.length].分級內容["LV1"];
            activesToInject.push(s1);

            for (let i = 1; i <= 2; i++) {
                const s2 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV2"];
                activesToInject.push(s2);
            }
        }
        else if (star >= 5 && star <= 6) {
            // 5-6星: 1 * Passive LV3 + 2 * Active LV2 + 2 * Active LV3
            const p1 = allPassives[pasOffset % allPassives.length].分級內容["LV3"];
            passivesToInject.push(p1);

            for (let i = 0; i < 2; i++) {
                const s2 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV2"];
                activesToInject.push(s2);
            }
            for (let i = 2; i < 4; i++) {
                const s3 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV3"];
                activesToInject.push(s3);
            }
        }
        else if (star >= 7 && star <= 8) {
            // 7-8星: 1 * Passive LV3 + 3 * Active LV3 + 2 * Active LV2
            const p1 = allPassives[pasOffset % allPassives.length].分級內容["LV3"];
            passivesToInject.push(p1);

            for (let i = 0; i < 3; i++) {
                const s3 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV3"];
                activesToInject.push(s3);
            }
            for (let i = 3; i < 5; i++) {
                const s2 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV2"];
                activesToInject.push(s2);
            }
        }
        else if (star === 9) {
            // 9星: 2 * Passive LV4 + 3 * Active LV4 + 2 * Active LV3
            const p1 = allPassives[pasOffset % allPassives.length].分級內容["LV4"];
            const p2 = allPassives[(pasOffset + 1) % allPassives.length].分級內容["LV4"];
            passivesToInject.push(p1, p2);

            for (let i = 0; i < 3; i++) {
                const s4 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV4"];
                activesToInject.push(s4);
            }
            for (let i = 3; i < 5; i++) {
                const s3 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV3"];
                activesToInject.push(s3);
            }
        }
        else if (star >= 10) {
            // 10星: 2 * Passive LV4 + 6 * Active LV4
            const p1 = allPassives[pasOffset % allPassives.length].分級內容["LV4"];
            const p2 = allPassives[(pasOffset + 1) % allPassives.length].分級內容["LV4"];
            passivesToInject.push(p1, p2);

            for (let i = 0; i < 6; i++) {
                const s4 = matchActives[(actOffset + i) % matchActives.length].分級內容["LV4"];
                activesToInject.push(s4);
            }
        }

        // Format mapping back to Game JSON structure
        monster.魔物攻擊內容.特殊攻擊 = passivesToInject.map(p => ({
            "異常屬性": p.異常屬性,
            "每秒傷害": p.每秒傷害,
            "觸發機率": p.觸發機率,
            "持續時間秒": p.持續時間秒,
            "圖片路徑": p.圖片路徑 // Supported in C# after schema update!
        }));

        monster.魔物攻擊內容.特殊招式 = activesToInject.map(a => ({
            "名稱": a.名稱,
            "傷害對普攻倍率": a.傷害對普攻倍率,
            "攻擊距離": a.攻擊距離,
            "使用權重": a.使用權重,
            "冷卻秒": a.冷卻秒,
            "投射物型別": a.投射物型別,
            "投射物速度": a.投射物速度,
            "投射物半徑": a.投射物半徑
        }));
    });

    fs.writeFileSync(monstersPath, JSON.stringify(monsters, null, 2), 'utf8');
    console.log(`[Success] Successfully updated all ${monsters.length} monsters in monsters.json with LV1-LV4 active and passive skills matching their star ratings!`);
}

main();
