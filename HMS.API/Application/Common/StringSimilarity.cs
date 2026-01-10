using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HMS.API.Application.Common
{
    public static class StringSimilarity
    {
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var normalized = input.Trim().ToLowerInvariant();
            normalized = RemoveDiacritics(normalized);
            return normalized;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public static int LevenshteinDistance(string a, string b)
        {
            a = Normalize(a);
            b = Normalize(b);

            if (string.IsNullOrEmpty(a)) return b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var n = a.Length;
            var m = b.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        public static double Similarity(string a, string b)
        {
            var dist = LevenshteinDistance(a, b);
            var max = Math.Max(Normalize(a).Length, Normalize(b).Length);
            if (max == 0) return 1.0;
            return 1.0 - (double)dist / max;
        }
    }
}