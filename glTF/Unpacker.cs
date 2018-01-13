using glTFLoader;
using glTFLoader.Schema;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace glTF
{
    internal class Unpacker
    {
        public static void Unpack(string inputFilePath, string outputDirectoryPath)
        {
            string inputFileName = Path.GetFileNameWithoutExtension(inputFilePath);
            string inputDirectoryPath = Path.GetDirectoryName(inputFilePath);

            var model = Interface.LoadModel(inputFilePath);
            Buffer binBuffer = null;
            byte[] binBufferData = null;
            if (model.Buffers != null && string.IsNullOrEmpty(model.Buffers[0].Uri))
            {
                binBuffer = model.Buffers[0];
                binBufferData = model.LoadBinaryBuffer(0, inputFilePath);
            }

            var imageBufferViewIndices = new List<int>();

            if (model.Images != null)
            {
                for (var index = 0; index < model.Images.Length; index++)
                {
                    var image = model.Images[index];

                    if (!string.IsNullOrEmpty(image.Uri))
                    {
                        if (!image.Uri.StartsWith("data:"))
                        {
                            var sourceFilePath = Path.Combine(inputDirectoryPath, image.Uri);
                            var fileName = $"{inputFilePath}_image{index}.bin";

                            if (File.Exists(sourceFilePath))
                            {
                                var destinationFilePath = Path.Combine(outputDirectoryPath, fileName);
                                File.Copy(sourceFilePath, destinationFilePath, true);
                            }

                            image.Uri = fileName;
                        }
                    }
                    else if (image.BufferView.HasValue)
                    {
                        var bufferView = model.BufferViews[image.BufferView.Value];
                        if (bufferView.Buffer == 0)
                        {
                            imageBufferViewIndices.Add(image.BufferView.Value);

                            var fileExtension = image.MimeType == Image.MimeTypeEnum.image_jpeg ? "jpg" : "png";
                            var fileName = $"{inputFileName}_image{index}.{fileExtension}";

                            using (var fileStream = File.Create(Path.Combine(outputDirectoryPath, fileName)))
                            {
                                fileStream.Write(binBufferData, bufferView.ByteOffset, bufferView.ByteLength);
                            }

                            image.BufferView = null;
                            image.MimeType = null;
                            image.Uri = fileName;
                        }
                    }
                }
            }

            if (model.BufferViews != null)
            {
                var binFileName = $"{inputFileName}.bin";
                var binFilePath = Path.Combine(outputDirectoryPath, binFileName);
                var binByteLength = 0;

                var indexMap = new Dictionary<int, int>();
                var bufferViews = new List<BufferView>();
                using (var fileStream = File.Create(binFilePath))
                {
                    for (var index = 0; index < model.BufferViews.Length; index++)
                    {
                        if (!imageBufferViewIndices.Any(imageIndex => imageIndex == index))
                        {
                            var bufferView = model.BufferViews[index];
                            if (bufferView.Buffer == 0)
                            {
                                fileStream.Align(4);
                                var fileStreamPosition = fileStream.Position;
                                fileStream.Write(binBufferData, bufferView.ByteOffset, bufferView.ByteLength);
                                bufferView.ByteOffset = (int)fileStreamPosition;
                            }

                            var newIndex = bufferViews.Count;
                            if (index != newIndex)
                            {
                                indexMap.Add(index, newIndex);
                            }

                            bufferViews.Add(bufferView);
                        }
                    }

                    binByteLength = (int)fileStream.Length;
                }

                model.BufferViews = bufferViews.ToArray();

                if (binByteLength == 0)
                {
                    File.Delete(binFilePath);
                    if (binBuffer != null)
                    {
                        model.Buffers = model.Buffers.Skip(1).ToArray();
                        foreach (var bufferView in model.BufferViews)
                        {
                            bufferView.Buffer--;
                        }
                    }
                }
                else
                {
                    binBuffer.Uri = binFileName;
                    binBuffer.ByteLength = binByteLength;
                }

                if (model.Accessors != null)
                {
                    foreach (var accessor in model.Accessors)
                    {
                        if (accessor.BufferView.HasValue)
                        {
                            if (indexMap.TryGetValue(accessor.BufferView.Value, out int newIndex))
                            {
                                accessor.BufferView = newIndex;
                            }
                        }
                    }
                }
            }

            if (model.Buffers != null)
            {
                for (var index = 1; index < model.Buffers.Length; index++)
                {
                    var buffer = model.Buffers[index];
                    if (!buffer.Uri.StartsWith("data:"))
                    {
                        var sourceFilePath = Path.Combine(inputDirectoryPath, buffer.Uri);
                        var fileName = $"{inputFileName}{index}.bin";

                        if (File.Exists(sourceFilePath))
                        {
                            var destinationFilePath = Path.Combine(outputDirectoryPath, fileName);
                            File.Copy(sourceFilePath, destinationFilePath, true);
                        }

                        buffer.Uri = fileName;
                    }
                }
            }

            Interface.SaveModel(model, Path.Combine(outputDirectoryPath, $"{inputFileName}.gltf"));
        }
    }
}
