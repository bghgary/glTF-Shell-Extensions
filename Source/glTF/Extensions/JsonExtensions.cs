using System.Text.Json.Nodes;

namespace glTF
{
    internal static class JsonExtensions
    {
        public static void SetValue(this JsonNode jsonNode, string propertyName, int value, int defaultValue)
        {
            if (value.Equals(defaultValue))
            {
                jsonNode.AsObject().Remove(propertyName);
            }
            else
            {
                jsonNode[propertyName] = value;
            }
        }
    }
}
