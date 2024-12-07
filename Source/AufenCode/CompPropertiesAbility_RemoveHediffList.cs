using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace AufenCode
{
    public class CompProperties_AbilityRemoveHediffList : CompProperties_AbilityEffect
    {
        public List<HediffDef> hediffDefs = new List<HediffDef>();

        public bool applyToSelf;

        public bool applyToTarget;

        public CompProperties_AbilityRemoveHediffList()
        {
            compClass = typeof(CompAbilityEffect_RemoveHediffList);
        }
    }
}
