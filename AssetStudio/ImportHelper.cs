using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetStudio
{
    public enum FileType
    {
        AssetsFile,
        BundleFile,
        WebFile,
        ResourceFile
    }

    public static class ImportHelper
    {
        public static void MergeSplitAssets(string path, bool allDirectories = false)
        {
            var splitFiles = Directory.GetFiles(path, "*.split0", allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            foreach (var splitFile in splitFiles)
            {
                var destFile = Path.GetFileNameWithoutExtension(splitFile);
                var destPath = Path.GetDirectoryName(splitFile);
                var destFull = Path.Combine(destPath, destFile);
                if (!File.Exists(destFull))
                {
                    var splitParts = Directory.GetFiles(destPath, destFile + ".split*");
                    using (var destStream = File.Create(destFull))
                    {
                        for (int i = 0; i < splitParts.Length; i++)
                        {
                            var splitPart = destFull + ".split" + i;
                            using (var sourceStream = File.OpenRead(splitPart))
                            {
                                sourceStream.CopyTo(destStream);
                            }
                        }
                    }
                }
            }
        }

        public static string[] ProcessingSplitFiles(List<string> selectFile)
        {
            var splitFiles = selectFile.Where(x => x.Contains(".split"))
                .Select(x => Path.Combine(Path.GetDirectoryName(x), Path.GetFileNameWithoutExtension(x)))
                .Distinct()
                .ToList();
            selectFile.RemoveAll(x => x.Contains(".split"));
            foreach (var file in splitFiles)
            {
                if (File.Exists(file))
                {
                    selectFile.Add(file);
                }
            }
            return selectFile.Distinct().ToArray();
        }

        public static FileType CheckFileType(Stream stream, out EndianBinaryReader reader)
        {
            reader = new EndianBinaryReader(stream);
            return CheckFileType(reader);
        }

        public static FileType CheckFileType(string fileName, out EndianBinaryReader reader)
        {
            reader = new EndianBinaryReader(File.OpenRead(fileName));
            return CheckFileType(reader);
        }

        private static FileType CheckFileType(EndianBinaryReader reader)
        {
            var offset = RemoveDummy(reader);
            var signature = reader.ReadStringToNull(20);
            reader.Position = offset;
            switch (signature)
            {
                case "UnityWeb":
                case "UnityRaw":
                case "UnityArchive":
                case "UnityFS":
                    return FileType.BundleFile;
                case "UnityWebData1.0":
                    return FileType.WebFile;
                default:
                    {
                        var magic = reader.ReadBytes(2);
                        reader.Position = 0;
                        if (WebFile.gzipMagic.SequenceEqual(magic))
                        {
                            return FileType.WebFile;
                        }
                        reader.Position = 0x20;
                        magic = reader.ReadBytes(6);
                        reader.Position = 0;
                        if (WebFile.brotliMagic.SequenceEqual(magic))
                        {
                            return FileType.WebFile;
                        }
                        if (SerializedFile.IsSerializedFile(reader))
                        {
                            return FileType.AssetsFile;
                        }
                        else
                        {
                            return FileType.ResourceFile;
                        }
                    }
            }
        }

        public static long RemoveDummy(EndianBinaryReader reader)
        {
            string signature = string.Empty;
            var done = false;
            int inc;
            long offset = 0;

            for (inc = 0; inc < 100 && done == false; inc++)
            {
                signature = reader.ReadStringToNull();
                switch (signature)
                {
                    case "UnityWeb":
                    case "UnityRaw":
                    case "UnityArchive":
                    case "UnityFS":
                    case "UnityWebData1.0":
                        done = true;
                        break;
                }
            }
            if (inc == 0 && done)
                offset = 0;
            if (inc != 0 && done)
                offset = reader.Position - signature.Length - 1;
            reader.Position = offset;
            return offset;
        }
    }
}
