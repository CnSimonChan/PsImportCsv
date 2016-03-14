using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace Microsoft.PowerShell.Commands
{
    internal class ImportCsvHelper
    {
        private const string UnspecifiedName = "H";

        private readonly char _delimiter;
        
        private readonly StreamReader _reader;

        private bool EOF
        {
            get
            {
                return _reader.EndOfStream;
            }
        }

        private IList<string> _header;

        internal ImportCsvHelper(char delimiter, IList<string> header, StreamReader streamReader)
        {
            _delimiter = delimiter;
            _header = header;
            _reader = streamReader;
        }

        private JObject BuildMshobject(IList<string> names, Collection<string> values, char delimiter)
        {
            var pSObject = new JObject();
            int num = 1;
            for (int i = 0; i <= names.Count - 1; i++)
            {
                string item = names[i];
                string str = null;
                if (item.Length != 0 || delimiter != '\"')
                {
                    if (string.IsNullOrEmpty(item))
                    {
                        item = string.Concat(UnspecifiedName, num);
                        num++;
                    }
                    if (i < values.Count)
                    {
                        str = values[i];
                    }
                    pSObject[item] = str;
                }
            }
            return pSObject;
        }

        internal IEnumerable<JObject> Import()
        {
            ReadHeader();
            while (true)
            {
                Collection<string> strs = ParseNextRecord(false);
                if (strs.Count == 0)
                {
                    break;
                }
                if (strs.Count != 1 || !string.IsNullOrEmpty(strs[0]))
                {
                    var pSObject = this.BuildMshobject(_header, strs, _delimiter);
                    yield return pSObject;
                }
            }
        }

        private bool IsNewLine(char ch)
        {
            bool flag = false;
            if (ch == '\n')
            {
                flag = true;
            }
            else if (ch == '\r' && PeekNextChar('\n'))
            {
                flag = true;
            }
            return flag;
        }

        private Collection<string> ParseNextRecord(bool isHeaderRow)
        {
            Collection<string> strs = new Collection<string>();
            StringBuilder stringBuilder = new StringBuilder();
            bool flag = false;
            while (!EOF)
            {
                char chr = ReadChar();
                if (chr == _delimiter)
                {
                    if (!flag)
                    {
                        strs.Add(stringBuilder.ToString());
                        stringBuilder.Remove(0, stringBuilder.Length);
                    }
                    else
                    {
                        stringBuilder.Append(chr);
                    }
                }
                else if (chr == '\"')
                {
                    if (flag)
                    {
                        if (!PeekNextChar('\"'))
                        {
                            flag = false;
                            bool flag1 = false;
                            ReadTillNextDelimiter(stringBuilder, ref flag1, true);
                            strs.Add(stringBuilder.ToString());
                            stringBuilder.Remove(0, stringBuilder.Length);
                            if (!flag1)
                            {
                                continue;
                            }
                            break;
                        }
                        else
                        {
                            ReadChar();
                            stringBuilder.Append('\"');
                        }
                    }
                    else if (stringBuilder.Length != 0)
                    {
                        bool flag2 = false;
                        stringBuilder.Append(chr);
                        ReadTillNextDelimiter(stringBuilder, ref flag2, false);
                        strs.Add(stringBuilder.ToString());
                        stringBuilder.Remove(0, stringBuilder.Length);
                        if (!flag2)
                        {
                            continue;
                        }
                        break;
                    }
                    else
                    {
                        flag = true;
                    }
                }
                else if (chr != ' ' && chr != '\t')
                {
                    if (!IsNewLine(chr))
                    {
                        stringBuilder.Append(chr);
                    }
                    else
                    {
                        if (chr == '\r')
                        {
                            ReadChar();
                        }
                        if (!flag)
                        {
                            strs.Add(stringBuilder.ToString());
                            stringBuilder.Remove(0, stringBuilder.Length);
                            break;
                        }
                        else
                        {
                            stringBuilder.Append(chr);
                            if (chr != '\r')
                            {
                                continue;
                            }
                            stringBuilder.Append('\n');
                        }
                    }
                }
                else if (!flag)
                {
                    if (stringBuilder.Length == 0)
                    {
                        continue;
                    }
                    bool flag3 = false;
                    stringBuilder.Append(chr);
                    ReadTillNextDelimiter(stringBuilder, ref flag3, true);
                    strs.Add(stringBuilder.ToString());
                    stringBuilder.Remove(0, stringBuilder.Length);
                    if (!flag3)
                    {
                        continue;
                    }
                    break;
                }
                else
                {
                    stringBuilder.Append(chr);
                }
            }
            if (stringBuilder.Length != 0)
            {
                strs.Add(stringBuilder.ToString());
            }
            if (isHeaderRow)
            {
                while (strs.Count > 1 && strs[strs.Count - 1].Equals(string.Empty))
                {
                    strs.RemoveAt(strs.Count - 1);
                }
            }
            return strs;
        }

        private bool PeekNextChar(char c)
        {
            int num = _reader.Peek();
            if (num == -1)
            {
                return false;
            }
            return c == (char)num;
        }

        private char ReadChar()
        {
            return (char)_reader.Read();
        }

        internal void ReadHeader()
        {
            if (!EOF)
            {
                ReadTypeInformation();
            }

            if (_header == null && !EOF)
            {
                Collection<string> strs = ParseNextRecord(true);
                if (strs.Count != 0)
                {
                    _header = strs;
                }
            }

            if (_header != null && _header.Count > 0)
            {
                ValidatePropertyNames(_header);
            }
        }

        private string ReadLine()
        {
            return _reader.ReadLine();
        }

        private void ReadTillNextDelimiter(StringBuilder current, ref bool endOfRecord, bool eatTrailingBlanks)
        {
            StringBuilder stringBuilder = new StringBuilder();
            bool flag = false;
            while (true)
            {
                if (!EOF)
                {
                    char chr = ReadChar();
                    if (chr == _delimiter)
                    {
                        break;
                    }
                    if (!IsNewLine(chr))
                    {
                        stringBuilder.Append(chr);
                        if (chr != ' ' && chr != '\t')
                        {
                            flag = true;
                        }
                    }
                    else
                    {
                        endOfRecord = true;
                        if (chr != '\r')
                        {
                            break;
                        }
                        ReadChar();
                        break;
                    }
                }
                else
                {
                    endOfRecord = true;
                    break;
                }
            }
            if (!eatTrailingBlanks || flag)
            {
                current.Append(stringBuilder);
                return;
            }
            current.Append(stringBuilder.ToString().Trim());
        }

        private string ReadTypeInformation()
        {
            string str = null;
            if (PeekNextChar('#'))
            {
                string str1 = ReadLine();
                if (str1.StartsWith("#Type", StringComparison.OrdinalIgnoreCase))
                {
                    str = str1.Substring(5);
                    str = str.Trim();
                    if (str.Length != 0)
                    {
                        str = string.Concat("CSV:", str);
                    }
                    else
                    {
                        str = null;
                    }
                }
            }
            return str;
        }

        private static void ValidatePropertyNames(IList<string> names)
        {
            if (names != null)
            {
                if (names.Count == 0)
                    return;

                HashSet<string> strs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string name in names)
                {
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (strs.Contains(name))
                        throw new FormatException("Duplicate property names.");

                    strs.Add(name);
                }
            }
        }
    }
}