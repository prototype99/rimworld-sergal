using RimWorld;
using Verse;
using Verse.Sound;

namespace AufenCode
{
    class AufenBetty_Ability
    {
        public class CompAbilityEffect_RadialBurst : CompAbilityEffect
        {
            public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
            {
                Map map = parent.pawn.Map;
                IntVec3 cell = target.Cell;

                // Portal animation
                MoteMaker.MakeStaticMote(cell, map, ThingDefOf.Mote_Stun, 2f);
                SoundDefOf.PsycastPsychicPulse.PlayOneShot(new TargetInfo(cell, map));

                // Delay for effect
                int delayTicks = 60;
                Find.TickManager.DoLater(delayTicks, () =>
                {
                    AufenBetty.RadialBurstUtility.FireRadialBurst(cell, map, ThingDef.Named("MiniExplosionProjectile"), 12, 12, parent.pawn);
                });
            }
        }

    }
}
