using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multicolor_Ideology.Patches
{
    [HarmonyPatch(typeof(Ideo), nameof(Ideo.ExposeData))]
    internal static class Patch_Ideo_ExposeData
    {
        private static void Postfix(Ideo __instance)
        {
            IconPaintData data = Scribe.mode == LoadSaveMode.Saving ? IconPaintRenderer.SaveData(__instance) : null;
            Scribe_Deep.Look(ref data, "multicolorIconPaint");
            if (Scribe.mode == LoadSaveMode.LoadingVars) IconPaintRenderer.Assign(__instance, data);
        }
    }

    [HarmonyPatch(typeof(Ideo), nameof(Ideo.CopyTo))]
    internal static class Patch_Ideo_CopyTo
    {
        private static void Postfix(Ideo __instance, Ideo ideo)
        {
            ideo.SetIcon(ideo.iconDef, ideo.colorDef);
            IconPaintRenderer.Assign(ideo, IconPaintRenderer.CopyData(__instance));
        }
    }

    [HarmonyPatch(typeof(Ideo), nameof(Ideo.SetIcon))]
    internal static class Patch_Ideo_SetIcon
    {
        private static void Prefix(Ideo __instance, out IdeoIconDef __state)
        {
            __state = __instance.iconDef;
        }

        private static void Postfix(Ideo __instance, IdeoIconDef __state)
        {
            if (__state != __instance.iconDef) IconPaintRenderer.Assign(__instance, null);
            else IconPaintRenderer.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Ideo), nameof(Ideo.Icon), MethodType.Getter)]
    internal static class Patch_Ideo_Icon
    {
        private static void Postfix(Ideo __instance, ref Texture2D __result)
        {
            __result = IconPaintRenderer.Get(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(Ideo), nameof(Ideo.DrawIcon))]
    internal static class Patch_Ideo_DrawIcon
    {
        private static bool Prefix(Ideo __instance, Rect rect)
        {
            if (!IconPaintRenderer.HasPaint(__instance)) return true;
            Texture2D texture = __instance.Icon;
            if (!IconPaintRenderer.IsGenerated(texture)) return true;
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, previous.a);
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
            return false;
        }
    }

    [HarmonyPatch(typeof(IdeoFoundation), nameof(IdeoFoundation.RandomizeIcon))]
    internal static class Patch_IdeoFoundation_RandomizeIcon
    {
        private static void Prefix(IdeoFoundation __instance)
        {
            IconPaintRenderer.Assign(__instance.ideo, null);
        }
    }

    [HarmonyPatch(typeof(IdeoManager), nameof(IdeoManager.Remove))]
    internal static class Patch_IdeoManager_Remove
    {
        private static void Postfix(Ideo ideo, bool __result)
        {
            if (__result) IconPaintRenderer.Release(ideo);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Dispose))]
    internal static class Patch_Game_Dispose
    {
        private static void Prefix()
        {
            IconPaintRenderer.Reset();
        }
    }

    [HarmonyPatch(typeof(IdeoFiles), nameof(IdeoFiles.RecacheData))]
    internal static class Patch_IdeoFiles_RecacheData
    {
        private static void Prefix()
        {
            foreach (Ideo ideo in IdeoFiles.AllIdeosLocal) IconPaintRenderer.Release(ideo);
        }
    }
}
