using System;
using System.IO;
using System.Collections.Generic;
using ImpromptuNinjas.ZStd;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;

namespace NjJoshMan.Moonlite
{
    class Program
    {
        private static (string ext, int trimOffset) IdentifyExtension(byte[] buffer)
        {
            byte[] pngMagic = { 0x89, 0x50, 0x4E, 0x47 };
            byte[] ktxMagic = { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB };
            byte[] jpgMagic = { 0xFF, 0xD8, 0xFF };
            byte[] oggMagic = { 0x4F, 0x67, 0x67, 0x53 };
            byte[] ozzMagic1 = { 0x01, 0x6F, 0x7A, 0x7A, 0x2D, 0x73, 0x6B, 0x65 };
            byte[] ozzMagic2 = { 0x01, 0x6F, 0x7A, 0x7A, 0x2D, 0x61, 0x6E, 0x69 };
            byte[] fragMagic = { 0x46, 0x53, 0x48, 0x05 };
            byte[] vertMagic = { 0x56, 0x53, 0x48, 0x05 };

            for (int oz1 = 0; oz1 < 128; oz1++)
            {
                if (buffer.Length >= oz1 && (buffer.Skip(oz1).Take(8).SequenceEqual(ozzMagic1) || buffer.Skip(oz1).Take(8).SequenceEqual(ozzMagic2)))
                {
                    return (".ozz", oz1);
                }
            }

            if (buffer.Length >= 128 && (buffer.Skip(80).Take(4).SequenceEqual(vertMagic)))
            {
                return (".vsh", 80);
            }

            if (buffer.Length >= 128 && (buffer.Skip(80).Take(4).SequenceEqual(fragMagic)))
            {
                return (".fsh", 80);
            }

            // png checks
            if (buffer.Length >= 8 && (buffer.Take(4).SequenceEqual(pngMagic)))
            {
                return (".png", 0);
            }

            if (buffer.Skip(4).Take(4).SequenceEqual(pngMagic))
            {
                return (".png", 4);
            }

            if (buffer.Skip(8).Take(4).SequenceEqual(pngMagic))
            {
                return (".png", 8);
            }

            if (buffer.Skip(16).Take(4).SequenceEqual(pngMagic))
            {
                return (".png", 16);
            }

            // khronos texture checks
            if (buffer.Length >= 8 && buffer.Skip(4).Take(8).SequenceEqual(ktxMagic))
            {
                return (".ktx", 4);
            }

            if (buffer.Length >= 8 && buffer.Skip(8).Take(8).SequenceEqual(ktxMagic))
            {
                return (".ktx", 8);
            }

            if (buffer.Length >= 8 && buffer.Skip(16).Take(8).SequenceEqual(ktxMagic))
            {
                return (".ktx", 16);
            }

            // jpg checks
            if (buffer.Length >= 3 && buffer.Take(3).SequenceEqual(jpgMagic))
            {
                return (".jpg", 0);
            }

            if (buffer.Length >= 7 && buffer.Skip(4).Take(3).SequenceEqual(jpgMagic))
            {
                return (".jpg", 4);
            }

            if (buffer.Length >= 13 && buffer.Skip(8).Take(3).SequenceEqual(jpgMagic))
            {
                return (".jpg", 8);
            }

            if (buffer.Length >= 13 && buffer.Skip(16).Take(3).SequenceEqual(jpgMagic))
            {
                return (".jpg", 16);
            }

            // ogg checks
            if (buffer.Length >= 60 && buffer.Skip(4).Take(4).SequenceEqual(oggMagic))
            {
                return (".ogg", 4);
            }

            if (buffer.Length >= 60 && buffer.Skip(8).Take(4).SequenceEqual(oggMagic))
            {
                return (".ogg", 8);
            }

            if (buffer.Length >= 60 && buffer.Skip(16).Take(4).SequenceEqual(oggMagic))
            {
                return (".ogg", 16);
            }

            // failed to find resource, assume internal format
            return (".dat", 0);
        }

        static void Main(string[] args)
        {
            List<Frame> zstds = new List<Frame>();

            byte[] Noo = new byte[]
            {
                0x00,0x00,0x01,0x00
            };

            bool isNew = File.ReadAllBytes(args[0]).Take(4).SequenceEqual(Noo);

            try
            {
                FileStream str = new FileStream(args[0], FileMode.Open);

                using (BinaryReader reader = new BinaryReader(str))
                {
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        zstds.Add(new Frame(reader, isNew));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {args[0]}: {ex.Message}");
                return;
            }

            List<byte[]> decFrames = new List<byte[]>();

            ZStdDecompressor decompressor = new ZStdDecompressor();

            foreach (Frame frame in zstds)
            {
                byte[] tmp = frame.decSize == frame.encSize ? frame.data : new byte[frame.decSize];
                if (frame.decSize != frame.encSize)
                {
                    decompressor.Decompress(tmp, frame.data);
                }
                decFrames.Add(tmp);
            }

            List<byte[]> assets = new List<byte[]>();
            List<byte> temp = new List<byte>();
            List<string> extension = new List<string>();

            for (int i = 0; i < decFrames.Count; i++)
            {
                if (isNew)
                {
                    if (decFrames[i].Length < 65536)
                    {
                        temp.AddRange(decFrames[i]);

                        var (ext, trimOffset) = IdentifyExtension(temp.ToArray());

                        byte[] trimmedData = temp.Skip(trimOffset).ToArray();

                        assets.Add(trimmedData);

                        extension.Add(ext);

                        temp.Clear();
                    }
                    else if (decFrames[i].Length == 65536)
                    {
                        temp.AddRange(decFrames[i]);
                    }
                }
                else
                {
                    if (decFrames[i].Length < 65535)
                    {
                        temp.AddRange(decFrames[i]);

                        var (ext, trimOffset) = IdentifyExtension(temp.ToArray());

                        byte[] trimmedData = temp.Skip(trimOffset).ToArray();

                        assets.Add(trimmedData);

                        extension.Add(ext);

                        temp.Clear();
                    }
                    else if (decFrames[i].Length == 65535)
                    {
                        temp.AddRange(decFrames[i]);
                    }
                }
            }

            int c = 0;

            foreach (byte[] asset in assets)
            {
                string filePath = args[0] + "_extracted\\" + c + "_asset" + extension[c];
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                try
                {
                    using (FileStream str = new FileStream(filePath, FileMode.Create))
                    {
                        str.Write(asset, 0, asset.Length);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing asset {filePath}: {ex.Message}");
                    continue;
                }

                c++;
            }
        }


        public static string ReadString(BinaryReader reader)
        {
            StringBuilder stringBuilder = new StringBuilder();

            while (true)
            {
                try
                {
                    char byteValue = (char)reader.ReadByte();

                    if (byteValue == 0x00) break;

                    stringBuilder.Append(byteValue);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
            }

            return stringBuilder.ToString();
        }

        public struct Frame
        {
            public uint decSize;
            public uint encSize;
            public byte[] data;

            public Frame(BinaryReader reader, bool isNew)
            {
                this.decSize = reader.ReadUInt32();
                this.encSize = reader.ReadUInt32();
                if (!isNew) reader.BaseStream.Seek(8, SeekOrigin.Current);
                if (isNew) reader.BaseStream.Seek(12, SeekOrigin.Current);
                this.data = reader.ReadBytes((int)this.encSize);
            }
        }
    }
}
