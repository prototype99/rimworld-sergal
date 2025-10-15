using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace AufenCode
{

    public class PlasmaFire : Fire
    {
        // Keep track of spread timing
        private int customTicksSinceSpread;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            // Start spread counter at a random offset so all fires don't sync
            customTicksSinceSpread = (int)(CustomSpreadInterval * Rand.Value);
        }

        // Custom spread interval - controls how often we try to spread
        private float CustomSpreadInterval
        {
            get
            {
                // ↓ Lower this number to spread *more often*
                float num = 50f - (fireSize - 1f) * 30f; // Vanilla is 150f, now faster
                if (num < 50f) num = 30f; // Minimum delay between spread attempts
                return num;
            }
        }

        protected override void TickInterval(int delta)
        {
            // Burn faster before normal logic runs
            // ↓ Increase this to make the fire grow faster
            fireSize += 0.0028f * delta; // Vanilla: 0.00055f

            // Our custom spread timing
            if (fireSize > 0.5f) // start spreading earlier than vanilla (which waits for > 1.0)
            {
                customTicksSinceSpread += delta;
                if (customTicksSinceSpread >= CustomSpreadInterval)
                {
                    CustomTrySpread();
                    customTicksSinceSpread = 0;
                }
            }

            // Call vanilla burning logic for damage, visuals, heat, etc.
            base.TickInterval(delta);
        }

        // Modified TrySpread to spawn PlasmaFire instead of vanilla Fire
        private void CustomTrySpread()
        {
            IntVec3 targetCell;
            bool closeSpread;

            if (Rand.Chance(0.9f))
            {
                targetCell = Position + GenRadial.ManualRadialPattern[Rand.RangeInclusive(1, 8)];
                closeSpread = true;
            }
            else
            {
                targetCell = Position + GenRadial.ManualRadialPattern[Rand.RangeInclusive(10, 20)];
                closeSpread = false;
            }

            if (!targetCell.InBounds(Map) || !Rand.Chance(FireUtility.ChanceToStartFireIn(targetCell, Map)))
                return;

            // Helper method: check if targetCell has Filth_Plasma
            bool HasPlasmaFilth(IntVec3 cell)
            {
                List<Thing> thingsAtCell = Map.thingGrid.ThingsListAt(cell);
                foreach (Thing t in thingsAtCell)
                {
                    if (t.def.defName == "Filth_Plasma")
                    {
                        return true;
                    }
                }
                return false;
            }

            if (closeSpread)
            {
                ThingDef fireDef = HasPlasmaFilth(targetCell) ? ThingDef.Named("PlasmaFire") : ThingDef.Named("Fire");
                Fire newFire = (Fire)GenSpawn.Spawn(fireDef, targetCell, Map, WipeMode.Vanish);
                newFire.fireSize = 0.2f;
                newFire.instigator = instigator;
            }
            else
            {
                FleckMaker.ThrowMicroSparks(DrawPos, Map);

                ThingDef fireDef = HasPlasmaFilth(targetCell) ? ThingDef.Named("PlasmaFire") : ThingDef.Named("Fire");
                Fire newFire = (Fire)GenSpawn.Spawn(fireDef, targetCell, Map, WipeMode.Vanish);
                newFire.fireSize = 0.1f;
                newFire.instigator = instigator;
            }
        }
    }

}