using Microsoft.PowerShell.Commands;
using System.Collections.Generic;
using System.IO;

namespace PsImportCsv
{
    public static class Csv
    {
        public static List<T> Parse<T>(string input, IList<string> header = null)
        {
            using (var stream = new MemoryStream())
            {
                var writer = new StreamWriter(stream);
                writer.WriteAsync(input);
                writer.Flush();

                stream.Position = 0;
                using (var reader = new StreamReader(stream))
                {
                    var helper = new ImportCsvHelper(',', header, reader);
                    var result = new List<T>();
                    foreach (var item in helper.Import())
                        result.Add(item.ToObject<T>());
                    return result;
                }
            }
        }

        public static List<T> ParseFile<T>(string path, IList<string> header = null)
        {
            using (var reader = new StreamReader(path))
            {
                var helper = new ImportCsvHelper(',', header, reader);
                var result = new List<T>();
                foreach (var item in helper.Import())
                    result.Add(item.ToObject<T>());
                return result;
            }
        }

        public static List<T> ParseStream<T>(Stream stream, IList<string> header = null, bool leaveOpen = false)
        {
            try
            {
                var reader = new StreamReader(stream);

                var helper = new ImportCsvHelper(',', header, reader);
                var result = new List<T>();
                foreach (var item in helper.Import())
                    result.Add(item.ToObject<T>());
                return result;
            }
            finally
            {
                if (!leaveOpen)
                    stream.Dispose();
            }
        }
    }
}
