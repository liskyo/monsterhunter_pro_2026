namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物「部位／受擊盒」契約：<see cref="WeaponHitbox"/> 傳入<strong>基礎傷害</strong>，
    /// 由實作端依部位 <c>damageMultiplier</c> 結算後，再交給根節點的 <see cref="MonsterHealth"/>。
    /// </summary>
    public interface IHurtbox
    {
        /// <summary>由武器命中時呼叫；實作應計算 最終傷害 = baseDamage × 部位倍率 並轉交 <see cref="MonsterHealth"/>。</summary>
        /// <param name="source">觸發判定的武器攻擊盒（可用於未來判斷攻擊來源／屬性）。</param>
        /// <param name="baseDamage">武器設定的基礎傷害（未乘肉質前）。</param>
        void ApplyWeaponHit(WeaponHitbox source, float baseDamage);
    }
}
