using System;
using System.Text;
using System.Web.Script.Serialization;

namespace RAMGuardian.Common.Serialization
{
    public static class JsonHelper
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static string Serialize<T>(T obj, bool prettyPrint = true)
        {
            if (obj == null) return "{}";
            string raw = Serializer.Serialize(obj);
            return prettyPrint ? FormatJson(raw) : raw;
        }

        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default(T);
            return Serializer.Deserialize<T>(json);
        }

        public static string FormatJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;

            var sb = new StringBuilder();
            bool inQuotes = false;
            bool isEscaped = false;
            int indent = 0;
            const string indentString = "  ";

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];

                if (isEscaped)
                {
                    sb.Append(ch);
                    isEscaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    sb.Append(ch);
                    isEscaped = true;
                    continue;
                }

                if (ch == '\"')
                {
                    inQuotes = !inQuotes;
                    sb.Append(ch);
                    continue;
                }

                if (!inQuotes)
                {
                    switch (ch)
                    {
                        case '{':
                        case '[':
                            sb.Append(ch);
                            sb.AppendLine();
                            indent++;
                            AppendIndent(sb, indent, indentString);
                            break;
                        case '}':
                        case ']':
                            sb.AppendLine();
                            indent = Math.Max(0, indent - 1);
                            AppendIndent(sb, indent, indentString);
                            sb.Append(ch);
                            break;
                        case ',':
                            sb.Append(ch);
                            sb.AppendLine();
                            AppendIndent(sb, indent, indentString);
                            break;
                        case ':':
                            sb.Append(": ");
                            break;
                        default:
                            if (!char.IsWhiteSpace(ch))
                            {
                                sb.Append(ch);
                            }
                            break;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static void AppendIndent(StringBuilder sb, int count, string indentString)
        {
            for (int i = 0; i < count; i++)
            {
                sb.Append(indentString);
            }
        }
    }
}
