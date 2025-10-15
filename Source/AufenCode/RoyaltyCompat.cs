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

            harmony.Patch(applyMethod, prefix: new HarmonyMethod(typeof(RoyaltyCompatBootstrap), nameof(PawnchangerApply_Prefix)));
        }

        // Prefix that prevents execution of the original method which calls the removed Pawn.Strip()
        public static bool PawnchangerApply_Prefix(object __instance, LocalTargetInfo target, LocalTargetInfo dest)
        {
            // Minimal compatibility: skip broken original to prevent MissingMethodException crash.
            // Note: This disables Pawnchanger Apply behavior to avoid hard errors on newer game versions.
            Log.Warning("[AufenCode/RoyaltyCompat] Skipping CompAbilityEffect_Pawnchanger.Apply to avoid MissingMethodException (Pawn.Strip missing). This is a compatibility shim.");
            return false; // skip original
        }
    }
}
