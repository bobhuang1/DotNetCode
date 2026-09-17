using ShopCommon;
using Xunit;

namespace ShopCommon.Tests;

public sealed class CouponCodeGeneratorTests
{
    [Fact]
    public void Generate_returns_16_alphanumeric_chars()
    {
        var code = CouponCodeGenerator.Generate();

        Assert.Equal(16, code.Length);
        Assert.All(code, c => Assert.Contains(c, CouponCodeGenerator.Alphabet));
    }

    [Fact]
    public void Generate_uses_unambiguous_alphabet()
    {
        for (var i = 0; i < 100; i++)
        {
            var code = CouponCodeGenerator.Generate();
            Assert.All(code, c => Assert.DoesNotContain(c, "0O1IL"));
        }
    }

    [Fact]
    public void Generate_is_random_over_many_calls()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => CouponCodeGenerator.Generate()).ToHashSet();
        Assert.True(codes.Count > 45, $"Expected mostly-unique codes, got {codes.Count}/50");
    }

    [Fact]
    public void GeneratePrefixed_keeps_prefix_and_pads_to_16()
    {
        var code = CouponCodeGenerator.GeneratePrefixed("SPRNG");

        Assert.Equal(16, code.Length);
        Assert.StartsWith("SPRNG", code);
    }

    [Fact]
    public void GeneratePrefixed_strips_ambiguous_characters()
    {
        var code = CouponCodeGenerator.GeneratePrefixed("L0Cal0");

        Assert.Equal(16, code.Length);
        Assert.StartsWith("CA", code); // L and 0 are dropped from the prefix
    }

    [Fact]
    public void IsWellFormed_accepts_prefixed_codes()
    {
        var code = CouponCodeGenerator.GeneratePrefixed("SPRING");
        Assert.True(CouponCodeGenerator.IsWellFormed(code));
    }

    [Fact]
    public void IsWellFormed_accepts_valid_and_rejects_others()
    {
        Assert.True(CouponCodeGenerator.IsWellFormed("ABCDEFGH23456789"));
        Assert.False(CouponCodeGenerator.IsWellFormed("SHORT"));
        Assert.False(CouponCodeGenerator.IsWellFormed("ABCDEFGH234567890")); // 17 chars
        Assert.False(CouponCodeGenerator.IsWellFormed(null));
        Assert.False(CouponCodeGenerator.IsWellFormed("ABCDEFGH2345678O")); // O excluded
        Assert.False(CouponCodeGenerator.IsWellFormed("ABCDEFGH2345678" + "0")); // 0 excluded
    }

    [Fact]
    public void Deterministic_picker_produces_deterministic_codes()
    {
        static int AlwaysTwo(int max) => Math.Min(2, max - 1);

        var code = CouponCodeGenerator.Generate(AlwaysTwo);
        Assert.Equal(new string(Enumerable.Repeat(CouponCodeGenerator.Alphabet[2], 16).ToArray()), code);
    }
}
