namespace MonsterHunter.Combat
{
    public interface IDamageReceiver
    {
        void ApplyDamage(float amount, bool isCrit);
    }
}
