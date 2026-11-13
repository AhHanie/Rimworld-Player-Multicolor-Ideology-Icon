using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using RimWorld.Planet;

namespace Multicolor_Ideology.Patches
{
    internal static class IconTintInstruction
    {
        public static IEnumerable<CodeInstruction> ReplaceIdeoColor(IEnumerable<CodeInstruction> source, string site)
        {
            MethodInfo original = AccessTools.PropertyGetter(typeof(Ideo), nameof(Ideo.Color));
            MethodInfo replacement = AccessTools.Method(typeof(IconPaintRenderer), nameof(IconPaintRenderer.Tint), new[] { typeof(Ideo) });
            int count = 0;
            foreach (CodeInstruction instruction in source)
            {
                if (instruction.Calls(original) && count++ == 0)
                    yield return new CodeInstruction(OpCodes.Call, replacement) { labels = instruction.labels, blocks = instruction.blocks };
                else yield return instruction;
            }
            if (count != 1) Log.Error("Multicolor Ideology: tint patch expected one symbol color in " + site + ", found " + count);
        }
    }

    [HarmonyPatch(typeof(IdeoUIUtility), nameof(IdeoUIUtility.DoNameAndSymbol))]
    internal static class Patch_IdeoUIUtility_DoNameAndSymbol
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => IconTintInstruction.ReplaceIdeoColor(instructions, nameof(IdeoUIUtility.DoNameAndSymbol));
    }

    [HarmonyPatch(typeof(Dialog_ChooseColonistsForIdeo), nameof(Dialog_ChooseColonistsForIdeo.DoWindowContents))]
    internal static class Patch_Dialog_ChooseColonistsForIdeo
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => IconTintInstruction.ReplaceIdeoColor(instructions, nameof(Dialog_ChooseColonistsForIdeo));
    }

    [HarmonyPatch(typeof(PawnColumnWorker_Ideo), "GetIconColor")]
    internal static class Patch_PawnColumnWorker_Ideo_GetIconColor
    {
        private static void Postfix(Pawn pawn, ref Color __result)
        {
            if (IconPaintRenderer.HasPaint(pawn.Ideo)) __result = Color.white;
        }
    }

    [HarmonyPatch(typeof(PawnPortraitIconsDrawer), nameof(PawnPortraitIconsDrawer.DrawPawnPortraitIcons))]
    internal static class Patch_PawnPortraitIconsDrawer
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawTextureFitted), new[] { typeof(Rect), typeof(Texture), typeof(float), typeof(float) });
            MethodInfo replacement = AccessTools.Method(typeof(Patch_PawnPortraitIconsDrawer), nameof(DrawFitted));
            int count = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    count++;
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    yield return new CodeInstruction(OpCodes.Call, replacement) { labels = instruction.labels, blocks = instruction.blocks };
                }
                else yield return instruction;
            }
            if (count != 1) Log.Error("Multicolor Ideology: portrait tint patch expected one draw, found " + count);
        }

        private static void DrawFitted(Rect rect, Texture texture, float scale, float alpha, bool grayedOut)
        {
            Color old = GUI.color;
            if (texture is Texture2D icon && IconPaintRenderer.IsGenerated(icon))
                GUI.color = grayedOut ? new Color(0.5f, 0.5f, 0.5f, old.a) : new Color(1f, 1f, 1f, old.a);
            Widgets.DrawTextureFitted(rect, texture, scale, alpha);
            GUI.color = old;
        }
    }

    [HarmonyPatch(typeof(FloatMenuOption), MethodType.Constructor, new[] { typeof(string), typeof(Action), typeof(Texture2D), typeof(Color), typeof(MenuOptionPriority), typeof(Action<Rect>), typeof(Thing), typeof(float), typeof(Func<Rect, bool>), typeof(WorldObject), typeof(bool), typeof(int), typeof(HorizontalJustification), typeof(bool) })]
    internal static class Patch_FloatMenuOption
    {
        private static void Postfix(FloatMenuOption __instance, Texture2D iconTex)
        {
            if (IconPaintRenderer.IsGenerated(iconTex)) __instance.iconColor = Color.white;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.Icon), MethodType.Getter)]
    internal static class Patch_Precept_Ritual_Icon
    {
        private static void Postfix(Precept_Ritual __instance, ref Texture2D __result)
        {
            if ((__instance.iconPathOverride ?? __instance.def.iconPath) == null) __result = __instance.ideo.Icon;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.CancelIcon), MethodType.Getter)]
    internal static class Patch_Precept_Ritual_CancelIcon
    {
        private static void Postfix(Precept_Ritual __instance, ref Texture2D __result)
        {
            if ((__instance.cancelIconPathOverride ?? __instance.def.iconPath) == null) __result = __instance.ideo.Icon;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.DrawIcon))]
    internal static class Patch_Precept_Ritual_DrawIcon
    {
        private static bool Prefix(Precept_Ritual __instance, Rect rect)
        {
            Texture2D texture = __instance.Icon;
            if (!IconPaintRenderer.IsGenerated(texture)) return true;
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, old.a);
            GUI.DrawTexture(rect, texture);
            GUI.color = Color.white;
            return false;
        }
    }

    [HarmonyPatch(typeof(Command_Ritual), MethodType.Constructor, new[] { typeof(Precept_Ritual), typeof(TargetInfo), typeof(RitualObligation), typeof(Dictionary<string, Pawn>) })]
    internal static class Patch_Command_Ritual
    {
        private static void Postfix(Command_Ritual __instance)
        {
            if (__instance.icon is Texture2D texture && IconPaintRenderer.IsGenerated(texture)) __instance.defaultIconColor = Color.white;
        }
    }

    [HarmonyPatch(typeof(Command_Ritual), nameof(Command_Ritual.DrawIcon))]
    internal static class Patch_Command_Ritual_DrawIcon
    {
        private static void Prefix(Command_Ritual __instance, Precept_Ritual ___ritual)
        {
            Texture2D current = ___ritual.Icon;
            if (!ReferenceEquals(__instance.icon, current)) __instance.icon = current;
            if (!___ritual.def.mergeRitualGizmosFromAllIdeos && !___ritual.def.iconIgnoresIdeoColor)
                __instance.defaultIconColor = IconPaintRenderer.IsGenerated(current) ? Color.white : ___ritual.ideo.Color;
        }
    }
}
