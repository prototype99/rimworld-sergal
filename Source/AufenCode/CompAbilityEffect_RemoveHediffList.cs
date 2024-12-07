using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace AufenCode
{
public class CompAbilityEffect_RemoveHediffList : CompAbilityEffect
{
    public new CompProperties_AbilityRemoveHediffList Props => (CompProperties_AbilityRemoveHediffList)props;

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);
        if (Props.applyToSelf)
        {
            RemoveHediffs(parent.pawn);
        }
        if (target.Pawn != null && Props.applyToTarget && target.Pawn != parent.pawn)
        {
            RemoveHediffs(target.Pawn);
        }
    }

    private void RemoveHediffs(Pawn pawn)
    {
        foreach (var hediffDef in Props.hediffDefs)
        {
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (firstHediffOfDef != null)
            {
                pawn.health.RemoveHediff(firstHediffOfDef);
            }
        }
    }
}
}
