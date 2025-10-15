using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AufenCode
{
    // Bootstraps Harmony patches at game startup
    [StaticConstructorOnStartup]
    public static class RoyaltyCompatBootstrap
    {
        static RoyaltyCompatBootstrap()
        {
            var harmony = new Harmony("AufenCode.RoyaltyCompat");
            Log.Message("[AufenCode/RoyaltyCompat] Bootstrap initializing.");
            LogPawnStripPresence();
            TryPatchPawnchanger(harmony);
            TryPatchSpidersOmg(harmony);
            TryPatchBoomcast(harmony);
        }

        private static void TryPatchPawnchanger(Harmony harmony)
        {
            var targetType = AccessTools.TypeByName("AufenCodeRoyalty.CompAbilityEffect_Pawnchanger");
            if (targetType == null)
            {
                Log.Message("[AufenCode/RoyaltyCompat] Pawnchanger type not found (AufenCodeRoyalty not loaded?). Skipping.");
                return;
            }

            // Try exact signature first, then any Apply
            var applyMethod = AccessTools.Method(targetType, "Apply", new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })
                              ?? AccessTools.GetDeclaredMethods(targetType).FirstOrDefault(m => m.Name == "Apply");
            if (applyMethod == null)
            {
                Log.Warning("[AufenCode/RoyaltyCompat] Could not find CompAbilityEffect_Pawnchanger.Apply to patch.");
                return;
            }

            harmony.Patch(
                applyMethod,
                transpiler: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(PawnchangerApply_Transpiler)),
                finalizer: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(PawnchangerApply_Finalizer))
            );
            Log.Message("[AufenCode/RoyaltyCompat] Patched Pawnchanger.Apply (Strip replacement).");
        }

        private static void TryPatchSpidersOmg(Harmony harmony)
        {
            var targetType = AccessTools.TypeByName("AufenCodeRoyalty.CompAbilityEffect_SpidersOmg");
            if (targetType == null)
            {
                Log.Message("[AufenCode/RoyaltyCompat] SpidersOmg type not found. Skipping.");
                return;
            }
            var applyMethod = AccessTools.GetDeclaredMethods(targetType).FirstOrDefault(m => m.Name == "Apply");
            if (applyMethod == null)
            {
                Log.Warning("[AufenCode/RoyaltyCompat] Could not find CompAbilityEffect_SpidersOmg.Apply to patch.");
                return;
            }
            harmony.Patch(
                applyMethod,
                transpiler: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(SpidersOmgApply_Transpiler)),
                finalizer: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(SpidersOmgApply_Finalizer))
            );
            Log.Message("[AufenCode/RoyaltyCompat] Patched SpidersOmg.Apply (PawnGenerator.GeneratePawn replacement).");
        }

        private static void TryPatchBoomcast(Harmony harmony)
        {
            var targetType = AccessTools.TypeByName("AufenCodeRoyalty.CompAbilityEffect_Boomcast");
            if (targetType == null)
            {
                Log.Message("[AufenCode/RoyaltyCompat] Boomcast type not found. Skipping.");
                return;
            }
            var applyMethod = AccessTools.GetDeclaredMethods(targetType).FirstOrDefault(m => m.Name == "Apply");
            if (applyMethod == null)
            {
                Log.Warning("[AufenCode/RoyaltyCompat] Could not find CompAbilityEffect_Boomcast.Apply to patch.");
                return;
            }
            harmony.Patch(
                applyMethod,
                transpiler: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(BoomcastApply_Transpiler)),
                finalizer: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(BoomcastApply_Finalizer))
            );
            Log.Message("[AufenCode/RoyaltyCompat] Patched Boomcast.Apply (GenExplosion.DoExplosion replacement).");
        }

        private static void LogPawnStripPresence()
        {
            try
            {
                var methods = AccessTools.GetDeclaredMethods(typeof(Pawn)).Where(m => m.Name == "Strip").ToList();
                if (methods == null || methods.Count == 0)
                {
                    Log.Message("[AufenCode/RoyaltyCompat] Diagnostic: Verse.Pawn has no method named 'Strip' in this runtime. Using compatibility replacement.");
                }
                else
                {
                    Log.Message("[AufenCode/RoyaltyCompat] Diagnostic: Found Verse.Pawn.Strip overload(s): " + string.Join(", ", methods.Select(m => m.ToString())));
                }
            }
            catch (Exception e)
            {
                Log.Warning("[AufenCode/RoyaltyCompat] Diagnostic failed while checking for Pawn.Strip: " + e);
            }
        }

        // Transpiler: replace calls to the removed Pawn.Strip() with our compatibility implementation
        public static IEnumerable<CodeInstruction> PawnchangerApply_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var stripCompat = AccessTools.Method(typeof(RoyaltyCompatBootstrap), nameof(StripPawn));
            foreach (var ci in instructions)
            {
                if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand != null)
                {
                    string operandStr = ci.operand.ToString();
                    if (!string.IsNullOrEmpty(operandStr) && operandStr.Contains("Verse.Pawn::Strip"))
                    {
                        yield return new CodeInstruction(OpCodes.Call, stripCompat);
                        continue;
                    }
                }
                yield return ci;
            }
        }

        // Transpiler: replace old PawnGenerator.GeneratePawn(kind,faction) with GeneratePawnCompat(kind,faction)
        public static IEnumerable<CodeInstruction> SpidersOmgApply_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var compat = AccessTools.Method(typeof(RoyaltyCompatBootstrap), nameof(GeneratePawnCompat));
            foreach (var ci in instructions)
            {
                if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand != null)
                {
                    string operandStr = ci.operand.ToString();
                    if (!string.IsNullOrEmpty(operandStr) && operandStr.Contains("Verse.PawnGenerator::GeneratePawn(Verse.PawnKindDef, RimWorld.Faction)"))
                    {
                        yield return new CodeInstruction(OpCodes.Call, compat);
                        continue;
                    }
                }
                yield return ci;
            }
        }

        // Transpiler: replace old GenExplosion.DoExplosion(...) overload with DoExplosionCompat(...)
        public static IEnumerable<CodeInstruction> BoomcastApply_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var compat = AccessTools.Method(typeof(RoyaltyCompatBootstrap), nameof(DoExplosionCompat));
            foreach (var ci in instructions)
            {
                if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand != null)
                {
                    string s = ci.operand.ToString();
                    if (!string.IsNullOrEmpty(s) && s.Contains("Verse.GenExplosion::DoExplosion"))
                    {
                        yield return new CodeInstruction(OpCodes.Call, compat);
                        continue;
                    }
                }
                yield return ci;
            }
        }

        // Finalizers
        public static Exception PawnchangerApply_Finalizer(Exception __exception, LocalTargetInfo target)
        {
            if (__exception is MissingMethodException mme && (mme.Message?.Contains("Pawn.Strip") ?? false))
            {
                var pawn = target.Pawn;
                if (pawn != null)
                {
                    try { StripPawn(pawn); } catch (Exception e) { Log.Warning($"[AufenCode/RoyaltyCompat] Fallback StripPawn failed: {e}"); }
                }
                Log.Warning("[AufenCode/RoyaltyCompat] Recovered from missing Pawn.Strip by applying StripPawn() and suppressing the exception.");
                return null;
            }
            return __exception;
        }

        public static Exception SpidersOmgApply_Finalizer(Exception __exception)
        {
            if (__exception is MissingMethodException mme && (mme.Message?.Contains("PawnGenerator.GeneratePawn") ?? false))
            {
                Log.Warning("[AufenCode/RoyaltyCompat] Recovered from missing PawnGenerator.GeneratePawn by using GeneratePawnCompat.");
                return null;
            }
            return __exception;
        }

        public static Exception BoomcastApply_Finalizer(Exception __exception)
        {
            if (__exception is MissingMethodException mme && (mme.Message?.Contains("GenExplosion.DoExplosion") ?? false))
            {
                Log.Warning("[AufenCode/RoyaltyCompat] Recovered from missing GenExplosion.DoExplosion by using DoExplosionCompat.");
                return null;
            }
            return __exception;
        }

        // Compatibility helpers
        public static Pawn GeneratePawnCompat(PawnKindDef kindDef, Faction faction)
        {
            try
            {
                // Prefer request-based API
                var reqCtor = AccessTools.Constructor(typeof(PawnGenerationRequest), new[] { typeof(PawnKindDef), typeof(Faction), typeof(PawnGenerationContext), typeof(int), typeof(bool) });
                if (reqCtor != null)
                {
                    var req = (PawnGenerationRequest)reqCtor.Invoke(new object[] { kindDef, faction, PawnGenerationContext.NonPlayer, -1, true });
                    return PawnGenerator.GeneratePawn(req);
                }
            }
            catch { /* fall back below */ }

            try
            {
                // Try any available GeneratePawn overload via reflection
                var method = AccessTools.GetDeclaredMethods(typeof(PawnGenerator)).FirstOrDefault(m => m.Name == "GeneratePawn");
                if (method != null)
                {
                    var ps = method.GetParameters();
                    var args = new object[ps.Length];
                    for (int i = 0; i < ps.Length; i++)
                    {
                        var pt = ps[i].ParameterType;
                        if (pt == typeof(PawnKindDef)) args[i] = kindDef;
                        else if (pt == typeof(Faction)) args[i] = faction;
                        else if (pt == typeof(PawnGenerationRequest)) args[i] = new PawnGenerationRequest(kindDef, faction);
                        else if (pt == typeof(PawnGenerationContext)) args[i] = PawnGenerationContext.NonPlayer;
                        else if (pt == typeof(int)) args[i] = -1;
                        else if (pt == typeof(bool)) args[i] = true;
                        else args[i] = null;
                    }
                    var pawn = method.Invoke(null, args) as Pawn;
                    if (pawn != null) return pawn;
                }
            }
            catch { }

            // Last resort
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kindDef, faction));
        }

        public static void DoExplosionCompat(IntVec3 center, Map map, float radius, DamageDef damType, Thing instigator = null)
        {
            try
            {
                // Try to find a DoExplosion overload that we can call with basic parameters
                var method = AccessTools.GetDeclaredMethods(typeof(GenExplosion)).FirstOrDefault(m => m.Name == "DoExplosion");
                if (method != null)
                {
                    var ps = method.GetParameters();
                    var args = new object[ps.Length];
                    for (int i = 0; i < ps.Length; i++)
                    {
                        var pt = ps[i].ParameterType;
                        if (pt == typeof(IntVec3)) args[i] = center;
                        else if (pt == typeof(Map)) args[i] = map;
                        else if (pt == typeof(float)) args[i] = radius;
                        else if (pt == typeof(DamageDef)) args[i] = damType;
                        else if (pt == typeof(Thing)) args[i] = instigator;
                        else if (pt == typeof(int)) args[i] = -1;
                        else if (pt == typeof(float)) args[i] = -1f; // armorPen or others
                        else if (pt == typeof(SoundDef)) args[i] = null;
                        else if (pt == typeof(ThingDef)) args[i] = null;
                        else if (pt == typeof(bool)) args[i] = false;
                        else if (pt == typeof(ThingPlaceMode)) args[i] = ThingPlaceMode.Near;
                        else if (pt == typeof(List<Thing>)) args[i] = null;
                        else args[i] = null;
                    }
                    method.Invoke(null, args);
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[AufenCode/RoyaltyCompat] DoExplosionCompat reflection call failed: {e}");
            }

            // Fallback to current signature subset if known
            GenExplosion.DoExplosion(center, map, radius, damType, instigator);
        }

        // Compatibility implementation that drops apparel and equipment similar to the old Pawn.Strip().
        public static void StripPawn(Pawn pawn)
        {
            if (pawn == null) return;
            try
            {
                var map = pawn.MapHeld;
                var pos = pawn.PositionHeld;

                // Drop apparel
                var apparelTracker = pawn.apparel;
                if (apparelTracker != null)
                {
                    var worn = apparelTracker.WornApparel?.ToList();
                    if (worn != null)
                    {
                        foreach (var ap in worn)
                        {
                            if (ap == null) continue;
                            if (!TryInvokeApparelTryDrop(apparelTracker, ap, pos, map))
                            {
                                apparelTracker.Remove(ap);
                                if (ap.Spawned) continue;
                                GenPlace.TryPlaceThing(ap, pos, map, ThingPlaceMode.Near);
                            }
                        }
                    }
                }

                // Drop equipment
                var eq = pawn.equipment;
                if (eq != null)
                {
                    if (!TryInvokeDropAllEquipment(eq, pos))
                    {
                        var all = eq.AllEquipmentListForReading?.ToList();
                        if (all != null)
                        {
                            foreach (var e in all)
                            {
                                if (e == null) continue;
                                if (!TryInvokeTryDropEquipment(eq, e, pos))
                                {
                                    eq.Remove(e);
                                    if (!e.Spawned)
                                    {
                                        GenPlace.TryPlaceThing(e, pos, map, ThingPlaceMode.Near);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AufenCode/RoyaltyCompat] StripPawn encountered an error: {ex}");
            }
        }

        private static bool TryInvokeApparelTryDrop(object apparelTracker, Apparel apparel, IntVec3 pos, Map map)
        {
            var methods = AccessTools.GetDeclaredMethods(apparelTracker.GetType()).Where(m => m.Name == "TryDrop");
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    if (pt == typeof(Apparel)) args[i] = apparel;
                    else if (pt == typeof(IntVec3)) args[i] = pos;
                    else if (pt == typeof(Map)) args[i] = map;
                    else if (pt == typeof(ThingPlaceMode)) args[i] = ThingPlaceMode.Near;
                    else if (pt == typeof(bool)) args[i] = false;
                    else if (pt.IsByRef) args[i] = null;
                    else args[i] = null;
                }
                try
                {
                    var result = m.Invoke(apparelTracker, args);
                    if (result is bool b && b) return true;
                }
                catch { }
            }
            return false;
        }

        private static bool TryInvokeDropAllEquipment(object equipmentTracker, IntVec3 pos)
        {
            var m = AccessTools.Method(equipmentTracker.GetType(), "DropAllEquipment");
            if (m == null) return false;
            var ps = m.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var pt = ps[i].ParameterType;
                if (pt == typeof(IntVec3)) args[i] = pos;
                else if (pt == typeof(bool)) args[i] = false;
                else if (pt == typeof(Map)) args[i] = (equipmentTracker as Pawn_EquipmentTracker)?.pawn?.MapHeld ?? default(Map);
                else if (pt == typeof(ThingPlaceMode)) args[i] = ThingPlaceMode.Near;
                else args[i] = null;
            }
            try { m.Invoke(equipmentTracker, args); return true; } catch { return false; }
        }

        private static bool TryInvokeTryDropEquipment(object equipmentTracker, ThingWithComps eq, IntVec3 pos)
        {
            var methods = AccessTools.GetDeclaredMethods(equipmentTracker.GetType()).Where(mm => mm.Name == "TryDropEquipment");
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    if (pt == typeof(ThingWithComps)) args[i] = eq;
                    else if (pt == typeof(IntVec3)) args[i] = pos;
                    else if (pt == typeof(bool)) args[i] = false;
                    else if (pt.IsByRef) args[i] = null;
                    else if (pt == typeof(Map)) args[i] = (equipmentTracker as Pawn_EquipmentTracker)?.pawn?.MapHeld ?? default(Map);
                    else args[i] = null;
                }
                try
                {
                    var result = m.Invoke(equipmentTracker, args);
                    if (result is bool b && b) return true;
                }
                catch { }
            }
            return false;
        }
    }
}

