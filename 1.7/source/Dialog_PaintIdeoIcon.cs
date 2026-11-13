using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multicolor_Ideology
{
    internal sealed class Dialog_PaintIdeoIcon : Window
    {
        private enum PaintTool { Brush, Eraser, Fill }

        private static readonly List<Color> Palette = new List<Color>
        {
            Color.white, Color.black, Color.red, Color.yellow, Color.green, Color.cyan,
            Color.blue, Color.magenta, new Color(1f, 0.5f, 0f), new Color(0.5f, 0.25f, 1f)
        };
        private static readonly Vector2[] BrushCircle = MakeBrushCircle();

        private readonly DialogPaintState parent;
        private IconPaintData working;
        private Texture2D preview;
        private bool dirty = true;
        private PaintTool tool;
        private Color color = Color.red;
        private float brushSize = 12f;
        private float opacity = 1f;
        private bool wheelDragging;
        private string[] rgbBuffers = new string[6];
        private string[] hsvBuffers = new string[6];
        private Color rgbColorBuffer;
        private Color hsvColorBuffer;
        private string previousFocusedControl;
        private readonly List<IconPaintData> undo = new List<IconPaintData>();
        private readonly List<IconPaintData> redo = new List<IconPaintData>();
        private IconPaintData beforeStroke;
        private IconPaintData beforeAngle;
        private bool stroke;
        private bool strokeChanged;
        private Vector2 lastPoint;

        public override Vector2 InitialSize => new Vector2(920f, 700f);

        public Dialog_PaintIdeoIcon(DialogPaintState parent)
        {
            this.parent = parent;
            working = parent.Pending.Clone();
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            IconPaintRenderer.Prewarm(parent.IconDef);
        }

        public override void PostClose()
        {
            EndStroke();
            if (preview != null) UnityEngine.Object.Destroy(preview);
            base.PostClose();
        }

        public override void DoWindowContents(Rect rect)
        {
            if (parent.IconDef == null || !IconPaintRenderer.Prewarm(parent.IconDef))
            {
                Widgets.Label(rect, "MulticolorIdeology.ReadbackFailed".Translate());
                if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 42f, 150f, 38f), "CancelButton".Translate())) Close();
                return;
            }

            Widgets.Label(new Rect(0f, 0f, rect.width, 30f), "MulticolorIdeology.PaintTitle".Translate());
            Rect canvas = new Rect(12f, 40f, 440f, 440f);
            Widgets.DrawBoxSolid(canvas, new Color(0.2f, 0.2f, 0.2f));
            if (Event.current.type == EventType.Repaint && dirty)
            {
                if (IconPaintRenderer.Render(parent.IconDef, parent.BaseColor, working, ref preview)) dirty = false;
            }
            Color oldColor = GUI.color;
            GUI.color = preview == null ? parent.BaseColor : Color.white;
            GUI.DrawTexture(canvas, preview == null ? parent.IconDef.Icon : preview);
            GUI.color = oldColor;
            Widgets.DrawBox(canvas);
            HandleCanvas(canvas);
            DrawBrushCircle(canvas);

            float x = 480f;
            Widgets.Label(new Rect(x, 40f, 180f, 26f), "MulticolorIdeology.FinishedPreview".Translate());
            Rect small = new Rect(x + 220f, 40f, 88f, 88f);
            GUI.color = preview == null ? parent.BaseColor : Color.white;
            GUI.DrawTexture(small, preview == null ? parent.IconDef.Icon : preview);
            GUI.color = oldColor;

            if (Widgets.ButtonText(new Rect(x, 139f, 100f, 30f), "MulticolorIdeology.Brush".Translate())) tool = PaintTool.Brush;
            if (Widgets.ButtonText(new Rect(x + 106f, 139f, 100f, 30f), "MulticolorIdeology.Eraser".Translate())) tool = PaintTool.Eraser;
            if (Widgets.ButtonText(new Rect(x + 212f, 139f, 100f, 30f), "MulticolorIdeology.Fill".Translate())) tool = PaintTool.Fill;
            Widgets.Label(new Rect(x, 176f, 175f, 24f), "MulticolorIdeology.BrushSize".Translate() + ": " + Mathf.RoundToInt(brushSize));
            brushSize = Widgets.HorizontalSlider(new Rect(x + 160f, 178f, 215f, 22f), brushSize, 1f, 64f);
            Widgets.Label(new Rect(x, 202f, 175f, 24f), "MulticolorIdeology.Opacity".Translate() + ": " + Mathf.RoundToInt(opacity * 100f) + "%");
            opacity = Widgets.HorizontalSlider(new Rect(x + 160f, 204f, 215f, 22f), opacity, 0.01f, 1f);

            Widgets.Label(new Rect(x, 232f, 240f, 24f), "MulticolorIdeology.BrushColor".Translate());
            Color selected = color;
            Widgets.ColorSelector(new Rect(x, 257f, 380f, 49f), ref selected, Palette, out _);
            color = selected;
            Widgets.HSVColorWheel(new Rect(x, 315f, 128f, 128f), ref color, ref wheelDragging);
            Widgets.Label(new Rect(x + 145f, 312f, 112f, 24f), "RGB");
            Widgets.Label(new Rect(x + 262f, 312f, 112f, 24f), "HSV");
            Widgets.ColorComponents rgb = Widgets.ColorComponents.Red | Widgets.ColorComponents.Green | Widgets.ColorComponents.Blue;
            Widgets.ColorComponents hsv = Widgets.ColorComponents.Hue | Widgets.ColorComponents.Sat | Widgets.ColorComponents.Value;
            RectAggregator rgbFields = new RectAggregator(new Rect(x + 145f, 338f, 112f, 0f), 24681357);
            RectAggregator hsvFields = new RectAggregator(new Rect(x + 262f, 338f, 112f, 0f), 24681358);
            Widgets.ColorTextfields(ref rgbFields, ref color, ref rgbBuffers, ref rgbColorBuffer, previousFocusedControl, "MI_RGBFields", rgb, rgb);
            Widgets.ColorTextfields(ref hsvFields, ref color, ref hsvBuffers, ref hsvColorBuffer, previousFocusedControl, "MI_HSVFields", hsv, hsv);
            previousFocusedControl = GUI.GetNameOfFocusedControl();

            float gy = 461f;
            if (Widgets.ButtonText(new Rect(x, gy, 142f, 30f), working.Gradient ? "MulticolorIdeology.GradientOn".Translate() : "MulticolorIdeology.GradientOff".Translate()))
            {
                Edit(() => working.Gradient = !working.Gradient);
            }
            if (Widgets.ButtonText(new Rect(x + 148f, gy, 106f, 30f), "MulticolorIdeology.SetStart".Translate()))
            {
                Edit(() => working.Start = (Color32)color);
            }
            if (Widgets.ButtonText(new Rect(x + 260f, gy, 106f, 30f), "MulticolorIdeology.SetEnd".Translate()))
            {
                Edit(() => working.End = (Color32)color);
            }
            Widgets.DrawBoxSolid(new Rect(x + 148f, gy + 34f, 106f, 8f), working.Start);
            Widgets.DrawBoxSolid(new Rect(x + 260f, gy + 34f, 106f, 8f), working.End);
            Widgets.Label(new Rect(x, 513f, 160f, 25f), "MulticolorIdeology.Angle".Translate() + ": " + Mathf.RoundToInt(working.Angle) + "°");
            Rect angleRect = new Rect(x + 160f, 516f, 215f, 22f);
            EventType angleEvent = Event.current.rawType;
            if (angleEvent == EventType.MouseDown && Mouse.IsOver(angleRect)) beforeAngle = working.Clone();
            float angle = Widgets.HorizontalSlider(angleRect, working.Angle, 0f, 360f);
            if (!Mathf.Approximately(angle, working.Angle))
            {
                working.Angle = angle;
                dirty = true;
            }
            if (angleEvent == EventType.MouseUp && beforeAngle != null)
            {
                if (!Mathf.Approximately(beforeAngle.Angle, working.Angle)) PushUndo(beforeAngle);
                beforeAngle = null;
            }

            if (Widgets.ButtonText(new Rect(x, 553f, 105f, 30f), "MulticolorIdeology.Undo".Translate())) Restore(undo, redo);
            if (Widgets.ButtonText(new Rect(x + 111f, 553f, 105f, 30f), "MulticolorIdeology.Redo".Translate())) Restore(redo, undo);
            if (Widgets.ButtonText(new Rect(x + 222f, 553f, 144f, 30f), "MulticolorIdeology.Reset".Translate())) Edit(working.Clear);
            Widgets.Label(new Rect(12f, 492f, 440f, 75f), "MulticolorIdeology.PaintHelp".Translate());
            if (Widgets.ButtonText(new Rect(rect.width - 315f, rect.height - 42f, 145f, 38f), "CancelButton".Translate())) Close();
            if (Widgets.ButtonText(new Rect(rect.width - 155f, rect.height - 42f, 145f, 38f), "MulticolorIdeology.Apply".Translate()))
            {
                EndStroke();
                parent.Pending = working.Clone();
                parent.Dirty = true;
                Close();
            }
        }

        private void HandleCanvas(Rect canvas)
        {
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && Mouse.IsOver(canvas))
            {
                if (tool == PaintTool.Fill) Edit(Fill);
                else
                {
                    beforeStroke = working.Clone();
                    stroke = true;
                    strokeChanged = false;
                    lastPoint = CanvasPoint(evt.mousePosition, canvas);
                    Stamp(lastPoint);
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && stroke)
            {
                Vector2 next = CanvasPoint(evt.mousePosition, canvas);
                float step = Mathf.Max(0.5f, brushSize * 0.25f);
                float distance = Vector2.Distance(lastPoint, next);
                int count = Mathf.Max(1, Mathf.CeilToInt(distance / step));
                Vector2 from = lastPoint;
                for (int i = 1; i <= count; i++) Stamp(Vector2.Lerp(from, next, (float)i / count));
                lastPoint = next;
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && stroke)
            {
                EndStroke();
                evt.Use();
            }
        }

        private void DrawBrushCircle(Rect canvas)
        {
            if (tool != PaintTool.Brush || Event.current.type != EventType.Repaint || !canvas.Contains(Event.current.mousePosition)) return;
            Vector2 center = Event.current.mousePosition - canvas.position;
            float radius = brushSize * 0.5f * canvas.width / (IconPaintData.Size - 1);
            GUI.BeginGroup(canvas);
            for (int i = 0; i < BrushCircle.Length; i++)
            {
                Vector2 start = center + BrushCircle[i] * radius;
                Vector2 end = center + BrushCircle[(i + 1) % BrushCircle.Length] * radius;
                Widgets.DrawLine(start, end, Color.black, 0.9f);
                Widgets.DrawLine(start, end, Color.white, 0.45f);
            }
            GUI.EndGroup();
        }

        private static Vector2[] MakeBrushCircle()
        {
            Vector2[] points = new Vector2[48];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * 2f * Mathf.PI / points.Length;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            return points;
        }

        private static Vector2 CanvasPoint(Vector2 mouse, Rect canvas)
        {
            return new Vector2(Mathf.Clamp01((mouse.x - canvas.x) / canvas.width) * (IconPaintData.Size - 1), (1f - Mathf.Clamp01((mouse.y - canvas.y) / canvas.height)) * (IconPaintData.Size - 1));
        }

        private void Stamp(Vector2 point)
        {
            float radius = brushSize * 0.5f;
            int left = Mathf.Max(0, Mathf.FloorToInt(point.x - radius));
            int right = Mathf.Min(IconPaintData.Size - 1, Mathf.CeilToInt(point.x + radius));
            int bottom = Mathf.Max(0, Mathf.FloorToInt(point.y - radius));
            int top = Mathf.Min(IconPaintData.Size - 1, Mathf.CeilToInt(point.y + radius));
            Color32 brush = color;
            for (int y = bottom; y <= top; y++)
                for (int x = left; x <= right; x++)
                {
                    if ((new Vector2(x, y) - point).sqrMagnitude > radius * radius || !IconPaintRenderer.IsVisible(parent.IconDef, x, y)) continue;
                    int i = (y * IconPaintData.Size + x) * 4;
                    if (tool == PaintTool.Eraser) working.Pixels[i + 3] = 0;
                    else
                    {
                        working.Pixels[i] = brush.r;
                        working.Pixels[i + 1] = brush.g;
                        working.Pixels[i + 2] = brush.b;
                        working.Pixels[i + 3] = (byte)Mathf.RoundToInt(opacity * 255f);
                    }
                    strokeChanged = true;
                }
            if (strokeChanged)
            {
                working.Touch();
                dirty = true;
            }
        }

        private void Fill()
        {
            Color32 brush = color;
            byte alpha = (byte)Mathf.RoundToInt(opacity * 255f);
            for (int y = 0; y < IconPaintData.Size; y++)
                for (int x = 0; x < IconPaintData.Size; x++)
                {
                    if (!IconPaintRenderer.IsVisible(parent.IconDef, x, y)) continue;
                    int i = (y * IconPaintData.Size + x) * 4;
                    working.Pixels[i] = brush.r;
                    working.Pixels[i + 1] = brush.g;
                    working.Pixels[i + 2] = brush.b;
                    working.Pixels[i + 3] = alpha;
                }
        }

        private void EndStroke()
        {
            if (!stroke) return;
            if (strokeChanged) PushUndo(beforeStroke);
            beforeStroke = null;
            stroke = false;
            strokeChanged = false;
        }

        private void Edit(Action action)
        {
            IconPaintData before = working.Clone();
            action();
            working.Touch();
            dirty = true;
            PushUndo(before);
        }

        private void PushUndo(IconPaintData data)
        {
            if (data == null) return;
            undo.Add(data);
            if (undo.Count > 20) undo.RemoveAt(0);
            redo.Clear();
        }

        private void Restore(List<IconPaintData> from, List<IconPaintData> to)
        {
            if (from.Count == 0) return;
            to.Add(working.Clone());
            working = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            dirty = true;
        }
    }
}
