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
            LogPawnStripPresence();
            TryPatchPawnchanger(harmony);
        }

        private static void TryPatchPawnchanger(Harmony harmony)
        {
            // Target type is in a different assembly: AufenCodeRoyalty.CompAbilityEffect_Pawnchanger
            var targetType = AccessTools.TypeByName("AufenCodeRoyalty.CompAbilityEffect_Pawnchanger");
            if (targetType == null)
            {
                return; // Royalty module not present; nothing to patch
            }

            var applyMethod = AccessTools.Method(targetType, "Apply", new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
            if (applyMethod == null)
            {
                return; // Unexpected signature; avoid hard crash
            }

            harmony.Patch(
                applyMethod,
                transpiler: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(PawnchangerApply_Transpiler)),
                finalizer: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(PawnchangerApply_Finalizer))
            );
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
            var list = new List<CodeInstruction>(instructions);
            var stripCompat = AccessTools.Method(typeof(RoyaltyCompatBootstrap), nameof(StripPawn));

            for (int i = 0; i < list.Count; i++)
            {
                var ci = list[i];
                if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand != null)
                {
                    // Harmony will usually resolve to a MethodBase when reading IL if possible. Match by string to survive version differences.
                    string operandStr = ci.operand.ToString();
                    if (!string.IsNullOrEmpty(operandStr) && operandStr.Contains("Verse.Pawn::Strip"))
                    {
                        ci.opcode = OpCodes.Call; // Call our static helper with the pawn instance already on stack
                        ci.operand = stripCompat;
                    }
                }
                
                yield return ci;
            }
        }

        // Finalizer: if something still throws due to missing Strip, recover and suppress the crash.
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
                return null; // swallow exception
            }
            return __exception;
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
                                // Fallback: force remove and place near
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
                    // Prefer a single DropAllEquipment if available
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
                                    // Fallback: remove and place near
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
            // Try multiple signature variants via reflection for maximum compatibility
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
                    else if (pt.IsByRef) args[i] = null; // out parameter (e.g., out Thing)
                    else args[i] = null;
                }
                try
                {
                    var result = m.Invoke(apparelTracker, args);
                    if (result is bool b && b) return true;
                }
                catch { /* try next overload */ }
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
                    else if (pt.IsByRef) args[i] = null; // out param
                    else if (pt == typeof(Map)) args[i] = (equipmentTracker as Pawn_EquipmentTracker)?.pawn?.MapHeld ?? default(Map);
                    else args[i] = null;
                }
                try
                {
                    var result = m.Invoke(equipmentTracker, args);
                    if (result is bool b && b) return true;
                }
                catch { /* try next overload */ }
            }
            return false;
        }
    }
}

