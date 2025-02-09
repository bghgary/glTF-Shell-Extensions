using System.IO.MemoryMappedFiles;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace glTF
{
    public class Packer
    {
        private struct FileInfo
        {
            public MemoryMappedFile File;
            public int FileLength;
            public MemoryMappedViewStream Stream;
            public int ByteOffset;
        }

        public static void Pack(string inputFilePath, string outputFilePath)
        {
            var baseUri = new Uri(inputFilePath);

            var fileMap = new Dictionary<string, FileInfo>();
            var byteOffset = 0;

            FileInfo AddFile(string filePath)
            {
                if (!fileMap.TryGetValue(filePath, out var fileInfo))
                {
                    var file = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open);
                    var fileLength = Tools.GetFileLength(filePath);
                    var stream = file.CreateViewStream(0, fileLength, MemoryMappedFileAccess.Read);
                    fileInfo = new() { File = file, FileLength = fileLength, Stream = stream, ByteOffset = byteOffset };
                    fileMap.Add(filePath, fileInfo);
                    byteOffset = Tools.Align(byteOffset + fileLength);
                }

                return fileInfo;
            }

            JsonNode root;
            using (var stream = File.OpenRead(inputFilePath))
            {
                root = JsonNode.Parse(stream)!;
            }

            var buffers = root.GetArray("buffers");
            var bufferViews = root.GetArray("bufferViews");

            if (buffers != null)
            {
                for (var index = buffers.Count - 1; index >= 0; index--)
                {
                    var buffer = buffers[index];
                    if (buffer == null)
                    {
                        continue;
                    }

                    var filePath = buffer.GetLocalPath("uri", baseUri);
                    if (filePath == null)
                    {
                        continue;
                    }

                    var fileInfo = AddFile(filePath);

                    if (bufferViews != null)
                    {
                        foreach (var bufferView in bufferViews)
                        {
                            if (bufferView == null)
                            {
                                continue;
                            }

                            var bufferIndex = bufferView.GetInt("buffer");
                            if (bufferIndex == -1)
                            {
                                continue;
                            }

                            if (bufferIndex == index)
                            {
                                bufferView.Remove("buffer");
                                bufferView.SetInt("byteOffset", fileInfo.ByteOffset + bufferView.GetInt("byteOffset", 0), 0);
                            }
                            else if (bufferIndex > index)
                            {
                                bufferView["buffer"] = bufferIndex - 1;
                            }
                        }
                    }

                    buffers.RemoveAt(index);
                }
            }

            void ProcessArray(JsonArray array)
            {
                foreach (var element in array)
                {
                    if (element == null)
                    {
                        continue;
                    }

                    var filePath = element.GetLocalPath("uri", baseUri);
                    if (filePath == null)
                    {
                        continue;
                    }

                    var fileInfo = AddFile(filePath);

                    if (bufferViews == null)
                    {
                        bufferViews = [];
                        root["bufferViews"] = bufferViews;
                    }

                    element.Remove("uri");
                    element["bufferView"] = bufferViews.Count;
                    element["mimeType"] = MimeType.FromFileExtension(Path.GetExtension(filePath));

                    JsonNode bufferView = new JsonObject();
                    bufferView.SetInt("byteOffset", fileInfo.ByteOffset, 0);
                    bufferView["byteLength"] = fileInfo.FileLength;
                    bufferViews.Add(bufferView);
                }
            }

            var images = root.GetArray("images");
            if (images != null)
            {
                ProcessArray(images);
            }

            // EXT_lights_ies
            var lights = root["extensions"]?.AsObject()["EXT_lights_ies"]?.AsObject().GetArray("lights");
            if (lights != null)
            {
                ProcessArray(lights);
            }

            if (fileMap.Count != 0)
            {
                if (buffers == null)
                {
                    buffers = [];
                    root["buffers"] = buffers;
                }

                JsonNode buffer = new JsonObject();
                buffer["byteLength"] = byteOffset;
                buffers.Insert(0, buffer);

                if (bufferViews != null)
                {
                    foreach (var bufferView in bufferViews)
                    {
                        if (bufferView == null)
                        {
                            continue;
                        }

                        var bufferIndex = bufferView.GetInt("buffer");
                        bufferView["buffer"] = bufferIndex + 1;
                    }
                }
            }

            var outputDirectoryPath = Path.GetDirectoryName(outputFilePath);
            if (outputDirectoryPath != null)
            {
                Directory.CreateDirectory(outputDirectoryPath);
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
                    root.WriteTo(jsonTextWriter);
                }

                binaryWriter.BaseStream.Align(0x20);
                var jsonChunkLength = checked((uint)(binaryWriter.BaseStream.Length - jsonChunkPosition)) - Binary.ChunkHeaderLength;

                binaryWriter.BaseStream.Seek(jsonChunkPosition, SeekOrigin.Begin);
                binaryWriter.Write(jsonChunkLength);

                if (fileMap.Count != 0)
                {
                    binaryWriter.BaseStream.Seek(0, SeekOrigin.End);
                    var binChunkPosition = binaryWriter.BaseStream.Position;

                    binaryWriter.Write(0); // bin chunk length
                    binaryWriter.Write(Binary.ChunkFormatBin);

                    foreach (var value in fileMap.Values)
                    {
                        binaryWriter.BaseStream.Align();
                        value.Stream.CopyTo(binaryWriter.BaseStream);
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

            foreach (var value in fileMap.Values)
            {
                value.Stream.Dispose();
                value.File.Dispose();
            }
        }
    }
}