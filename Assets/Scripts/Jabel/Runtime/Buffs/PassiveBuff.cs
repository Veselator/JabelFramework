using UnityEngine;

namespace Jabel.Buffs
{
    /// <summary>
    /// Upgrade with a permanent effect and no timing: "+1 per click", "x2 income", "unlock big screen".
    /// Effects come from OnBought and/or derived values that read count('id').
    /// </summary>
    [CreateAssetMenu(menuName = "Jabel/Passive Buff", fileName = "PassiveBuff")]
    public class PassiveBuff : BaseBuff
    {
        public override bool IsActive => false;
    }
}
