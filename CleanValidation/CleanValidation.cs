using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CleanValidation
{
    /// <summary>
    /// General-purpose string/number/date cleaning, validation, and small formatting helpers.
    /// Every method is null-tolerant: bad or missing input produces a sensible default
    /// (empty string, zero, <see cref="DateTime.MinValue"/>) instead of throwing, which is the
    /// point of a "clean" layer sitting between raw user/external input and your business logic.
    /// See README.md for what was intentionally left out (and why) versus what was kept.
    /// </summary>
    public static class CleanValidation
    {
        /// <summary>Default rounding precision for <see cref="Decimal"/> - 2 places, as used for currency.</summary>
        public const int DefaultCurrencyPrecision = 2;

        /// <summary>A commonly used precision for exchange-rate style decimals - 5 places.</summary>
        public const int DefaultExchangeRatePrecision = 5;

        /// <summary>Length of a Bluetooth MAC address with separators removed (12 hex characters).</summary>
        public const int BluetoothAddressLength = 12;

        /// <summary>Length of a UPC-A barcode (12 digits).</summary>
        public const int UpcALength = 12;

        #region Internal helpers

        private static string CleanAndTrim(object? input)
        {
            if (input == null) return string.Empty;
            if (input is string s) return s.Trim();
            return input.ToString()?.Trim() ?? string.Empty;
        }

        private static string SafeSubstring(string input, int start, int length)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (start < 0 || start >= input.Length) return string.Empty;
            var maxLength = Math.Min(length, input.Length - start);
            return input.Substring(start, maxLength);
        }

        private static string ExtractDigitsAndSymbols(string input, bool allowDecimal = true, bool allowNegative = true)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (char.IsDigit(c) || (allowDecimal && c == '.') || (allowNegative && c == '-')) sb.Append(c);
            }

            return sb.ToString();
        }

        private static decimal ParseDecimalOrZero(string input) => decimal.TryParse(input, out var result) ? result : 0m;

        private static float ParseFloatOrZero(string input) => float.TryParse(input, out var result) ? result : 0f;

        #endregion

        // ============================================================
        // TEXT
        // ============================================================

        /// <summary>Converts to string and trims; null becomes empty string.</summary>
        public static string Text(object? input) => CleanAndTrim(input);

        /// <summary>Replaces common non-breaking-space representations ("&amp;nbsp;", "&amp;#160;", U+00A0) with a regular space.</summary>
        public static string Nbsp(object? input) =>
            Text(input).Replace("&nbsp;", " ").Replace("&#160;", " ").Replace((char)160, ' ');

        /// <summary>Trims text to at most <paramref name="maxLength"/> characters.</summary>
        public static string TrimLongString(string? input, int maxLength)
        {
            var cleaned = Text(input);
            return cleaned.Length <= maxLength ? cleaned : Text(SafeSubstring(cleaned, 0, maxLength));
        }

        /// <summary>Strips everything except digits, '.', and '-'. Returns "0" for empty input.</summary>
        public static string Number(string? input)
        {
            var cleaned = Text(input);
            return string.IsNullOrEmpty(cleaned) ? "0" : ExtractDigitsAndSymbols(cleaned);
        }

        /// <summary>Inserts an HTML line break before each newline, for displaying plain text as HTML.</summary>
        public static string HtmlLineBreaks(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace(Environment.NewLine, "<br />" + Environment.NewLine);
        }

        /// <summary>
        /// Case-insensitive string replace. .NET Framework 4.8 has no
        /// <c>string.Replace(string, string, StringComparison)</c> overload (added in .NET Core), so this
        /// library provides its own for callers targeting net48; on net6+ prefer the built-in overload.
        /// </summary>
        public static string ReplaceIgnoreCase(string original, string pattern, string replacement)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(pattern)) return original;

            var count = 0;
            var position0 = 0;

            var upperString = original.ToUpperInvariant();
            var upperPattern = pattern.ToUpperInvariant();

            var inc = (original.Length / pattern.Length) * (replacement.Length - pattern.Length);
            var chars = new char[original.Length + Math.Max(0, inc)];

            int position1;
            while ((position1 = upperString.IndexOf(upperPattern, position0, StringComparison.Ordinal)) != -1)
            {
                for (var i = position0; i < position1; ++i) chars[count++] = original[i];
                for (var i = 0; i < replacement.Length; ++i) chars[count++] = replacement[i];
                position0 = position1 + pattern.Length;
            }

            if (position0 == 0) return original;

            for (var i = position0; i < original.Length; ++i) chars[count++] = original[i];
            return new string(chars, 0, count);
        }

        // ============================================================
        // NUMBERS / DATES / BOOLEANS
        // ============================================================

        public static int Int32(object? input)
        {
            if (input is int v) return v;
            if (input is Enum e) return Convert.ToInt32(e);

            var cleaned = Number(input?.ToString());
            return string.IsNullOrEmpty(cleaned) ? 0 : (int)Math.Round(ParseDecimalOrZero(cleaned), 0, MidpointRounding.AwayFromZero);
        }

        public static long Int64(object? input)
        {
            if (input is long v) return v;

            var cleaned = Number(input?.ToString());
            return string.IsNullOrEmpty(cleaned) ? 0L : (long)Math.Round(ParseDecimalOrZero(cleaned), 0, MidpointRounding.AwayFromZero);
        }

        public static bool Bit(object? input)
        {
            if (input is bool b) return b;

            var cleaned = Text(input);
            return !string.IsNullOrEmpty(cleaned) &&
                   (cleaned.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || cleaned == "1");
        }

        public static DateTime Date(object? input)
        {
            if (input is DateTime dt) return dt;

            var cleaned = Text(input);
            if (string.IsNullOrEmpty(cleaned)) return DateTime.MinValue;

            DateTime.TryParse(cleaned, out var result);
            return result;
        }

        /// <summary>Formats a date (or a parseable date string) as a locale-short date string.</summary>
        public static string ShortDateString(object? input)
        {
            if (input is DateTime dateTime) return dateTime.ToLocalTime().ToShortDateString();

            var cleaned = Text(input);
            return string.IsNullOrEmpty(cleaned)
                ? DateTime.MinValue.ToShortDateString()
                : Date(cleaned).ToLocalTime().ToShortDateString();
        }

        /// <summary>Parses a decimal and rounds it to <paramref name="precision"/> places (away from zero).</summary>
        public static decimal Decimal(object? input, int precision = DefaultCurrencyPrecision)
        {
            if (input is decimal d) return Math.Round(d, precision, MidpointRounding.AwayFromZero);

            var cleaned = Number(input?.ToString());
            if (string.IsNullOrEmpty(cleaned)) return 0m;

            return Math.Round(ParseDecimalOrZero(cleaned), precision, MidpointRounding.AwayFromZero);
        }

        public static float Float(object? input)
        {
            if (input is float f) return f;

            var cleaned = Number(input?.ToString());
            return string.IsNullOrEmpty(cleaned) ? 0f : ParseFloatOrZero(cleaned);
        }

        public static string BoolToString(object? input) => Bit(input) ? "True" : "False";

        // ============================================================
        // HARDWARE / RETAIL IDENTIFIERS
        // ============================================================

        /// <summary>Strips separators, keeps only letters/digits, uppercases. Does not validate length - see <see cref="ValidateBluetoothAddress"/>.</summary>
        public static string BluetoothAddress(string? input)
        {
            var cleaned = Text(input);
            if (string.IsNullOrEmpty(cleaned)) return string.Empty;

            var sb = new StringBuilder(cleaned.Length);
            foreach (var c in cleaned)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }

            return Text(sb.ToString().ToUpperInvariant());
        }

        /// <summary>True if the cleaned address is exactly <see cref="BluetoothAddressLength"/> characters.</summary>
        public static bool ValidateBluetoothAddress(string input) =>
            !string.IsNullOrEmpty(input) && BluetoothAddress(input).Length == BluetoothAddressLength;

        /// <summary>Strips non-digits and truncates to <see cref="UpcALength"/> (UPC-A) digits.</summary>
        public static string Upc(object? input)
        {
            var cleaned = ExtractDigitsAndSymbols(Text(input), allowDecimal: false, allowNegative: false);
            if (string.IsNullOrEmpty(cleaned)) return string.Empty;
            return cleaned.Length > UpcALength ? SafeSubstring(cleaned, 0, UpcALength) : cleaned;
        }

        // ============================================================
        // VALIDATION
        // ============================================================

        public static bool ValidateEmail(string email)
        {
            var cleaned = Text(email);
            const string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return !string.IsNullOrEmpty(cleaned) && Regex.IsMatch(cleaned, pattern, RegexOptions.IgnoreCase);
        }

        // ============================================================
        // URL
        // ============================================================

        /// <summary>
        /// Canonicalizes a URL for display/dedup: strips the scheme, strips a leading "www."-style
        /// prefix from the host, and trims trailing slashes. If <paramref name="preserveAnnotationSuffix"/>
        /// is supplied and present in the input (e.g. your app's own convention for flagging unconfirmed
        /// URLs, such as "(unverified)"), it is stripped before normalizing and re-appended at the end -
        /// generalizes the common pattern of a free-text annotation riding along with a URL value.
        /// </summary>
        public static string NormalizeUrl(object? input, string? preserveAnnotationSuffix = null)
        {
            var tempUrl = Text(Text(input).Replace("'", string.Empty)).ToLowerInvariant();
            if (string.IsNullOrEmpty(tempUrl)) return string.Empty;

            string? annotation = null;
            if (!string.IsNullOrEmpty(preserveAnnotationSuffix))
            {
                var needle = preserveAnnotationSuffix!.ToLowerInvariant();
                if (tempUrl.Contains(needle))
                {
                    annotation = preserveAnnotationSuffix;
                    tempUrl = tempUrl.Replace($"({needle.Trim('(', ')')})", string.Empty).Replace(needle, string.Empty);
                    tempUrl = Text(tempUrl);
                }
            }

            var result = string.Empty;

            try
            {
                var uri = new Uri(tempUrl);
                var host = Text(uri.Host);

                foreach (var prefix in new[] { "wwww.", "www.", "ww.", "w." })
                {
                    if (host.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        host = host.Substring(prefix.Length);
                        break;
                    }
                }

                result = (host + uri.PathAndQuery + uri.Fragment).Trim('/');
            }
            catch (UriFormatException)
            {
                // Not a well-formed URL; fall through and return whatever cleanup happened above.
                result = tempUrl.Trim('/');
            }

            if (annotation != null) result += " " + annotation;
            return result.Trim();
        }

        // ============================================================
        // FILE NAMES / EXCEL
        // ============================================================

        /// <summary>Builds a timestamped file-safe name: "{prefix}{yyyyMMddHHmmss}".</summary>
        public static string FileName(string prefix = "Export") => $"{prefix}{DateTime.Now:yyyyMMddHHmmss}";

        /// <summary>
        /// Sanitizes a string for use as an Excel worksheet name: strips characters Excel forbids
        /// (<c>: \ / ? * [ ]</c>), trims surrounding quotes/whitespace, and truncates to Excel's 31-character limit.
        /// </summary>
        public static string ExcelSheetName(string input)
        {
            const string defaultName = "Sheet1";

            var cleaned = Text(input);
            if (string.IsNullOrWhiteSpace(cleaned)) return defaultName;

            cleaned = Regex.Replace(cleaned, @"[:\\/\?\*\[\]]", " ");
            cleaned = cleaned.Trim().Trim('\'');

            if (string.IsNullOrWhiteSpace(cleaned)) return defaultName;
            return cleaned.Length > 31 ? cleaned.Substring(0, 31) : cleaned;
        }

        public static string StripExtension(string input, string extension)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var ext = extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
            return Text(Regex.Replace(input.Trim(), Regex.Escape(ext), string.Empty, RegexOptions.IgnoreCase));
        }

        public static string EnsureExtension(string input, string extension)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var ext = extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
            return Text(input).EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? Text(input) : Text(input) + ext;
        }

        // ============================================================
        // JSON / ENUMS
        // ============================================================

        public static string PrettyJson(string uglyJson)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var element = JsonSerializer.Deserialize<JsonElement?>(uglyJson);
            return element == null ? string.Empty : JsonSerializer.Serialize(element.Value, options);
        }

        public static string EnumName(object? input)
        {
            try
            {
                return input == null ? string.Empty : Enum.GetName(input.GetType(), input) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // ============================================================
        // FORMATTING / HASHING / KEYS
        // ============================================================

        public static string FormatCurrency(decimal amount, string currencySymbol, bool wholeNumberOnly = false)
        {
            var format = wholeNumberOnly ? "{0:N0}" : "{0:N}";
            return currencySymbol + string.Format(format, amount);
        }

        /// <summary>
        /// MD5 hex digest of the input. MD5 is cryptographically broken - use this only for
        /// non-security purposes (cache keys, checksums, dedup fingerprints), never for passwords,
        /// tokens, or anything where collision/preimage resistance matters.
        /// </summary>
        public static string GetMd5Hash(string input)
        {
            var cleaned = Text(input);
            if (string.IsNullOrEmpty(cleaned)) return string.Empty;

            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(cleaned));

            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2").ToUpperInvariant());
            return sb.ToString();
        }

        /// <summary>Cryptographically random alphanumeric key (uppercase letters + digits).</summary>
        public static string RandomKey(int length)
        {
            const string allowed = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var keyLength = Math.Max(0, length);
            var bytes = new byte[keyLength];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);

            var sb = new StringBuilder(keyLength);
            for (var i = 0; i < keyLength; i++) sb.Append(allowed[bytes[i] % allowed.Length]);
            return sb.ToString();
        }

        // ============================================================
        // TOKEN LISTS
        // ============================================================

        /// <summary>
        /// Splits free text on common delimiters (space, comma, period, colon, tab, CR, LF),
        /// removes empty entries, and de-duplicates. Handy for parsing a pasted list of IDs/codes
        /// from a textbox. Set <paramref name="sortDescending"/> for a simple descending sort;
        /// for a domain-specific sort key see <see cref="SortByStructuredCodeSegmentDescending"/>.
        /// </summary>
        public static List<string> ParseTokenList(string input, bool sortDescending = false)
        {
            var delimiters = new[] { ' ', ',', '.', ':', '\t', '\r', '\n' };
            var list = (input ?? string.Empty).Trim()
                .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();

            return sortDescending ? list.OrderByDescending(c => c, StringComparer.Ordinal).ToList() : list;
        }

        public static List<string> ParseCommaSeparatedList(string input)
        {
            return (input ?? string.Empty).Trim()
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Text)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();
        }

        public static string JoinTokenList(IEnumerable<string> tokens) => string.Join(",", tokens);

        public static List<string> SplitCommaSeparatedList(string input) => (input ?? string.Empty).Split(',').ToList();

        /// <summary>
        /// Extracts a fixed-position segment from a fixed-length structured code (serial number, SKU,
        /// lot code, etc.), optionally inserting a dash at a given offset within the extracted segment -
        /// useful as a custom sort key. Returns empty string if <paramref name="code"/> is shorter than
        /// <paramref name="expectedLength"/>.
        /// </summary>
        public static string ExtractStructuredCodeSegment(string code, int expectedLength, int segmentStart, int segmentLength, int? insertDashAt = null)
        {
            if (string.IsNullOrEmpty(code) || code.Length < expectedLength) return string.Empty;

            var segment = SafeSubstring(code, segmentStart, segmentLength);
            if (insertDashAt.HasValue && insertDashAt.Value >= 0 && insertDashAt.Value <= segment.Length)
                segment = segment.Insert(insertDashAt.Value, "-");

            return segment;
        }

        /// <summary>
        /// Sorts a list of fixed-length structured codes descending by a segment extracted via
        /// <see cref="ExtractStructuredCodeSegment"/>. Falls back to a plain ordinal descending sort
        /// if the first code's length doesn't match <paramref name="expectedLength"/>.
        /// </summary>
        public static List<string> SortByStructuredCodeSegmentDescending(
            List<string> codes, int expectedLength, int segmentStart, int segmentLength, int? insertDashAt = null)
        {
            if (codes.Count == 0) return codes;
            if (codes[0].Length != expectedLength) return codes.OrderByDescending(c => c, StringComparer.Ordinal).ToList();

            codes.Sort((a, b) => string.Compare(
                ExtractStructuredCodeSegment(b, expectedLength, segmentStart, segmentLength, insertDashAt),
                ExtractStructuredCodeSegment(a, expectedLength, segmentStart, segmentLength, insertDashAt),
                StringComparison.Ordinal));

            return codes;
        }

        // ============================================================
        // REFLECTION
        // ============================================================

        /// <summary>
        /// Recursively flattens an object graph into a flat list of leaf property name/value pairs -
        /// useful for logging or exporting an arbitrary object without writing per-type code.
        /// Value types and strings are emitted directly; nested classes are recursed into;
        /// enumerables have each element recursed into.
        /// </summary>
        public static List<PropertyValue> FlattenObjectProperties(object? obj)
        {
            var list = new List<PropertyValue>();
            if (obj == null) return list;

            foreach (var prop in obj.GetType().GetProperties())
            {
                if (prop.PropertyType.IsPrimitive || prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
                {
                    list.Add(new PropertyValue { Name = prop.Name, Value = prop.GetValue(obj) });
                    continue;
                }

                if (prop.PropertyType.IsClass && !typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
                {
                    var val = prop.GetValue(obj);
                    if (val != null) list.AddRange(FlattenObjectProperties(val));
                    continue;
                }

                if (prop.GetValue(obj) is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) list.AddRange(FlattenObjectProperties(item));
                }
            }

            return list;
        }
    }
}
