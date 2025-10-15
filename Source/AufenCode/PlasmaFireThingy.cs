using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;

namespace AufenCode
{
    public static class PlasmaFireUtility
    {
        /// <summary>
        /// Attempts to start a PlasmaFire in the given cell.
        /// </summary>
        /// <param name="c">Target cell</param>
        /// <param name="map">Map reference</param>
        /// <param name="fireSize">Initial fire size (vanilla defaults around 0.1 - 0.2)</param>
        /// <param name="instigator">Thing responsible for starting the fire</param>
        /// <returns>True if a fire was successfully started</returns>
        public static bool TryStartPlasmaFireIn(IntVec3 c, Map map, float fireSize, Thing instigator = null)
        {
            // Ensure coordinates are valid
            if (!c.InBounds(map))
                return false;

            // Don't place on impassable or non-flammable terrain
            if (!FireUtility.ChanceToStartFireIn(c, map).Equals(1f) && !Rand.Chance(FireUtility.ChanceToStartFireIn(c, map)))
                return false;

            if (!c.GetTerrain(map).extinguishesFire)
                return false;

            // No duplicate fires
            if (c.GetFirstThing(map, ThingDef.Named("Fire")) != null)
                return false;

            // Actually spawn the PlasmaFire
            Fire newFire = (Fire)GenSpawn.Spawn(ThingDef.Named("Fire"), c, map, WipeMode.Vanish);
            newFire.fireSize = Mathf.Clamp(fireSize, 0.1f, 1.75f);
            newFire.instigator = instigator;

            return true;
        }
    }
}