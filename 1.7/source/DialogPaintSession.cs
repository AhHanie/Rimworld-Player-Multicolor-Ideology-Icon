using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multicolor_Ideology
{
    internal sealed class DialogPaintState
    {
        public readonly Ideo Ideo;
        public IdeoIconDef IconDef;
        public ColorDef ColorDef;
        public IconPaintData Pending;
        public Texture2D Preview;
        public bool Dirty = true;

        public DialogPaintState(Ideo ideo)
        {
            Ideo = ideo;
            IconDef = ideo.iconDef;
            ColorDef = ideo.colorDef;
            Pending = IconPaintRenderer.CopyData(ideo) ?? new IconPaintData();
        }

        public Color BaseColor => ColorDef == Ideo.colorDef ? Ideo.Color : ColorDef?.color ?? Color.white;

        public void Selection(IdeoIconDef icon, ColorDef color)
        {
            if (IconDef != icon)
            {
                IconDef = icon;
                Pending = new IconPaintData();
                Dirty = true;
            }
            if (ColorDef != color)
            {
                ColorDef = color;
                Dirty = true;
            }
        }

        public Texture2D GetPreview()
        {
            if (!Pending.IsCustom || IconDef == null) return null;
            if (Dirty && Event.current.type == EventType.Repaint)
            {
                if (IconPaintRenderer.Render(IconDef, BaseColor, Pending, ref Preview)) Dirty = false;
            }
            return Preview;
        }

        public void Release()
        {
            if (Preview != null) UnityEngine.Object.Destroy(Preview);
            Preview = null;
        }
    }

    internal static class DialogPaintSession
    {
        private static readonly ConditionalWeakTable<Dialog_ChooseIdeoSymbols, DialogPaintState> sessions = new ConditionalWeakTable<Dialog_ChooseIdeoSymbols, DialogPaintState>();

        public static DialogPaintState Get(Dialog_ChooseIdeoSymbols dialog, Ideo ideo)
        {
            if (!sessions.TryGetValue(dialog, out DialogPaintState state))
            {
                state = new DialogPaintState(ideo);
                sessions.Add(dialog, state);
            }
            return state;
        }

        public static void Discard(Dialog_ChooseIdeoSymbols dialog)
        {
            if (sessions.TryGetValue(dialog, out DialogPaintState state))
            {
                state.Release();
                sessions.Remove(dialog);
            }
        }
    }
}
