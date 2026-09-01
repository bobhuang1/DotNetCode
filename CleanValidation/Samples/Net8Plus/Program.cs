using CV = CleanValidation.CleanValidation;

// Null/garbage-tolerant conversions instead of throwing.
Console.WriteLine($"Int32(\"  42px \") = {CV.Int32("  42px ")}");
Console.WriteLine($"Decimal(\"$1,234.567\") = {CV.Decimal("$1,234.567")}");
Console.WriteLine($"Bit(\"yes\")  = {CV.Bit("yes")}");
Console.WriteLine($"Bit(null)   = {CV.Bit(null)}");

Console.WriteLine($"ValidateEmail(\"a@b.com\") = {CV.ValidateEmail("a@b.com")}");
Console.WriteLine($"NormalizeUrl(\"HTTPS://WWW.Example.com/Path/?x=1\") = {CV.NormalizeUrl("HTTPS://WWW.Example.com/Path/?x=1")}");

Console.WriteLine($"ExcelSheetName(\"Q1:Sales/Report*\") = {CV.ExcelSheetName("Q1:Sales/Report*")}");

var tokens = CV.ParseTokenList("ABC123, abc123 DEF456\tGHI789", sortDescending: true);
Console.WriteLine($"ParseTokenList(...) = [{string.Join(", ", tokens)}]");

Console.WriteLine($"FormatCurrency(1234.5m, \"$\") = {CV.FormatCurrency(1234.5m, "$")}");
Console.WriteLine($"GetMd5Hash(\"hello\") = {CV.GetMd5Hash("hello")}");
Console.WriteLine($"RandomKey(10) = {CV.RandomKey(10)}");

// Structured-code segment extraction/sort - a generic replacement for any fixed-width code
// convention (serial numbers, SKUs, lot codes): here a 10-char code "AAAA-99999" sorted by the
// 5-digit segment starting at index 5.
List<string> codes = ["AAAA-00003", "AAAA-00001", "AAAA-00002"];
var sorted = CV.SortByStructuredCodeSegmentDescending(codes, expectedLength: 10, segmentStart: 5, segmentLength: 5);
Console.WriteLine($"SortByStructuredCodeSegmentDescending(...) = [{string.Join(", ", sorted)}]");

// On net6+, prefer the built-in string.Replace(string, string, StringComparison) overload -
// ReplaceIgnoreCase exists in this library mainly for net48, which lacks it.
Console.WriteLine($"ReplaceIgnoreCase(\"Hello World\", \"WORLD\", \"There\") = {CV.ReplaceIgnoreCase("Hello World", "WORLD", "There")}");

var flattened = CV.FlattenObjectProperties(new SampleOrder(7, new SampleCustomer("Grace Hopper", "grace@example.com")));
foreach (var p in flattened) Console.WriteLine($"  {p.Name} = {p.Value}");

internal sealed record SampleOrder(int Id, SampleCustomer Customer);
internal sealed record SampleCustomer(string Name, string Email);
