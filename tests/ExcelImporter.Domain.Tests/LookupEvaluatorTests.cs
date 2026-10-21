using ExcelImporter.Domain.Rules;
using Shouldly;

namespace ExcelImporter.Domain.Tests;

public class LookupEvaluatorTests
{
    private static readonly LookupRule Rule = new()
    {
        TargetField = "GradId",
        SourceField = "Grad",
        Table = "Gradovi",
        KeyColumn = "Naziv",
        ReturnColumn = "Id",
    };

    private static LookupMap Cities() => new(
        [new("Beograd", 1), new("Novi Sad", 2), new("Niš", 3)],
        "dbo.Gradovi");

    private static Dictionary<string, object?> Row(object? city) => new Dictionary<string, object?> { ["Grad"] = city };

    [Fact]
    public void Hit_ReturnsId()
    {
        var result = LookupEvaluator.Evaluate(Rule, Cities(), Row("Novi Sad"), 2, targetIsNullable: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(2);
    }

    [Theory]
    [InlineData("novi sad")]
    [InlineData("NOVI SAD")]
    [InlineData("  Novi Sad\t")]
    public void Comparison_IgnoresCaseAndTrimsKey(string input)
    {
        var result = LookupEvaluator.Evaluate(Rule, Cities(), Row(input), 2, targetIsNullable: false);

        result.Value.ShouldBe(2);
    }

    [Fact]
    public void Dictionary_TrimsKeysFromDatabase()
    {
        var dictionary = new LookupMap([new(" Zagreb ", 9)], "dbo.Gradovi");

        LookupEvaluator.Evaluate(Rule, dictionary, Row("zagreb"), 2, false).Value.ShouldBe(9);
    }

    [Fact]
    public void NoHit_IsRowFailureWithReason_AndNeverSilentNull()
    {
        var result = LookupEvaluator.Evaluate(Rule, Cities(), Row("Subotica"), 7, targetIsNullable: true);

        result.IsSuccess.ShouldBeFalse();
        result.Failure!.RowNumber.ShouldBe(7);
        result.Failure.ColumnName.ShouldBe("GradId");
        result.Failure.Reason.ShouldContain("Subotica");
        result.Value.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyCell_WithNullableTarget_StaysNull(string? input)
    {
        var result = LookupEvaluator.Evaluate(Rule, Cities(), Row(input), 2, targetIsNullable: true);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void EmptyCell_WithNonNullableTarget_IsRowFailure()
    {
        var result = LookupEvaluator.Evaluate(Rule, Cities(), Row(null), 2, targetIsNullable: false);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void NullReturnValue_IsRowFailure()
    {
        var dictionary = new LookupMap([new("Beograd", null)], "dbo.Gradovi");

        LookupEvaluator.Evaluate(Rule, dictionary, Row("Beograd"), 2, true).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void NumericCell_IsMatchedAsInvariantText()
    {
        var dictionary = new LookupMap([new("5", 50)], "dbo.Sifre");

        LookupEvaluator.Evaluate(Rule, dictionary, Row(5), 2, false).Value.ShouldBe(50);
    }

    [Theory]
    [InlineData("Beograd", "beograd")]
    [InlineData("Beograd", " BEOGRAD ")]
    public void DuplicateKey_ThrowsConfigurationErrorNamingTheValue(string first, string second)
    {
        var ex = Should.Throw<LookupConfigurationException>(
            () => new LookupMap([new(first, 1), new(second, 2)], "dbo.Gradovi (Naziv → Id)"));

        ex.Message.ShouldContain("dbo.Gradovi");
        ex.Message.ShouldContain("duplikat");
        ex.Message.ShouldContain(second.Trim());
    }
}
