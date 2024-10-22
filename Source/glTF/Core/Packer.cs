using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace glTF
{
    internal class Packer
    {
        public static void Pack(string inputFilePath, string outputFilePath)
        {
            JsonNode jsonNode;
            using (var jsonStream = File.OpenRead(inputFilePath))
            {
                jsonNode = JsonNode.Parse(jsonStream);
            }

            var baseUri = new Uri(inputFilePath);

            var position = 0;

            var memoryMappedFiles = new Dictionary<string, MemoryMappedFile>();
            var viewStreams = new List<MemoryMappedViewStream>();

            var buffers = jsonNode["buffers"]?.AsArray();
            var bufferViews = jsonNode["bufferViews"]?.AsArray();
            var images = jsonNode["images"]?.AsArray();

            if (buffers != null)
            {
                for (var index = buffers.Count - 1; index >= 0; index--)
                {
                    var buffer = buffers[index];
                    var uriString = (string)buffer["uri"];
                    if (!Uri.TryCreate(baseUri, uriString, out var uri))
                    {
                        throw new InvalidDataException($"Invalid URI: {uriString}");
                    }

                    foreach (var bufferView in bufferViews)
                    {
                        var bufferIndex = (int)bufferView["buffer"];
                        if (bufferIndex == index)
                        {
                            bufferView["buffer"] = -1;

                            var byteOffset = (int?)bufferView["byteOffset"] ?? 0;
                            bufferView.SetValue("byteOffset", position + byteOffset, 0);
                        }
                    }

                    var filePath = uri.LocalPath;
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

            if (images != null)
            {
                foreach (var image in images)
                {
                    var uriString = (string)image["uri"];
                    if (!Uri.TryCreate(baseUri, uriString, out var uri))
                    {
                        throw new InvalidDataException($"Invalid URI: {uriString}");
                    }

                    var filePath = uri.LocalPath;
                    if (!memoryMappedFiles.TryGetValue(filePath, out MemoryMappedFile memoryMappedFile))
                    {
                        memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
                        memoryMappedFiles.Add(filePath, memoryMappedFile);
                    }

                    var fileLength = Tools.GetFileLength(filePath);
                    viewStreams.Add(memoryMappedFile.CreateViewStream(0, fileLength, MemoryMappedFileAccess.Read));

                    image.AsObject().Remove("uri");
                    image["bufferView"] = bufferViews.Count;
                    image["mimeType"] = MimeType.FromFileExtension(Path.GetExtension(filePath));

                    position = Tools.Align(position);

                    var bufferView = new JsonObject
                    {
                        ["buffer"] = -1,
                        ["byteLength"] = fileLength
                    };

                    bufferView.SetValue("byteOffset", position, 0);
                    bufferViews.Add((JsonNode)bufferView);

                    position += fileLength;
                }
            }

            jsonNode.AsObject().Remove("buffers");
            if (viewStreams.Count != 0)
            {
                buffers = [];
                buffers.Insert(0, new JsonObject
                {
                    ["byteLength"] = position
                });

                foreach (var bufferView in bufferViews)
                {
                    var bufferIndex = (int)bufferView["buffer"];
                    bufferView["buffer"] = bufferIndex + 1;
                }

                jsonNode["buffers"] = buffers;
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

                using (var jsonTextWriter = new Utf8JsonWriter(binaryWriter.BaseStream))
                {
                    jsonNode.WriteTo(jsonTextWriter);
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