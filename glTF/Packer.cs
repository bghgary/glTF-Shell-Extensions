using glTFLoader;
using glTFLoader.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace glTF
{
    internal class Packer
    {
        public static void Pack(string path)
        {
            var inputFilePath = path;
            var inputDirectoryPath = Path.GetDirectoryName(inputFilePath);
            var inputFileName = Path.GetFileNameWithoutExtension(inputFilePath);

            var model = Interface.LoadModel(inputFilePath);
            byte[] binBufferData = null;

            using (var memoryStream = new MemoryStream())
            {
                var bufferViews = new List<BufferView>();
                var bufferData = new Dictionary<int, byte[]>();

                if (model.BufferViews != null)
                {
                    foreach (var bufferView in model.BufferViews)
                    {
                        memoryStream.Align(4);

                        var byteOffset = memoryStream.Position;

                        byte[] data;
                        if (!bufferData.TryGetValue(bufferView.Buffer, out data))
                        {
                            data = model.LoadBinaryBuffer(bufferView.Buffer, inputFilePath);
                            bufferData.Add(bufferView.Buffer, data);
                        }

                        memoryStream.Write(data, bufferView.ByteOffset, bufferView.ByteLength);

                        bufferView.Buffer = 0;
                        bufferView.ByteOffset = (int)byteOffset;
                        bufferViews.Add(bufferView);
                    }
                }

                if (model.Images != null)
                {
                    for (var index = 0; index < model.Images.Length; index++)
                    {
                        var byteOffset = memoryStream.Position;

                        var data = model.OpenImageFile(index, inputFilePath);
                        data.CopyTo(memoryStream);

                        var image = model.Images[index];
                        image.BufferView = bufferViews.Count;
                        image.MimeType = GetMimeType(image.Uri);
                        image.Uri = null;

                        bufferViews.Add(new BufferView
                        {
                            Buffer = 0,
                            ByteOffset = (int)byteOffset,
                            ByteLength = (int)data.Length,
                        });
                    }
                }

                if (bufferViews.Any())
                {
                    model.BufferViews = bufferViews.ToArray();

                    model.Buffers = new[]
                    {
                        new glTFLoader.Schema.Buffer
                        {
                            ByteLength = (int)memoryStream.Length
                        }
                    };

                    binBufferData = memoryStream.ToArray();
                }
            }

            var outputFilePath = GetUniqueFilePath(inputDirectoryPath, inputFileName, ".glb");
            Interface.SaveBinaryModel(model, binBufferData, outputFilePath);
            SelectFileInExplorer(outputFilePath, true);
        }

        private static Image.MimeTypeEnum? GetMimeType(string uri)
        {
            if (String.IsNullOrEmpty(uri))
            {
                return null;
            }

            if (uri.StartsWith("data:image/png;base64,") || uri.EndsWith(".png"))
            {
                return Image.MimeTypeEnum.image_png;
            }

            if (uri.StartsWith("data:image/jpeg;base64,") || uri.EndsWith(".jpg") || uri.EndsWith(".jpeg"))
            {
                return Image.MimeTypeEnum.image_jpeg;
            }

            throw new InvalidOperationException("Unable to determine mime type from uri");
        }

        private static string GetUniqueFilePath(string directoryPath, string baseFileName, string fileExtension)
        {
            var filePath = Path.Combine(directoryPath, $"{baseFileName}{fileExtension}");

            for (var index = 2; index < 100; index++)
            {
                if (!File.Exists(filePath))
                {
                    break;
                }

                filePath = Path.Combine(directoryPath, $"{baseFileName} ({index}){fileExtension}");
            }

            return filePath;
        }

        private static void SelectFileInExplorer(string path, bool edit)
        {
            var pidlDirectory = IntPtr.Zero;
            var pidlFile = IntPtr.Zero;
            try
            {
                pidlDirectory = NativeMethods.ILCreateFromPath(Path.GetDirectoryName(path));
                pidlFile = NativeMethods.ILCreateFromPath(path);
                NativeMethods.SHOpenFolderAndSelectItems(pidlDirectory, 1, new[] { pidlFile }, edit ? NativeMethods.OFASI_EDIT : 0);
            }
            catch
            {
                NativeMethods.ILFree(pidlFile);
                NativeMethods.ILFree(pidlDirectory);
            }
        }

        private static class NativeMethods
        {
            public const int OFASI_EDIT = 0x0001;

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr ILCreateFromPath([In] string pszPath);

            [DllImport("shell32.dll")]
            public static extern void ILFree([In] IntPtr pidl);

            [DllImport("shell32.dll", EntryPoint = "SHOpenFolderAndSelectItems")]
            public static extern void SHOpenFolderAndSelectItems([In] IntPtr pidlFolder, uint cidl, [In, Optional, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, int dwFlags);
        }
    }
}