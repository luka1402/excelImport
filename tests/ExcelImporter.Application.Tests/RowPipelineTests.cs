using ExcelImporter.Application.Rules;
using ExcelImporter.Application.Schema;
using ExcelImporter.Domain.Rules;
using NSubstitute;
using Shouldly;

namespace ExcelImporter.Application.Tests;

public class RowPipelineTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static ColumnSchema Column(string name, string type, bool nullable = true, int? maxLength = null, byte precision = 0, byte scale = 0) =>
        new(name, 1, type, nullable, maxLength, precision, scale, false, false);

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells) =>
        cells.ToDictionary(cell => cell.Key, cell => cell.Value);

    private static Task<RowPipeline> Create(IEnumerable<Rule> rules, IReadOnlyList<ColumnSchema> columns, LookupMap? map = null)
    {
        var loader = Substitute.For<ILookupDictionaryLoader>();
        loader.LoadAsync(Arg.Any<string>(), Arg.Any<LookupRule>(), Arg.Any<CancellationToken>())
            .Returns(map ?? new LookupMap([], "test"));
        return RowPipeline.CreateAsync(rules, columns, "cs", loader, Ct);
    }

    private static RowFilterRule Filter(RowFilterAction action, params Condition[] conditions) =>
        new() { Action = action, Conditions = conditions };

    [Fact]
    public async Task Include_filter_skips_rows_and_counts_them()
    {
        var pipeline = await Create(
            [Filter(RowFilterAction.Include, new Condition("Kolicina", ComparisonOperator.Gt, "0"))],
            [Column("Kolicina", "int")]);

        pipeline.Process(Row(("Kolicina", 5)), 2).Kind.ShouldBe(RowOutcomeKind.Imported);
        pipeline.Process(Row(("Kolicina", 0)), 3).Kind.ShouldBe(RowOutcomeKind.Skipped);
        pipeline.Process(Row(("Kolicina", null)), 4).Kind.ShouldBe(RowOutcomeKind.Skipped);

        pipeline.ImportedCount.ShouldBe(1);
        pipeline.SkippedCount.ShouldBe(2);
        pipeline.FailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Exclude_filter_skips_matching_rows()
    {
        var pipeline = await Create(
            [Filter(RowFilterAction.Exclude, new Condition("Status", ComparisonOperator.Eq, "Obrisan"))],
            [Column("Status", "nvarchar", maxLength: 20)]);

        pipeline.Process(Row(("Status", "obrisan")), 2).Kind.ShouldBe(RowOutcomeKind.Skipped);
        pipeline.Process(Row(("Status", "Aktivan")), 3).Kind.ShouldBe(RowOutcomeKind.Imported);
        pipeline.SkippedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Include_and_exclude_filters_combine()
    {
        var pipeline = await Create(
            [
                Filter(RowFilterAction.Include, new Condition("Kolicina", ComparisonOperator.Gt, "0")),
                Filter(RowFilterAction.Exclude, new Condition("Status", ComparisonOperator.Eq, "Obrisan")),
            ],
            [Column("Kolicina", "int"), Column("Status", "nvarchar", maxLength: 20)]);

        pipeline.Process(Row(("Kolicina", 3), ("Status", "Obrisan")), 2).Kind.ShouldBe(RowOutcomeKind.Skipped);
        pipeline.Process(Row(("Kolicina", 0), ("Status", "Aktivan")), 3).Kind.ShouldBe(RowOutcomeKind.Skipped);
        pipeline.Process(Row(("Kolicina", 3), ("Status", "Aktivan")), 4).Kind.ShouldBe(RowOutcomeKind.Imported);
    }

    [Fact]
    public async Task Filter_is_evaluated_on_original_values_before_transforms()
    {
        // Da je transform pre filtera, "x" bi postao "X" i red ne bi bio preskočen.
        var pipeline = await Create(
            [
                new TransformRule { TargetField = "Status", Kind = TransformKind.Upper },
                Filter(RowFilterAction.Exclude, new Condition("Status", ComparisonOperator.Eq, "x")),
            ],
            [Column("Status", "nvarchar", maxLength: 5)]);

        pipeline.Process(Row(("Status", "x")), 2).Kind.ShouldBe(RowOutcomeKind.Skipped);
    }

    [Fact]
    public async Task Rules_are_applied_in_fixed_stage_order_regardless_of_list_order()
    {
        // Lista je namerno obrnuta: default → conditional → transform. Ispravan tok: Trim("  a  ")="a"
        // → conditional (Kod eq "a") daje "B" → default se ne primenjuje (nije prazno).
        var pipeline = await Create(
            [
                new DefaultValueRule { TargetField = "Kod", Value = "D" },
                new ConditionalValueRule
                {
                    TargetField = "Kod",
                    Conditions = [new Condition("Kod", ComparisonOperator.Eq, "a")],
                    Value = "B",
                },
                new TransformRule { TargetField = "Kod", Kind = TransformKind.Trim },
            ],
            [Column("Kod", "nvarchar", maxLength: 5)]);

        var outcome = pipeline.Process(Row(("Kod", "  a  ")), 2);

        outcome.Kind.ShouldBe(RowOutcomeKind.Imported);
        outcome.Values["Kod"].ShouldBe("B");
    }

    [Fact]
    public async Task Default_is_applied_after_conditional_so_conditional_sees_the_empty_value()
    {
        var pipeline = await Create(
            [
                new DefaultValueRule { TargetField = "Status", Value = "Nov" },
                new ConditionalValueRule
                {
                    TargetField = "Status",
                    Conditions = [new Condition("Status", ComparisonOperator.IsEmpty)],
                    Value = "Prazan",
                },
            ],
            [Column("Status", "nvarchar", maxLength: 10)]);

        pipeline.Process(Row(("Status", null)), 2).Values["Status"].ShouldBe("Prazan");
    }

    [Fact]
    public async Task Default_fills_empty_value_then_converts_to_column_type()
    {
        var pipeline = await Create(
            [new DefaultValueRule { TargetField = "Kolicina", Value = "1" }],
            [Column("Kolicina", "int", nullable: false)]);

        pipeline.Process(Row(("Kolicina", null)), 2).Values["Kolicina"].ShouldBe(1);
    }

    [Fact]
    public async Task Lookup_runs_after_transform_of_source_column_and_before_type_conversion()
    {
        var map = new LookupMap([new("novi sad", 21)], "gradovi");
        var pipeline = await Create(
            [
                new LookupRule { TargetField = "GradId", SourceField = "Grad", Table = "Gradovi", KeyColumn = "Naziv", ReturnColumn = "Id" },
                new TransformRule { TargetField = "Grad", Kind = TransformKind.Lower },
            ],
            [Column("GradId", "int", nullable: false)],
            map);

        var outcome = pipeline.Process(Row(("Grad", "  NOVI SAD ")), 2);

        // Trim radi lookup; Lower je potreban samo ako bi rečnik bio case-sensitive, ovde potvrđuje da je transform pre lookup-a.
        outcome.Kind.ShouldBe(RowOutcomeKind.Imported);
        outcome.Values["GradId"].ShouldBe(21);
    }

    [Fact]
    public async Task Unknown_lookup_value_fails_the_row_with_reason()
    {
        var pipeline = await Create(
            [new LookupRule { TargetField = "GradId", SourceField = "Grad", Table = "Gradovi", KeyColumn = "Naziv", ReturnColumn = "Id" }],
            [Column("GradId", "int", nullable: false)],
            new LookupMap([new("Beograd", 1)], "gradovi"));

        var outcome = pipeline.Process(Row(("Grad", "Atlantida")), 7);

        outcome.Kind.ShouldBe(RowOutcomeKind.Failed);
        var failure = outcome.Failures.ShouldHaveSingleItem();
        failure.RowNumber.ShouldBe(7);
        failure.ColumnName.ShouldBe("GradId");
        failure.Reason.ShouldContain("Atlantida");
        pipeline.FailedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Failed_transform_does_not_cascade_into_conversion_but_other_columns_still_report()
    {
        var pipeline = await Create(
            [new TransformRule { TargetField = "Datum", Kind = TransformKind.DateFormat, Format = "dd.MM.yyyy" }],
            [Column("Datum", "date", nullable: false), Column("Kolicina", "int", nullable: false)]);

        var outcome = pipeline.Process(Row(("Datum", "nije datum"), ("Kolicina", "abc")), 5);

        outcome.Kind.ShouldBe(RowOutcomeKind.Failed);
        outcome.Failures.Count.ShouldBe(2);
        outcome.Failures.Select(f => f.ColumnName).ShouldBe(["Datum", "Kolicina"], ignoreOrder: true);
        outcome.Failures.ShouldAllBe(f => f.RowNumber == 5 && f.Reason.Length > 0);
    }

    [Fact]
    public async Task Transformed_text_is_converted_to_target_types()
    {
        var pipeline = await Create(
            [
                new TransformRule { TargetField = "Datum", Kind = TransformKind.DateFormat, Format = "dd.MM.yyyy" },
                new TransformRule { TargetField = "Cena", Kind = TransformKind.DecimalSeparator, Separator = "," },
            ],
            [Column("Datum", "date"), Column("Cena", "decimal", precision: 10, scale: 2)]);

        var outcome = pipeline.Process(Row(("Datum", "31.12.2025"), ("Cena", "1.234,50")), 2);

        outcome.Values["Datum"].ShouldBe(new DateTime(2025, 12, 31));
        outcome.Values["Cena"].ShouldBe(1234.50m);
    }

    [Fact]
    public async Task Identity_and_computed_columns_are_not_written()
    {
        var pipeline = await Create(
            [],
            [
                Column("Naziv", "nvarchar", maxLength: 10),
                new ColumnSchema("Id", 2, "int", false, null, 10, 0, true, false),
                new ColumnSchema("Zbir", 3, "int", true, null, 10, 0, false, true),
            ]);

        var outcome = pipeline.Process(Row(("Naziv", "a"), ("Id", 5), ("Zbir", 1)), 2);

        outcome.Values.Keys.ShouldBe(["Naziv"]);
    }

    [Fact]
    public async Task Lookup_dictionary_is_loaded_once_not_per_row()
    {
        var loader = Substitute.For<ILookupDictionaryLoader>();
        loader.LoadAsync(Arg.Any<string>(), Arg.Any<LookupRule>(), Arg.Any<CancellationToken>())
            .Returns(new LookupMap([new("a", 1)], "t"));
        var pipeline = await RowPipeline.CreateAsync(
            [new LookupRule { TargetField = "X", SourceField = "S", Table = "T", KeyColumn = "K", ReturnColumn = "R" }],
            [Column("X", "int")],
            "cs",
            loader,
            Ct);

        for (var i = 0; i < 5; i++)
        {
            pipeline.Process(Row(("S", "a")), i + 2);
        }

        await loader.Received(1).LoadAsync("cs", Arg.Any<LookupRule>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("int", "abc", "ceo broj")]
    [InlineData("int", "3000000000", "van opsega")]
    [InlineData("tinyint", "256", "van opsega")]
    [InlineData("int", "1.5", "ceo broj")]
    [InlineData("bit", "mozda", "logička")]
    [InlineData("date", "nije", "datum")]
    [InlineData("uniqueidentifier", "xyz", "GUID")]
    [InlineData("geography", "x", "nije podržan")]
    public void Conversion_fails_with_reason(string dataType, string value, string expectedReason)
    {
        var result = ColumnValueConverter.Convert(Column("K", dataType), value, 9);

        result.IsSuccess.ShouldBeFalse();
        result.Failure!.RowNumber.ShouldBe(9);
        result.Failure.ColumnName.ShouldBe("K");
        result.Failure.Reason.ShouldContain(expectedReason);
    }

    [Fact]
    public void Conversion_rejects_decimal_exceeding_scale_and_precision_and_text_exceeding_length()
    {
        var money = Column("C", "decimal", precision: 5, scale: 2);
        ColumnValueConverter.Convert(money, "1.234", 1).IsSuccess.ShouldBeFalse();
        ColumnValueConverter.Convert(money, "1000.00", 1).IsSuccess.ShouldBeFalse();
        ColumnValueConverter.Convert(money, "999.99", 1).Value.ShouldBe(999.99m);

        var text = Column("T", "nvarchar", maxLength: 3);
        ColumnValueConverter.Convert(text, "abcd", 1).Failure!.Reason.ShouldContain("najviše 3");
        ColumnValueConverter.Convert(text, "abc", 1).Value.ShouldBe("abc");
    }

    [Fact]
    public void Empty_value_becomes_null_only_for_nullable_columns()
    {
        ColumnValueConverter.Convert(Column("K", "int"), "  ", 1).Value.ShouldBeNull();
        ColumnValueConverter.Convert(Column("K", "int"), null, 1).IsSuccess.ShouldBeTrue();
        ColumnValueConverter.Convert(Column("K", "int", nullable: false), null, 1).Failure!.Reason.ShouldContain("NULL");
    }

    [Fact]
    public void Conversion_accepts_native_excel_values()
    {
        ColumnValueConverter.Convert(Column("K", "int"), 5.0, 1).Value.ShouldBe(5);
        ColumnValueConverter.Convert(Column("K", "int"), 5.5, 1).IsSuccess.ShouldBeFalse();
        ColumnValueConverter.Convert(Column("K", "bit"), true, 1).Value.ShouldBe(true);
        ColumnValueConverter.Convert(Column("K", "datetime2"), new DateTime(2025, 1, 2), 1).Value.ShouldBe(new DateTime(2025, 1, 2));
        ColumnValueConverter.Convert(Column("K", "nvarchar", maxLength: 10), 12, 1).Value.ShouldBe("12");
    }
}
