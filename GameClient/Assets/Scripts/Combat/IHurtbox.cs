namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物「部位／受擊盒」契約：結算 <c>baseDamage × 部位倍率</c> 後交給根節點的 <see cref="MonsterHealth"/>。
    /// </summary>
    public interface IHurtbox
    {
        /// <param name="baseDamage">尚未乘部位倍率／肉質前的武器基礎傷害。</param>
        void ApplyWeaponHit(float baseDamage);
    }
}
