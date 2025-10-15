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
    class AufenBetty_Projectiles
    {
        public class Projectile_MiniExplosion : Projectile
        {
            protected override void Impact(Thing hitThing, bool blockedByShield = false)
            {
                base.Impact(hitThing, blockedByShield);

                if (Map == null) return;

                GenExplosion.DoExplosion(
                    Position,
                    Map,
                    1.9f,                     // radius
                    DamageDefOf.Bomb,
                    launcher,
                    15,                       // damage
                    armorPenetration: 0.2f,
                    explosionSound: SoundDefOf.Explosion_FirefoamPopper
                );

                // Add visuals
                MoteMaker.MakeStaticMote(Position, Map, ThingDefOf.Mote_Stun, 1.2f);
                FleckMaker.Static(Position, Map, FleckDefOf.ExplosionFlash, 1.5f);
            }
        }

    }
}
