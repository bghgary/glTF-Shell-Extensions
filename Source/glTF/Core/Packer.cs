using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace glTF
{
    internal class Packer
    {
        public static void Pack(string inputFilePath, string outputFilePath)
        {
            var inputDirectoryPath = Path.GetDirectoryName(inputFilePath);

            JObject json;
            using (var jsonStream = File.OpenRead(inputFilePath))
            using (var jsonStreamReader = new StreamReader(jsonStream))
            using (var jsonTextReader = new JsonTextReader(jsonStreamReader))
            {
                json = (JObject)JToken.ReadFrom(jsonTextReader);
            }

            var position = 0;

            var memoryMappedFiles = new Dictionary<string, MemoryMappedFile>();
            var viewStreams = new List<MemoryMappedViewStream>();

            var buffers = (JArray)json["buffers"];
            var bufferViews = (JArray)json["bufferViews"];
            var images = (JArray)json["images"];

            if (buffers != null)
            {
                for (var index = buffers.Count - 1; index >= 0; index--)
                {
                    var buffer = (JObject)buffers[index];
                    var uri = (string)buffer["uri"];
                    if (uri != null && !Tools.IsBase64(uri))
                    {
                        foreach (JObject bufferView in bufferViews)
                        {
                            var bufferIndex = (int)bufferView["buffer"];
                            if (bufferIndex == index)
                            {
                                bufferView["buffer"] = -1;

                                var byteOffset = (int?)bufferView["byteOffset"] ?? 0;
                                bufferView.SetValue("byteOffset", position + byteOffset, 0);
                            }
                        }

                        var filePath = Path.Combine(inputDirectoryPath, uri);
                        if (!memoryMappedFiles.TryGetValue(filePath, out MemoryMappedFile memoryMappedFile))
                        {
                            memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
                            memoryMappedFiles.Add(filePath, memoryMappedFile);
                        }

                        var fileLength = Tools.GetFileLength(filePath);
                        viewStreams.Add(memoryMappedFile.CreateViewStream(0, fileLength, MemoryMappedFileAccess.Read));

                        position += fileLength;
                        position = Tools.Align(position);

                        buffers.RemoveAt(index);
                    }
                }
            }

            if (images != null)
            {
                foreach (JObject image in images)
                {
                    var uri = (string)image["uri"];
                    if (uri != null && !Tools.IsBase64(uri))
                    {
                        var filePath = Path.Combine(inputDirectoryPath, uri);
                        if (!memoryMappedFiles.TryGetValue(filePath, out MemoryMappedFile memoryMappedFile))
                        {
                            memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
                            memoryMappedFiles.Add(filePath, memoryMappedFile);
                        }

                        var fileLength = Tools.GetFileLength(filePath);
                        viewStreams.Add(memoryMappedFile.CreateViewStream(0, fileLength, MemoryMappedFileAccess.Read));

                        image.Remove("uri");
                        image["bufferView"] = bufferViews.Count;
                        image["mimeType"] = MimeType.FromFileExtension(Path.GetExtension(uri));

                        position = Tools.Align(position);

                        var bufferView = new JObject
                        {
                            ["buffer"] = -1,
                            ["byteLength"] = fileLength
                        };

                        bufferView.SetValue("byteOffset", position, 0);
                        bufferViews.Add(bufferView);

                        position += fileLength;
                    }
                }
            }

            if (viewStreams.Count != 0)
            {
                if (buffers == null)
                {
                    json["buffers"] = new JArray();
                }

                buffers.Insert(0, new JObject
                {
                    ["byteLength"] = position
                });

                foreach (var bufferView in bufferViews)
                {
                    var bufferIndex = (int)bufferView["buffer"];
                    bufferView["buffer"] = bufferIndex + 1;
                }
            }

            using (var fileStream = File.Create(outputFilePath))
            using (var binaryWriter = new BinaryWriter(fileStream))
            {
                binaryWriter.Write(Binary.Magic);
                binaryWriter.Write(Binary.Version);

                var chunksPosition = binaryWriter.BaseStream.Position;

                binaryWriter.Write(0U); // length

                var jsonChunkPosition = binaryWriter.BaseStream.Position;

                binaryWriter.Write(0U); // json chunk length
                binaryWriter.Write(Binary.ChunkFormatJson);

                using (var streamWriter = new StreamWriter(binaryWriter.BaseStream, new UTF8Encoding(false, true), 1024, true))
                using (var jsonTextWriter = new JsonTextWriter(streamWriter))
                {
                    json.WriteTo(jsonTextWriter);
                }

                binaryWriter.BaseStream.Align(0x20);
                var jsonChunkLength = checked((uint)(binaryWriter.BaseStream.Length - jsonChunkPosition)) - Binary.ChunkHeaderLength;

                binaryWriter.BaseStream.Seek(jsonChunkPosition, SeekOrigin.Begin);
                binaryWriter.Write(jsonChunkLength);

                if (viewStreams.Count != 0)
                {
                    binaryWriter.BaseStream.Seek(0, SeekOrigin.End);
                    var binChunkPosition = binaryWriter.BaseStream.Position;

                    binaryWriter.Write(0); // bin chunk length
                    binaryWriter.Write(Binary.ChunkFormatBin);

                    foreach (var viewStream in viewStreams)
                    {
                        binaryWriter.BaseStream.Align();
                        viewStream.CopyTo(binaryWriter.BaseStream);
                    }

                    binaryWriter.BaseStream.Align(0x20);
                    var binChunkLength = checked((uint)(binaryWriter.BaseStream.Length - binChunkPosition)) - Binary.ChunkHeaderLength;

                    binaryWriter.BaseStream.Seek(binChunkPosition, SeekOrigin.Begin);
                    binaryWriter.Write(binChunkLength);
                }

                var length = checked((uint)binaryWriter.BaseStream.Length);

                binaryWriter.BaseStream.Seek(chunksPosition, SeekOrigin.Begin);
                binaryWriter.Write(length);
            }

            foreach (var viewStream in viewStreams)
            {
                viewStream.Dispose();
            }

            foreach (var memoryMappedFile in memoryMappedFiles.Values)
            {
                memoryMappedFile.Dispose();
            }
        }
    }
}