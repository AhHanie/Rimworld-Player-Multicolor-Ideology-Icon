using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multicolor_Ideology.Patches
{
    [HarmonyPatch(typeof(Dialog_ChooseIdeoSymbols), "DoColorSelector")]
    internal static class Patch_Dialog_ChooseIdeoSymbols_ColorSelector
    {
        private static void Postfix(Dialog_ChooseIdeoSymbols __instance, Rect mainRect, ref float curY, Ideo ___ideo, IdeoIconDef ___newIconDef, ColorDef ___newColorDef)
        {
            DialogPaintState state = DialogPaintSession.Get(__instance, ___ideo);
            state.Selection(___newIconDef, ___newColorDef);
            Texture2D preview = state.GetPreview();
            if (preview != null)
            {
                Color old = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(mainRect.x + 5f, mainRect.y + 5f, 88f, 88f), preview);
                GUI.color = old;
            }
            if (Widgets.ButtonText(new Rect(mainRect.x + 5f, curY, 180f, 30f), "MulticolorIdeology.PaintButton".Translate()))
            {
                if (IconPaintRenderer.Prewarm(state.IconDef)) Find.WindowStack.Add(new Dialog_PaintIdeoIcon(state));
                else Messages.Message("MulticolorIdeology.ReadbackFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
            curY += 36f;
        }
    }

    [HarmonyPatch(typeof(Dialog_ChooseIdeoSymbols), "DoIconSelector")]
    internal static class Patch_Dialog_ChooseIdeoSymbols_IconSelector
    {
        private static void Postfix(Dialog_ChooseIdeoSymbols __instance, Ideo ___ideo, IdeoIconDef ___newIconDef, ColorDef ___newColorDef)
        {
            DialogPaintSession.Get(__instance, ___ideo).Selection(___newIconDef, ___newColorDef);
        }
    }

    [HarmonyPatch(typeof(Dialog_ChooseIdeoSymbols), "TryAccept")]
    internal static class Patch_Dialog_ChooseIdeoSymbols_TryAccept
    {
        private static void Prefix(Dialog_ChooseIdeoSymbols __instance, Ideo ___ideo, IdeoIconDef ___newIconDef, ColorDef ___newColorDef, out IconPaintData __state)
        {
            DialogPaintState state = DialogPaintSession.Get(__instance, ___ideo);
            state.Selection(___newIconDef, ___newColorDef);
            __state = state.Pending.Clone();
        }

        private static void Postfix(Ideo ___ideo, IconPaintData __state)
        {
            IconPaintRenderer.Assign(___ideo, __state);
        }
    }

    [HarmonyPatch(typeof(Window), nameof(Window.PostClose))]
    internal static class Patch_Dialog_ChooseIdeoSymbols_PostClose
    {
        private static void Postfix(Window __instance)
        {
            if (__instance is Dialog_ChooseIdeoSymbols dialog) DialogPaintSession.Discard(dialog);
        }
    }
}
