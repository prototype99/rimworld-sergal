using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;

namespace AufenCode
{
    class AufenBetty
    {
            public static class RadialBurstUtility
            {
                public static void FireRadialBurst(IntVec3 center, Map map, ThingDef projectileDef, int count = 12, int maxRange = 12, Thing launcher = null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        // Random angle + distance
                        float angle = Rand.Range(0f, 360f);
                        float distance = Rand.Range(1f, maxRange);

                        // Convert to IntVec3 target
                        IntVec3 target = (center.ToVector3Shifted()
                                         + (Quaternion.Euler(0f, angle, 0f) * Vector3.forward) * distance)
                                         .ToIntVec3();

                        if (!target.InBounds(map))
                            continue;

                        // Always fire, even without LoS
                        Projectile proj = (Projectile)GenSpawn.Spawn(projectileDef, center, map);
                        proj.Launch(launcher, center.ToVector3Shifted(), target, target, ProjectileHitFlags.IntendedTarget);
                    }
                }
            }

    }
}
