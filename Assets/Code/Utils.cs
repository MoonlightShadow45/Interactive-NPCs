using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Code.Characters;
using Code.Characters.Memories;
using UnityEngine;

namespace Code
{
    public static class Utils
    {
        public static float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
            {
                throw new System.ArgumentException("Vectors must be of the same length.");
            }

            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
            {
                return 0f; // Avoid division by zero
            }

            return dotProduct / (Mathf.Sqrt(magnitudeA) * Mathf.Sqrt(magnitudeB));
        }
        
        public static void SerializeIntoJson<T>(T obj, string fileName)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Vector2IntDictionaryConverterFactory());
            options.WriteIndented = true;
            options.IncludeFields = true;
            options.PropertyNameCaseInsensitive = true;
            
            string jsonString = JsonSerializer.Serialize(obj, options);
            
            using (FileStream stream = new FileStream(Application.persistentDataPath + $"/{fileName}", FileMode.Create))
            {
                using(StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(jsonString);
                }
            }
        }
        
        public class Vector2IntDictionaryConverter<TValue> : JsonConverter<Dictionary<Vector2Int, TValue>>
        {
            private JsonConverter<Dictionary<Vector2Int, TValue>> _jsonConverterImplementation;

            public override Dictionary<Vector2Int, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var result = new Dictionary<Vector2Int, TValue>();
                using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var vec = ParseKey(prop.Name); // "(x,y)" => Vector2Int
                        var value = JsonSerializer.Deserialize<TValue>(prop.Value.GetRawText(), options);
                        result[vec] = value;
                    }
                }
                return result;
            }

            public override void Write(Utf8JsonWriter writer, Dictionary<Vector2Int, TValue> value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                foreach (var kv in value)
                {
                    var key = $"{kv.Key.x},{kv.Key.y}";
                    writer.WritePropertyName(key);
                    JsonSerializer.Serialize(writer, kv.Value, options);
                }
                writer.WriteEndObject();
            }

            private Vector2Int ParseKey(string key)
            {
                var parts = key.Split(',');
                return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
            }
        }
        
        public class Vector2IntDictionaryConverterFactory : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                if (!typeToConvert.IsGenericType) return false;

                var genericDef = typeToConvert.GetGenericTypeDefinition();
                if (genericDef != typeof(Dictionary<,>)) return false;

                var keyType = typeToConvert.GetGenericArguments()[0];
                return keyType == typeof(Vector2Int);
            }

            public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
            {
                var valueType = type.GetGenericArguments()[1];
                var converterType = typeof(Vector2IntDictionaryConverter<>).MakeGenericType(valueType);
                return (JsonConverter)Activator.CreateInstance(converterType);
            }
        }
        
        public static bool IsAdjacent(Vector2Int pos1, Vector2Int pos2)
        {
            return (Mathf.Abs(pos1.x - pos2.x) <= 1 && pos1.y == pos2.y) ||
                   (Mathf.Abs(pos1.y - pos2.y) <= 1 && pos1.x == pos2.x);
        }
        
        // Normalizes a list of floats to the range [0, 1]
        public static List<float> Normalize(List<float> list)
        {
            if (list.Count == 0)
            {
                return new List<float>();
            }

            float max = float.MinValue;
            float min = float.MaxValue;

            foreach (var value in list)
            {
                if (value > max)
                {
                    max = value;
                }

                if (value < min)
                {
                    min = value;
                }
            }

            List<float> normalizedList = new List<float>();
            if (Mathf.Approximately(max, min))
            {
                // All values are the same, return a list of 0.5f
                for (int i = 0; i < list.Count; i++)
                {
                    normalizedList.Add(0.5f);
                }
                return normalizedList;
            }
            
            // Normalize the values
            foreach (var value in list)
            {
                normalizedList.Add((value - min) / (max - min));
            }

            return normalizedList;
        }

        public static string GetSummaryStatements(List<ConceptNode> nodes)
        {
            var nodeDescriptions = new StringBuilder();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                string nodeTypeString = node.Type switch
                {
                    NodeType.Event => "Event",
                    NodeType.Thought => "Thought",
                    _ => "Event"
                };
                string timeString = node.CreationTime.ToRealTime();
                nodeDescriptions.AppendLine($"{i}. {nodeTypeString} at {timeString}: {node.Description}");
            }

            return nodeDescriptions.ToString();
        }
        
        public static string ChatEntriesToString(List<ChatEntry> charEntries)
        {
            var sb = new StringBuilder();
            foreach (var entry in charEntries)
            {
                sb.AppendLine($"{entry.Speaker}: {entry.Content}");
            }
            return sb.ToString();
        }

        public static string InventoryToString(List<Item> items)
        {
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine($"{item.Name}  {item.Quantity}");
            }
            return sb.ToString();
        }
    }
}