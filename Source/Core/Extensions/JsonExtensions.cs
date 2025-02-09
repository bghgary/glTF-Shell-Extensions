using System.Text.Json;
using System.Text.Json.Nodes;

namespace glTF
{
    internal static class JsonExtensions
    {
        public static JsonArray? GetArray(this JsonNode node, string propertyName, JsonArray? defaultValue = null)
        {
            var propertyNode = node[propertyName];
            if (propertyNode == null || propertyNode.GetValueKind() != JsonValueKind.Array)
            {
                return defaultValue;
            }

            return propertyNode.AsArray();
        }

        public static int GetInt(this JsonNode node, string propertyName, int defaultValue = -1)
        {
            var propertyNode = node[propertyName];
            if (propertyNode == null || propertyNode.GetValueKind() != JsonValueKind.Number)
            {
                return defaultValue;
            }

            return propertyNode.GetValue<int>();
        }

        public static string? GetString(this JsonNode node, string propertyName, string? defaultValue = null)
        {
            var propertyNode = node[propertyName];
            if (propertyNode == null || propertyNode.GetValueKind() != JsonValueKind.String)
            {
                return defaultValue;
            }

            return propertyNode.GetValue<string>();
        }

        public static string? GetLocalPath(this JsonNode node, string propertyName, Uri baseUri, string? defaultValue = null)
        {
            var uriString = node.GetString(propertyName);
            if (uriString == null)
            {
                return defaultValue;
            }

            if (!Uri.TryCreate(baseUri, uriString, out var uri) || !uri.IsFile)
            {
                return defaultValue;
            }

            return uri.LocalPath;
        }

        public static void SetInt(this JsonNode jsonNode, string propertyName, int value, int defaultValue)
        {
            if (value == defaultValue)
            {
                jsonNode.AsObject().Remove(propertyName);
            }
            else
            {
                jsonNode[propertyName] = JsonValue.Create(value);
            }
        }

        public static bool Remove(this JsonNode node, string propertyName)
        {
            if (node.GetValueKind() != JsonValueKind.Object)
            {
                return false;
            }

            return node.AsObject().Remove(propertyName);
        }
    }
}
