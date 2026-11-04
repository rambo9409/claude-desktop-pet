using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Web.Script.Serialization;
using System.Windows.Media.Imaging;

namespace ClaudePet
{
    internal static class PetAssets
    {
        private sealed class NotebookBox { internal Rect Bounds; }
        private static readonly ConditionalWeakTable<BitmapSource, NotebookBox> notebookBoxes = new ConditionalWeakTable<BitmapSource, NotebookBox>();
        internal static Rect NotebookHitBox(BitmapSource image)
        { NotebookBox value; return image != null && notebookBoxes.TryGetValue(image, out value) ? value.Bounds : Rect.Empty; }
        private static readonly Dictionary<string, int> CommonStates = new Dictionary<string, int> {
            { "idle", 1 }, { "happy", 2 }, { "pet", 2 }, { "rose", 2 }, { "typing", 2 },
            { "walk", 4 }, { "stroll", 4 }, { "climb-up", 4 }
        };

        internal static bool HasWindGlider(string character)
        { return character == "hutao" || character == "ganyu" || character == "varesa"; }

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
                if (character == "kolo" || character == "gpt6a" || character == "deepseek") frames["ability"] = LoadSeries(archive, character, "ability", 4);
                if (character == "kolo" && archive.GetEntry("kolo-disguise-1.png") != null)
                    frames["disguise"] = LoadSeries(archive, character, "disguise", 8);
                if (character == "ganyu") frames["jump"] = LoadSeries(archive, character, "jump", 4);
                if (character == "hutao") frames["run-human"] = LoadSeries(archive, character, "run-human", 4);
                if (character == "kolo" && archive.GetEntry("kolo-book-idle.png") != null)
                {
                    ZipArchiveEntry metadata = archive.GetEntry("kolo-notebook-hitboxes.json");
                    if (metadata == null) throw new InvalidOperationException("缺少小本本交互区域数据。");
                    Dictionary<string, double[]> bounds;
                    using (StreamReader reader = new StreamReader(metadata.Open()))
                        bounds = new JavaScriptSerializer().Deserialize<Dictionary<string, double[]>>(reader.ReadToEnd());
                    foreach (KeyValuePair<string, int> series in CommonStates)
                    {
                        BitmapSource[] held = LoadSeries(archive, character, "book-" + series.Key, series.Value);
                        frames["book-" + series.Key] = held;
                        for (int i = 0; i < held.Length; i++) {
                            string name = "kolo-book-" + series.Key + (held.Length == 1 ? "" : "-" + (i + 1)) + ".png";
                            double[] box;
                            if (!bounds.TryGetValue(name, out box) || box.Length != 4 || box[0] < 0 || box[1] < 0 || box[2] <= 0 || box[3] <= 0 || box[0] + box[2] > 512 || box[1] + box[3] > 512)
                                throw new InvalidOperationException("小本本交互区域无效：" + name);
                            notebookBoxes.Add(held[i], new NotebookBox { Bounds = new Rect(box[0], box[1], box[2], box[3]) });
                        }
                    }
                    frames["book-climb-down"] = (BitmapSource[])frames["book-climb-up"].Clone();
                    Array.Reverse(frames["book-climb-down"]);
                    frames["notebook"] = LoadSeries(archive, character, "notebook", 1);
                }
                if (character == "gpt6a" || character == "deepseek")
                {
                    if (archive.GetEntry(character + "-fall.png") != null) frames["fall"] = LoadSeries(archive, character, "fall", 1);
                    if (archive.GetEntry(character + "-held.png") != null) frames["held"] = LoadSeries(archive, character, "held", 1);
                }
                if (HasWindGlider(character))
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
