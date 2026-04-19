using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SevenDebug
{
    /// <summary>
    /// Enables prefab editing tools by replacing EntityPlayerLocal.GetDebug() checks
    /// with a method that always returns true. This allows block placement, rotation,
    /// debug keys, and prefab editor panels to work without needing admin permissions.
    /// </summary>
    public static class PrefabToolsPatch
    {
        /// <summary>
        /// Replacement for EntityPlayerLocal.GetDebug() — always returns true.
        /// </summary>
        public static bool AlwaysTrue(EntityPlayerLocal _player)
        {
            return true;
        }

        private static readonly MethodInfo _originalGetDebug =
            AccessTools.Method(typeof(EntityPlayerLocal), "GetDebug");

        private static readonly MethodInfo _alwaysTrue =
            AccessTools.Method(typeof(PrefabToolsPatch), "AlwaysTrue");

        /// <summary>
        /// Shared transpiler logic: replaces all calls to EntityPlayerLocal.GetDebug()
        /// with PrefabToolsPatch.AlwaysTrue() in the target method's IL.
        /// </summary>
        public static IEnumerable<CodeInstruction> ReplaceGetDebugCalls(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instr in instructions)
            {
                if ((instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt) &&
                    instr.operand is MethodInfo method &&
                    method == _originalGetDebug)
                {
                    // Replace instance call with our static call (same signature: takes EntityPlayerLocal, returns bool)
                    yield return new CodeInstruction(OpCodes.Call, _alwaysTrue);
                }
                else
                {
                    yield return instr;
                }
            }
        }
    }

    // --- Transpiler patches for PlayerMoveController methods ---

    [HarmonyPatch(typeof(PlayerMoveController), "Update")]
    public static class PlayerMoveController_Update_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    [HarmonyPatch(typeof(PlayerMoveController), "OnGUI")]
    public static class PlayerMoveController_OnGUI_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    [HarmonyPatch(typeof(PlayerMoveController), "updateDebugKeys")]
    public static class PlayerMoveController_updateDebugKeys_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    // --- Transpiler patches for BlockToolSelection methods ---

    [HarmonyPatch(typeof(BlockToolSelection), "CheckKeys")]
    public static class BlockToolSelection_CheckKeys_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    [HarmonyPatch(typeof(BlockToolSelection), "CheckSpecialKeys")]
    public static class BlockToolSelection_CheckSpecialKeys_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    [HarmonyPatch(typeof(BlockToolSelection), "ExecuteUseAction")]
    public static class BlockToolSelection_ExecuteUseAction_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    [HarmonyPatch(typeof(BlockToolSelection), "ExecuteAttackAction")]
    public static class BlockToolSelection_ExecuteAttackAction_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    [HarmonyPatch(typeof(BlockToolSelection), "decInventoryLater")]
    public static class BlockToolSelection_decInventoryLater_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    [HarmonyPatch(typeof(BlockToolSelection), "RotateFocusedBlock")]
    public static class BlockToolSelection_RotateFocusedBlock_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }

    // --- Enable debug panels on startup ---

    [HarmonyPatch(typeof(NGuiWdwDebugPanels), "Awake")]
    public static class NGuiWdwDebugPanels_Awake_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(NGuiWdwDebugPanels __instance)
        {
            // Enable debug panels — some are fields, some are properties/methods
            // Use reflection to handle both cases safely
            var type = typeof(NGuiWdwDebugPanels);
            var panels = new[] {
                "showDebugPanel_General", "showDebugPanel_Prefab",
                "showDebugPanel_FocusedBlock", "showDebugPanel_Selection",
                "showDebugPanel_Cache", "showDebugPanel_Chunk",
                "showDebugPanel_Player", "showDebugPanel_Network",
                "showDebugPanel_Spawning", "showDebugPanel_Stealth",
                "showDebugPanel_Texture", "showDebugPanel_PlayerEffectInfo"
            };

            foreach (var name in panels)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(bool))
                {
                    field.SetValue(__instance, true);
                    continue;
                }
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
                    prop.SetValue(__instance, true);
            }
        }
    }

    // --- Enable debug mode in PlayerMoveController.Init ---

    [HarmonyPatch(typeof(PlayerMoveController), "Init")]
    public static class PlayerMoveController_Init_Patch
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => PrefabToolsPatch.ReplaceGetDebugCalls(instructions);
    }
}
