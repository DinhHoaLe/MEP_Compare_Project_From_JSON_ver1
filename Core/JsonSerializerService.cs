using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
namespace BimComparison.Core
{
    public static class JsonSerializerService
    {
        private static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.None, Culture = System.Globalization.CultureInfo.InvariantCulture,
            Formatting = Formatting.Indented, MaxDepth = 64 });
        public static T Read<T>(string path) where T : class
        {
            using (var stream = File.OpenText(path))
            using (var reader = new JsonTextReader(stream))
                return Serializer().Deserialize<T>(reader) ?? throw new InvalidDataException("JSON is empty.");
        }
        public static void Write<T>(string path, T value)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new StreamWriter(temporary, false, new UTF8Encoding(false)))
                using (var writer = new JsonTextWriter(stream)) Serializer().Serialize(writer, value);
                if (File.Exists(path)) File.Replace(temporary, path, null, true); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
