namespace glTF
{
    internal class MimeType
    {
        public static string ToFileExtension(string? mimeType)
        {
            switch (mimeType)
            {
                case "image/png":
                    return ".png";
                case "image/jpeg":
                    return ".jpg";
                case "image/vnd-ms.dds":
                    return ".dds";
                case "image/ktx2":
                    return ".ktx2";
                case "image/webp":
                    return ".webp";
            }

            return ".bin";
        }

        public static string FromFileExtension(string? fileExtension)
        {
            if (fileExtension != null)
            {
                switch (fileExtension.ToLower())
                {
                    case ".png":
                        return "image/png";
                    case ".jpg":
                    case ".jpeg":
                        return "image/jpeg";
                    case ".dds":
                        return "image/vnd-ms.dds";
                    case ".ktx2":
                        return "image/ktx2";
                    case ".webp":
                        return "image/webp";
                }
            }

            return "application/octet-stream";
        }
    }
}
