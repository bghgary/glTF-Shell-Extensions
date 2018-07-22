using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;

namespace glTF
{
    internal class Unpacker
    {
        private readonly string inputFilePath;
        private readonly string inputDirectoryPath;
        private readonly string inputFileName;
        private readonly string outputDirectoryPath;
        private readonly bool unpackImages;

        private Unpacker(string inputFilePath, string outputDirectoryPath, bool unpackImages)
        {
            this.inputFilePath = inputFilePath;
            this.inputDirectoryPath = Path.GetDirectoryName(this.inputFilePath);
            this.inputFileName = Path.GetFileNameWithoutExtension(this.inputFilePath);
            this.outputDirectoryPath = outputDirectoryPath;
            this.unpackImages = unpackImages;
        }

        public static void Unpack(string inputFilePath, string outputDirectoryPath, bool unpackImages)
        {
            new Unpacker(inputFilePath, outputDirectoryPath, unpackImages).Unpack();
        }

        private void Unpack()
        {
            JObject json;

            var inputFileLength = Tools.GetFileLength(this.inputFilePath);

            using (var memoryMappedFile = MemoryMappedFile.CreateFromFile(this.inputFilePath, FileMode.Open))
            using (var viewStream = memoryMappedFile.CreateViewStream(0, inputFileLength, MemoryMappedFileAccess.Read))
            using (var binaryReader = new BinaryReader(viewStream))
            {
                var magic = binaryReader.ReadUInt32();
                if (magic != Binary.Magic)
                {
                    throw new InvalidDataException($"Unexpected magic: {magic:X8}");
                }

                var version = binaryReader.ReadUInt32();
                if (version != 2)
                {
                    throw new InvalidDataException($"Unsupported version: {version}");
                }

                var length = binaryReader.ReadInt32();
                if (length != viewStream.Length)
                {
                    throw new InvalidDataException($"Length in header does not match actual data length: {length} != {viewStream.Length}");
                }

                using (var jsonStream = GetJsonChunk(binaryReader, memoryMappedFile))
                using (var jsonStreamReader = new StreamReader(jsonStream))
                using (var jsonTextReader = new JsonTextReader(jsonStreamReader))
                {
                    json = (JObject)JToken.ReadFrom(jsonTextReader);
                }


                var binChunkOffset = FindBinChunk(binaryReader);
                this.ProcessImageFiles(json, memoryMappedFile, binChunkOffset);
                this.ProcessBinFiles(json, memoryMappedFile, binChunkOffset);
            }

            using (var streamWriter = new StreamWriter(Path.Combine(this.outputDirectoryPath, $"{this.inputFileName}.gltf")))
            using (var jsonTextWriter = new JsonTextWriter(streamWriter))
            {
                jsonTextWriter.Formatting = Formatting.Indented;
                json.WriteTo(jsonTextWriter);
            }
        }

        private static Stream GetJsonChunk(BinaryReader binaryReader, MemoryMappedFile memoryMappedFile)
        {
            var chunkLength = binaryReader.ReadUInt32();
            var chunkFormat = binaryReader.ReadUInt32();
            if (chunkFormat != Binary.ChunkFormatJson)
            {
                throw new InvalidDataException("First chunk format must be JSON");
            }

            var startPosition = binaryReader.BaseStream.Position;
            binaryReader.BaseStream.Position += chunkLength;

            return memoryMappedFile.CreateViewStream(startPosition, chunkLength, MemoryMappedFileAccess.Read);
        }

        private static long FindBinChunk(BinaryReader binaryReader)
        {
            // Look for BIN chunk
            while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
            {
                var chunkLength = binaryReader.ReadUInt32();
                var chunkFormat = binaryReader.ReadUInt32();
                switch (chunkFormat)
                {
                    case Binary.ChunkFormatJson:
                    {
                        throw new InvalidDataException("Unexpected JSON chunk");
                    }
                    case Binary.ChunkFormatBin:
                    {
                        return binaryReader.BaseStream.Position;
                    }
                    default:
                    {
                        // ignore unrecognized chunkFormat
                        binaryReader.BaseStream.Position += chunkLength;
                        break;
                    }
                }
            }

            return -1;
        }

        private void ProcessImageFiles(JObject json, MemoryMappedFile memoryMappedFile, long binChunkOffset)
        {
            var accessors = (JArray)json["accessors"];
            var bufferViews = (JArray)json["bufferViews"];
            var images = (JArray)json["images"];

            var bufferViewIndicesToRemove = new List<int>();

            if (images != null)
            {
                for (var index = 0; index < images.Count; index++)
                {
                    var image = (JObject)images[index];
                    var uri = (string)image["uri"];
                    if (uri != null)
                    {
                        if (!Tools.IsBase64(uri))
                        {
                            var sourceFilePath = Path.Combine(this.inputDirectoryPath, Path.GetFileName(uri));
                            var fileExtension = Path.GetExtension(uri);
                            var fileName = $"{this.inputFileName}_image{index}.{fileExtension}";

                            if (File.Exists(sourceFilePath))
                            {
                                var destinationFilePath = Path.Combine(this.outputDirectoryPath, fileName);
                                File.Copy(sourceFilePath, destinationFilePath, true);
                            }

                            image["uri"] = fileName;
                        }
                    }
                    else if (this.unpackImages && bufferViews != null && binChunkOffset != -1)
                    {
                        var bufferViewIndex = (int)image["bufferView"];
                        var bufferView = bufferViews[bufferViewIndex];
                        var bufferIndex = (int)bufferView["buffer"];
                        if (bufferIndex == 0)
                        {
                            var mimeType = (string)image["mimeType"];
                            var fileExtension = MimeType.ToFileExtension(mimeType);
                            var fileName = $"{this.inputFileName}_image{index}{fileExtension}";

                            using (var fileStream = File.Create(Path.Combine(this.outputDirectoryPath, fileName)))
                            {
                                var byteOffset = (long?)bufferView["byteOffset"] ?? 0;
                                var byteLength = (int)bufferView["byteLength"];

                                using (var viewStream = memoryMappedFile.CreateViewStream(binChunkOffset + byteOffset, byteLength, MemoryMappedFileAccess.Read))
                                {
                                    viewStream.CopyTo(fileStream);
                                }
                            }

                            image.Remove("bufferView");
                            image.Remove("mimeType");
                            image["uri"] = fileName;

                            bufferViewIndicesToRemove.Add(bufferViewIndex);
                        }
                    }
                }
            }

            var bufferViewIndexMap = new Dictionary<int, int>();

            if (bufferViews != null && bufferViewIndicesToRemove.Any())
            {
                var newBufferViews = new JArray();
                for (var index = 0; index < bufferViews.Count; index++)
                {
                    if (!bufferViewIndicesToRemove.Contains(index))
                    {
                        var newIndex = newBufferViews.Count;
                        bufferViewIndexMap.Add(index, newIndex);
                        newBufferViews.Add(bufferViews[index]);
                    }
                }

                if (newBufferViews.Count == 0)
                {
                    json.Remove("bufferViews");
                }
                else
                {
                    json["bufferViews"] = newBufferViews;
                }
            }

            if (accessors != null && bufferViewIndexMap.Any())
            {
                foreach (var accessor in accessors)
                {
                    var bufferViewIndex = (int?)accessor["bufferView"];
                    if (bufferViewIndex.HasValue)
                    {
                        if (bufferViewIndexMap.TryGetValue(bufferViewIndex.Value, out int newBufferViewIndex))
                        {
                            accessor["bufferView"] = newBufferViewIndex;
                        }
                    }
                }
            }
        }

        private void ProcessBinFiles(JObject json, MemoryMappedFile memoryMappedFile, long binChunkOffset)
        {
            var buffers = (JArray)json["buffers"];
            if (buffers != null)
            {
                for (var index = 0; index < buffers.Count; index++)
                {
                    var buffer = buffers[index];
                    var uri = (string)buffer["uri"];
                    if (uri != null && !uri.StartsWith("data:"))
                    {
                        var sourceFilePath = Path.Combine(this.inputDirectoryPath, Path.GetFileName(uri));
                        var fileName = $"{this.inputFileName}{index}.bin";

                        if (File.Exists(sourceFilePath))
                        {
                            var destinationFilePath = Path.Combine(this.outputDirectoryPath, fileName);
                            File.Copy(sourceFilePath, destinationFilePath, true);
                        }

                        buffer["uri"] = fileName;
                    }
                }

                var bufferViews = (JArray)json["bufferViews"];
                if (bufferViews != null && binChunkOffset != -1)
                {
                    if (bufferViews.Any(bufferView => (int)bufferView["buffer"] == 0))
                    {
                        var binFileName = $"{this.inputFileName}.bin";
                        var binFilePath = Path.Combine(this.outputDirectoryPath, binFileName);
                        using (var fileStream = File.Create(binFilePath))
                        {
                            var buffer = buffers[0];
                            buffer["uri"] = binFileName;

                            if (this.unpackImages)
                            {
                                foreach (var bufferView in bufferViews)
                                {
                                    if ((int)bufferView["buffer"] == 0)
                                    {
                                        fileStream.Align();
                                        var outputOffset = fileStream.Position;

                                        var byteOffset = (long?)bufferView["byteOffset"] ?? 0;
                                        var byteLength = (int)bufferView["byteLength"];
                                        using (var viewStream = memoryMappedFile.CreateViewStream(binChunkOffset + byteOffset, byteLength, MemoryMappedFileAccess.Read))
                                        {
                                            viewStream.CopyTo(fileStream, byteLength);
                                        }

                                        bufferView["byteOffset"] = outputOffset;
                                    }
                                }

                                buffer["byteLength"] = fileStream.Length;
                            }
                            else
                            {
                                var byteLength = (int)buffer["byteLength"];
                                using (var viewStream = memoryMappedFile.CreateViewStream(binChunkOffset, byteLength, MemoryMappedFileAccess.Read))
                                {
                                    viewStream.CopyTo(fileStream, byteLength);
                                }
                            }
                        }
                    }
                    else
                    {
                        buffers.RemoveAt(0);
                        json["buffers"] = buffers;

                        foreach (var bufferView in bufferViews)
                        {
                            var bufferIndex = (int)bufferView["buffer"];
                            bufferView["buffer"] = bufferIndex - 1;
                        }
                    }
                }
            }
        }
    }
}
