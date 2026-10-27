using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    internal static class PetAssets
    {
        private static readonly Dictionary<string, int> CommonStates = new Dictionary<string, int> {
            { "idle", 1 }, { "happy", 2 }, { "pet", 2 }, { "rose", 2 }, { "typing", 2 },
            { "walk", 4 }, { "stroll", 4 }, { "climb-up", 4 }
        };

        internal static Dictionary<string, BitmapSource[]> Load(string character)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("characters.zip");
            if (stream == null) throw new InvalidOperationException("缺少角色图片包。");
            using (stream)
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                Dictionary<string, BitmapSource[]> frames = new Dictionary<string, BitmapSource[]>();
                foreach (KeyValuePair<string, int> state in CommonStates)
                    frames[state.Key] = LoadSeries(archive, character, state.Key, state.Value);
                // Descending uses the exact same painted poses in reverse order.
                // Share frozen bitmaps instead of storing and decoding duplicates.
                frames["climb-down"] = (BitmapSource[])frames["climb-up"].Clone();
                Array.Reverse(frames["climb-down"]);
                if (character == "kolo") frames["ability"] = LoadSeries(archive, character, "ability", 4);
                if (character == "ganyu") frames["jump"] = LoadSeries(archive, character, "jump", 4);
                if (character == "hutao") frames["run-human"] = LoadSeries(archive, character, "run-human", 4);
                if (character != "kolo")
                {
                    frames["glider"] = LoadSeries(archive, character, "glider", 1);
                    frames["glide"] = LoadSeries(archive, character, "glide", 8);
                }
                return frames;
            }
        }

        private static BitmapSource[] LoadSeries(ZipArchive archive, string character, string state, int count)
        {
            BitmapSource[] frames = new BitmapSource[count];
            for (int i = 0; i < count; i++)
            {
                string name = character + "-" + state + (count == 1 ? "" : "-" + (i + 1)) + ".png";
                ZipArchiveEntry entry = archive.GetEntry(name);
                if (entry == null) throw new InvalidOperationException("缺少角色图片：" + name);
                using (Stream image = entry.Open())
                using (MemoryStream buffer = new MemoryStream())
                {
                    image.CopyTo(buffer);
                    buffer.Position = 0;
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    bitmap.StreamSource = buffer;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    frames[i] = bitmap;
                }
            }
            return frames;
        }
    }
}
