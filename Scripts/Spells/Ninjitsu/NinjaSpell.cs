using System;

namespace Server.Spells.Ninjitsu
{
    public abstract class NinjaSpell : Spell
    {
        public NinjaSpell(Mobile caster, Item scroll, SpellInfo info)
            : base(caster, scroll, info)
        {
        }

        public abstract double RequiredSkill { get; }
        public abstract int RequiredMana { get; }
        public override SkillName CastSkill => SkillName.Ninjitsu;
        public override SkillName DamageSkill => SkillName.Ninjitsu;
        public override bool RevealOnCast => false;
        public override bool ClearHandsOnCast => false;
        public override bool ShowHandMovement => false;
        public override bool BlocksMovement => false;
        public override int CastRecoveryBase => 7;

        public override bool CheckCast()
        {
            int mana = this.ScaleMana(this.RequiredMana);

            if (!base.CheckCast())
                return false;

            if (this.Caster.Skills[this.CastSkill].Value < this.RequiredSkill)
            {
                string args = String.Format("{0}\t{1}\t ", this.RequiredSkill.ToString("F1"), this.CastSkill.ToString());
                this.Caster.SendLocalizedMessage(1063013, args); // You need at least ~1_SKILL_REQUIREMENT~ ~2_SKILL_NAME~ skill to use that ability.
                return false;
            }
            else if (this.Caster.Mana < mana)
            {
                this.Caster.SendLocalizedMessage(1060174, mana.ToString()); // You must have at least ~1_MANA_REQUIREMENT~ Mana to use this ability.
                return false;
            }

            return true;
        }

        public override bool CheckFizzle()
        {
            int mana = this.ScaleMana(this.RequiredMana);

            if (this.Caster.Skills[this.CastSkill].Value < this.RequiredSkill)
            {
                this.Caster.SendLocalizedMessage(1063352, this.RequiredSkill.ToString("F1")); // You need ~1_SKILL_REQUIREMENT~ Ninjitsu skill to perform that attack!
                return false;
            }
            else if (this.Caster.Mana < mana)
            {
                this.Caster.SendLocalizedMessage(1060174, mana.ToString()); // You must have at least ~1_MANA_REQUIREMENT~ Mana to use this ability.
                return false;
            }

            if (!base.CheckFizzle())
                return false;

            this.Caster.Mana -= mana;

            return true;
        }

        public override void GetCastSkills(out double min, out double max)
        {
            min = this.RequiredSkill - 12.5;	//Per 5 on friday 2/16/07
            max = this.RequiredSkill + 37.5;
        }

        public override int GetMana()
        {
            return 0;
        }
    }
}