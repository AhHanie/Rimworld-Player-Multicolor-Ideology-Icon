using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Verse;

namespace Multicolor_Ideology
{
    public sealed class IconPaintData : IExposable
    {
        public const int Size = 128;
        private const int Version = 1;
        private const int ByteCount = Size * Size * 4;
        private const int MaxEncodedLength = 100000;

        public bool Gradient;
        public Color32 Start = new Color32(255, 255, 255, 255);
        public Color32 End = new Color32(0, 0, 0, 255);
        public float Angle;
        public byte[] Pixels = new byte[ByteCount];
        private string encoded;
        private bool changed = true;
        private bool customKnown;
        private bool custom;
        private static bool loggedBadData;

        public bool IsCustom
        {
            get
            {
                if (!customKnown)
                {
                    custom = Gradient;
                    if (!custom)
                        for (int i = 3; i < Pixels.Length; i += 4)
                            if (Pixels[i] != 0)
                            {
                                custom = true;
                                break;
                            }
                    customKnown = true;
                }
                return custom;
            }
        }

        public IconPaintData Clone()
        {
            return new IconPaintData
            {
                Gradient = Gradient,
                Start = Start,
                End = End,
                Angle = Angle,
                Pixels = (byte[])Pixels.Clone(),
                encoded = encoded,
                changed = changed,
                customKnown = customKnown,
                custom = custom
            };
        }

        public void Touch()
        {
            changed = true;
            customKnown = false;
        }

        public void Clear()
        {
            Gradient = false;
            Array.Clear(Pixels, 0, Pixels.Length);
            Touch();
        }

        public void ExposeData()
        {
            int version = Scribe.mode == LoadSaveMode.LoadingVars ? 0 : Version;
            int width = Scribe.mode == LoadSaveMode.LoadingVars ? 0 : Size;
            int height = Scribe.mode == LoadSaveMode.LoadingVars ? 0 : Size;
            int startR = Start.r, startG = Start.g, startB = Start.b;
            int endR = End.r, endG = End.g, endB = End.b;
            Scribe_Values.Look(ref version, "version", 0);
            Scribe_Values.Look(ref width, "width", 0);
            Scribe_Values.Look(ref height, "height", 0);
            Scribe_Values.Look(ref Gradient, "gradient", false);
            Scribe_Values.Look(ref Angle, "angle", 0f);
            Scribe_Values.Look(ref startR, "startR", 255);
            Scribe_Values.Look(ref startG, "startG", 255);
            Scribe_Values.Look(ref startB, "startB", 255);
            Scribe_Values.Look(ref endR, "endR", 0);
            Scribe_Values.Look(ref endG, "endG", 0);
            Scribe_Values.Look(ref endB, "endB", 0);
            if (Scribe.mode == LoadSaveMode.Saving && changed)
            {
                encoded = Encode(Pixels);
                changed = false;
            }
            Scribe_Values.Look(ref encoded, "layer", null);
            if (Scribe.mode != LoadSaveMode.LoadingVars) return;
            customKnown = false;
            if (version != Version || width != Size || height != Size || !ValidByte(startR) || !ValidByte(startG) || !ValidByte(startB) || !ValidByte(endR) || !ValidByte(endG) || !ValidByte(endB) || float.IsNaN(Angle) || float.IsInfinity(Angle) || Math.Abs(Angle) > 36000f || encoded != null && encoded.Length > MaxEncodedLength)
            {
                Invalid();
                return;
            }
            Start = new Color32((byte)startR, (byte)startG, (byte)startB, 255);
            End = new Color32((byte)endR, (byte)endG, (byte)endB, 255);
            if (encoded == null) return;
            try
            {
                byte[] compressed = Convert.FromBase64String(encoded);
                using (MemoryStream input = new MemoryStream(compressed))
                using (DeflateStream stream = new DeflateStream(input, CompressionMode.Decompress))
                {
                    byte[] result = new byte[ByteCount];
                    int offset = 0;
                    while (offset < result.Length)
                    {
                        int count = stream.Read(result, offset, result.Length - offset);
                        if (count == 0) throw new InvalidDataException();
                        offset += count;
                    }
                    if (stream.ReadByte() != -1) throw new InvalidDataException();
                    Pixels = result;
                }
            }
            catch (Exception)
            {
                Invalid();
            }
        }

        private static bool ValidByte(int value) => value >= 0 && value <= 255;

        private void Invalid()
        {
            Clear();
            encoded = null;
            if (!loggedBadData)
            {
                loggedBadData = true;
                Log.Warning("Multicolor Ideology: invalid icon paint data; using the vanilla symbol.");
            }
        }

        private static string Encode(byte[] pixels)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream stream = new DeflateStream(output, CompressionLevel.Optimal, true))
                    stream.Write(pixels, 0, pixels.Length);
                return Convert.ToBase64String(output.ToArray());
            }
        }
    }
}
