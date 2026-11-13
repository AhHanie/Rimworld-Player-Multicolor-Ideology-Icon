using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace Multicolor_Ideology
{
    internal sealed class IconPaintState
    {
        public IconPaintData Data;
        public Texture2D Texture;
        public IdeoIconDef IconDef;
        public Color BaseColor;
        public bool Dirty = true;
    }

    internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T x, T y) => ReferenceEquals(x, y);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }

    internal static class IconPaintRenderer
    {
        private const int OutputSize = 256;
        private static ConditionalWeakTable<Ideo, IconPaintState> states = new ConditionalWeakTable<Ideo, IconPaintState>();
        private static readonly Dictionary<Texture2D, IconPaintState> generated = new Dictionary<Texture2D, IconPaintState>(new ReferenceComparer<Texture2D>());
        private static readonly Dictionary<IdeoIconDef, Color32[]> masks = new Dictionary<IdeoIconDef, Color32[]>();
        private static readonly HashSet<IdeoIconDef> failedMasks = new HashSet<IdeoIconDef>();

        public static bool HasPaint(Ideo ideo) => ideo != null && states.TryGetValue(ideo, out IconPaintState state) && state.Data != null && state.Data.IsCustom && TryMask(ideo.iconDef, out _);

        public static IconPaintData CopyData(Ideo ideo)
        {
            return states.TryGetValue(ideo, out IconPaintState state) ? state.Data?.Clone() : null;
        }

        public static IconPaintData SaveData(Ideo ideo)
        {
            return states.TryGetValue(ideo, out IconPaintState state) ? state.Data : null;
        }

        public static void Assign(Ideo ideo, IconPaintData data)
        {
            if (ideo == null) return;
            if (data == null || !data.IsCustom)
            {
                Release(ideo);
                return;
            }
            IconPaintState state = states.GetOrCreateValue(ideo);
            state.Data = data.Clone();
            state.Dirty = true;
        }

        public static void Invalidate(Ideo ideo)
        {
            if (states.TryGetValue(ideo, out IconPaintState state)) state.Dirty = true;
        }

        public static void Release(Ideo ideo)
        {
            if (!states.TryGetValue(ideo, out IconPaintState state)) return;
            Release(state);
            states.Remove(ideo);
        }

        private static void Release(IconPaintState state)
        {
            if (state.Texture == null) return;
            generated.Remove(state.Texture);
            UnityEngine.Object.Destroy(state.Texture);
            state.Texture = null;
        }

        public static void Reset()
        {
            foreach (IconPaintState state in generated.Values) UnityEngine.Object.Destroy(state.Texture);
            generated.Clear();
            states = new ConditionalWeakTable<Ideo, IconPaintState>();
        }

        public static bool IsGenerated(Texture2D texture) => texture != null && generated.ContainsKey(texture);

        public static Color Tint(Ideo ideo) => HasPaint(ideo) ? Color.white : ideo.Color;

        public static Texture2D Get(Ideo ideo, Texture2D vanilla)
        {
            if (!states.TryGetValue(ideo, out IconPaintState state) || state.Data == null || !state.Data.IsCustom || ideo.iconDef == null) return vanilla;
            if (state.Texture == null || state.Dirty || state.IconDef != ideo.iconDef || state.BaseColor != ideo.Color)
            {
                if (!Render(ideo.iconDef, ideo.Color, state.Data, ref state.Texture)) return vanilla;
                generated[state.Texture] = state;
                state.IconDef = ideo.iconDef;
                state.BaseColor = ideo.Color;
                state.Dirty = false;
            }
            return state.Texture;
        }

        public static bool Render(IdeoIconDef iconDef, Color baseColor, IconPaintData data, ref Texture2D output)
        {
            if (!TryMask(iconDef, out Color32[] mask)) return false;
            if (output == null)
            {
                output = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false);
                output.wrapMode = TextureWrapMode.Clamp;
                output.filterMode = FilterMode.Bilinear;
                output.name = "MulticolorIdeology_" + iconDef.defName;
            }
            Color32[] pixels = new Color32[OutputSize * OutputSize];
            float angle = data.Angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float span = Mathf.Abs(cos) + Mathf.Abs(sin);
            for (int y = 0; y < OutputSize; y++)
            {
                float v = (y + 0.5f) / OutputSize;
                for (int x = 0; x < OutputSize; x++)
                {
                    int i = y * OutputSize + x;
                    Color32 source = mask[i];
                    if (source.a == 0) continue;
                    float u = (x + 0.5f) / OutputSize;
                    float t = Mathf.Clamp01(0.5f + ((u - 0.5f) * cos + (v - 0.5f) * sin) / span);
                    Color color = data.Gradient ? Color.Lerp((Color)data.Start, (Color)data.End, t) : baseColor;
                    Color overlay = Sample(data.Pixels, u, v);
                    color = Color.Lerp(color, overlay, overlay.a);
                    pixels[i] = new Color32((byte)Mathf.RoundToInt(source.r * color.r), (byte)Mathf.RoundToInt(source.g * color.g), (byte)Mathf.RoundToInt(source.b * color.b), source.a);
                }
            }
            output.SetPixels32(pixels);
            output.Apply(false, false);
            return true;
        }

        public static bool IsVisible(IdeoIconDef iconDef, int x, int y)
        {
            if (!TryMask(iconDef, out Color32[] mask)) return false;
            int sx = Mathf.Clamp(x * OutputSize / IconPaintData.Size, 0, OutputSize - 1);
            int sy = Mathf.Clamp(y * OutputSize / IconPaintData.Size, 0, OutputSize - 1);
            int nextX = Mathf.Min(sx + 1, OutputSize - 1);
            int nextY = Mathf.Min(sy + 1, OutputSize - 1);
            return mask[sy * OutputSize + sx].a != 0 || mask[sy * OutputSize + nextX].a != 0 || mask[nextY * OutputSize + sx].a != 0 || mask[nextY * OutputSize + nextX].a != 0;
        }

        public static bool Prewarm(IdeoIconDef iconDef) => TryMask(iconDef, out _);

        private static Color Sample(byte[] data, float u, float v)
        {
            float fx = Mathf.Clamp(u * IconPaintData.Size - 0.5f, 0f, IconPaintData.Size - 1f);
            float fy = Mathf.Clamp(v * IconPaintData.Size - 0.5f, 0f, IconPaintData.Size - 1f);
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            int x1 = Mathf.Min(x0 + 1, IconPaintData.Size - 1), y1 = Mathf.Min(y0 + 1, IconPaintData.Size - 1);
            float tx = fx - x0, ty = fy - y0;
            return Color.Lerp(Color.Lerp(Pixel(data, x0, y0), Pixel(data, x1, y0), tx), Color.Lerp(Pixel(data, x0, y1), Pixel(data, x1, y1), tx), ty);
        }

        private static Color Pixel(byte[] data, int x, int y)
        {
            int i = (y * IconPaintData.Size + x) * 4;
            return new Color32(data[i], data[i + 1], data[i + 2], data[i + 3]);
        }

        private static bool TryMask(IdeoIconDef iconDef, out Color32[] pixels)
        {
            if (iconDef == null || failedMasks.Contains(iconDef))
            {
                pixels = null;
                return false;
            }
            if (masks.TryGetValue(iconDef, out pixels)) return true;
            RenderTexture target = null;
            Texture2D readable = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                target = RenderTexture.GetTemporary(OutputSize, OutputSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
                Graphics.Blit(iconDef.Icon, target);
                RenderTexture.active = target;
                readable = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, OutputSize, OutputSize), 0, 0);
                readable.Apply(false, false);
                pixels = readable.GetPixels32();
                masks.Add(iconDef, pixels);
                return true;
            }
            catch (Exception e)
            {
                failedMasks.Add(iconDef);
                Log.Error("Multicolor Ideology: could not read symbol " + iconDef.defName + ": " + e.Message);
                pixels = null;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (readable != null) UnityEngine.Object.Destroy(readable);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
