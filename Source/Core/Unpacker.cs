using System.IO.MemoryMappedFiles;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace glTF
{
    public class Unpacker
    {
        private readonly string inputFilePath;
        private readonly string inputDirectoryPath;
        private readonly string inputFileName;
        private readonly string outputDirectoryPath;
        private readonly bool unpackImages;

        private Unpacker(string inputFilePath, string outputDirectoryPath, bool unpackImages)
        {
            this.inputFilePath = inputFilePath;
            this.inputDirectoryPath = Path.GetDirectoryName(this.inputFilePath)!;
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
            JsonNode jsonNode;

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
                {
                    jsonNode = JsonNode.Parse(jsonStream)!;
                }

                var binChunkOffset = FindBinChunk(binaryReader);
                this.ProcessImageFiles(jsonNode, memoryMappedFile, binChunkOffset);
                this.ProcessBinFiles(jsonNode, memoryMappedFile, binChunkOffset);
            }

            Directory.CreateDirectory(this.outputDirectoryPath);
            using (var fileStream = File.Create(Path.Combine(this.outputDirectoryPath, $"{this.inputFileName}.gltf")))
            using (var jsonWriter = new Utf8JsonWriter(fileStream, new() { Indented = true }))
            {
                jsonNode.WriteTo(jsonWriter);
            }
        }

        private static MemoryMappedViewStream GetJsonChunk(BinaryReader binaryReader, MemoryMappedFile memoryMappedFile)
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

        private void ProcessImageFiles(JsonNode node, MemoryMappedFile memoryMappedFile, long binChunkOffset)
        {
            var accessors = node.GetArray("accessors");
            var bufferViews = node.GetArray("bufferViews");
            var images = node.GetArray("images");

            var bufferViewIndicesToRemove = new List<int>();

            if (images != null)
            {
                for (var index = 0; index < images.Count; index++)
                {
                    var image = images[index];
                    if (image == null)
                    {
                        continue;
                    }

                    var uri = image.GetString("uri");
                    if (uri != null && Uri.IsWellFormedUriString(uri, UriKind.Relative))
                    {
                        var sourceFilePath = Path.Combine(this.inputDirectoryPath, uri.Replace('/', Path.DirectorySeparatorChar));
                        var fileExtension = Path.GetExtension(uri);
                        var fileName = $"{this.inputFileName}_image{index}.{fileExtension}";

                        if (File.Exists(sourceFilePath))
                        {
                            var destinationFilePath = Path.Combine(this.outputDirectoryPath, fileName);
                            File.Copy(sourceFilePath, destinationFilePath, true);
                        }

                        image.Set("uri", fileName);
                    }
                    else if (this.unpackImages && bufferViews != null && binChunkOffset != -1)
                    {
                        var bufferViewIndex = image.GetInt("bufferView");
                        if (bufferViewIndex != -1)
                        {
                            var bufferView = bufferViews[bufferViewIndex];
                            if (bufferView != null)
                            {
                                var bufferIndex = bufferView.GetInt("buffer");
                                if (bufferIndex == 0)
                                {
                                    var byteOffset = bufferView.GetInt("byteOffset", 0);
                                    var byteLength = bufferView.GetInt("byteLength", -1);
                                    if (byteLength != -1)
                                    {
                                        var mimeType = image.GetString("mimeType");
                                        var fileExtension = MimeType.ToFileExtension(mimeType);
                                        var fileName = $"{this.inputFileName}_image{index}{fileExtension}";

                                        Directory.CreateDirectory(this.outputDirectoryPath);

                                        using (var fileStream = File.Create(Path.Combine(this.outputDirectoryPath, fileName)))
                                        {
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
                    }
                }
            }

            var bufferViewIndexMap = new Dictionary<int, int>();

            if (bufferViews != null && bufferViewIndicesToRemove.Count > 0)
            {
                var newIndex = 0;
                for (var index = 0; index < bufferViews.Count; index++)
                {
                    if (!bufferViewIndicesToRemove.Contains(index))
                    {
                        if (index != newIndex)
                        {
                            bufferViewIndexMap.Add(index, newIndex);
                        }

                        newIndex++;
                    }
                }

                if (newIndex == 0)
                {
                    node.Remove("bufferViews");
                }
                else
                {
                    foreach (var indexToRemove in bufferViewIndicesToRemove.OrderDescending())
                    {
                        bufferViews.RemoveAt(indexToRemove);
                    }
                }
            }

            if (accessors != null && bufferViewIndexMap.Count > 0)
            {
                foreach (var accessor in accessors)
                {
                    if (accessor == null)
                    {
                        continue;
                    }

                    var bufferViewIndex = accessor.GetInt("bufferView");
                    if (bufferViewIndex != -1)
                    {
                        if (bufferViewIndexMap.TryGetValue(bufferViewIndex, out int newBufferViewIndex))
                        {
                            accessor["bufferView"] = newBufferViewIndex;
                        }
                    }
                }
            }
        }

        private void ProcessBinFiles(JsonNode node, MemoryMappedFile memoryMappedFile, long binChunkOffset)
        {
            var buffers = node.GetArray("buffers");
            if (buffers != null)
            {
                for (var index = 0; index < buffers.Count; index++)
                {
                    var buffer = buffers[index];
                    if (buffer == null)
                    {
                        continue;
                    }

                    var uri = buffer.GetString("uri");
                    if (uri != null && Uri.IsWellFormedUriString(uri, UriKind.Relative))
                    {
                        var sourceFilePath = Path.Combine(this.inputDirectoryPath, uri.Replace('/', Path.DirectorySeparatorChar));
                        var fileName = $"{this.inputFileName}{index}.bin";

                        if (File.Exists(sourceFilePath))
                        {
                            var destinationFilePath = Path.Combine(this.outputDirectoryPath, fileName);
                            File.Copy(sourceFilePath, destinationFilePath, true);
                        }

                        buffer["uri"] = fileName;
                    }
                }

                var firstBuffer = buffers[0];
                if (firstBuffer == null)
                {
                    return;
                }

                var bufferViews = node.GetArray("bufferViews");
                if (bufferViews != null && binChunkOffset != -1)
                {
                    if (bufferViews.Any(bufferView => bufferView != null && bufferView.GetInt("buffer") == 0))
                    {
                        Directory.CreateDirectory(this.outputDirectoryPath);

                        var binFileName = $"{this.inputFileName}.bin";
                        var binFilePath = Path.Combine(this.outputDirectoryPath, binFileName);
                        using (var fileStream = File.Create(binFilePath))
                        {
                            firstBuffer["uri"] = binFileName;

                            if (this.unpackImages)
                            {
                                foreach (var bufferView in bufferViews)
                                {
                                    if (bufferView != null && bufferView.GetInt("buffer") == 0)
                                    {
                                        fileStream.Align();
                                        var outputOffset = fileStream.Position;

                                        var byteOffset = bufferView.GetInt("byteOffset", 0);
                                        var byteLength = bufferView.GetInt("byteLength", 0);
                                        using (var viewStream = memoryMappedFile.CreateViewStream(binChunkOffset + byteOffset, byteLength, MemoryMappedFileAccess.Read))
                                        {
                                            viewStream.CopyTo(fileStream, byteLength);
                                        }

                                        bufferView["byteOffset"] = outputOffset;
                                    }
                                }

                                firstBuffer["byteLength"] = fileStream.Length;
                            }
                            else
                            {
                                var byteLength = firstBuffer.GetInt("byteLength", 0);
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
                        node["buffers"] = buffers;

                        foreach (var bufferView in bufferViews)
                        {
                            if (bufferView == null)
                            {
                                continue;
                            }

                            var bufferIndex = bufferView.GetInt("buffer", -1);
                            if (bufferIndex > 0)
                            {
                                bufferView["buffer"] = bufferIndex - 1;
                            }
                        }
                    }
                }
            }
        }
    }
}
