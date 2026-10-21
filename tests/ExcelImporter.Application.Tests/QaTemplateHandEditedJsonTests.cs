using ExcelImporter.Application.Templates;
using Shouldly;

namespace ExcelImporter.Application.Tests;

/// <summary>QA (COA-73): ručno izmenjen JSON u bazi mora uvek završiti kao TemplateInvalidException, nikad kao NRE/InvalidOperation.</summary>
public class QaTemplateHandEditedJsonTests
{
    private const string GoodMapping = """{"sourceColumns":["Ime"],"mappings":[{"sourceColumn":"Ime","targetColumn":"Name"}]}""";

    public static TheoryData<string, string, string> Corrupted => new()
    {
        { "", "[]", "[]" },
        { "null", "[]", "[]" },
        { "[]", "[]", "[]" },
        { """{"sourceColumns":null,"mappings":[]}""", "[]", "[]" },
        { """{"sourceColumns":[],"mappings":null}""", "[]", "[]" },
        { """{"sourceColumns":[null],"mappings":[]}""", "[]", "[]" },
        { """{"sourceColumns":[],"mappings":[null]}""", "[]", "[]" },
        { """{"sourceColumns":[],"mappings":[{"sourceColumn":null,"targetColumn":"Name"}]}""", "[]", "[]" },
        { """{"sourceColumns":[],"mappings":[{"sourceColumn":"A"}]}""", "[]", "[]" },
        { """{"sourceColumns":[],"mappings":[],"extra":1}""", "[]", "[]" },
        { GoodMapping, "", "[]" },
        { GoodMapping, "null", "[]" },
        { GoodMapping, "{}", "[]" },
        { GoodMapping, "[null]", "[]" },
        { GoodMapping, "[{}]", "[]" },
        { GoodMapping, """[{"type":"lookup","version":99}]""", "[]" },
        { GoodMapping, "[]", "" },
        { GoodMapping, "[]", "null" },
        { GoodMapping, "[]", "{}" },
        { GoodMapping, "[]", "[null]" },
        { GoodMapping, "[]", """[{"targetColumn":"Status"}]""" },
        { GoodMapping, "[]", """[{"targetColumn":"Status","value":{"dynamic":"nepoznato"}}]""" },
        { GoodMapping, "[]", """[{"targetColumn":"Status","value":{"dynamic":5}}]""" },
    };

    [Theory]
    [MemberData(nameof(Corrupted))]
    public void Deserialize_HandEditedJson_ThrowsTemplateInvalidOnly(string mapping, string rules, string constants)
    {
        Should.Throw<TemplateInvalidException>(() => TemplateContentSerializer.Deserialize(mapping, rules, constants));
    }

    [Theory]
    [InlineData("""{"sourceColumns":[null],"mappings":[]}""")]
    [InlineData("""{"sourceColumns":[],"mappings":[null]}""")]
    [InlineData("""{"sourceColumns":[],"mappings":[{"sourceColumn":"A"}]}""")]
    public void Apply_OnLoadedHandEditedTemplate_DoesNotThrowUnexpectedException(string mapping)
    {
        TemplateContent content;
        try
        {
            content = TemplateContentSerializer.Deserialize(mapping, "[]", "[]");
        }
        catch (TemplateInvalidException)
        {
            return; // ispravno ponašanje: odbijeno pri učitavanju
        }

        var template = new SavedTemplate(1, "x", 1, "dbo", "T", null, 1, "", content, DateTime.UtcNow, DateTime.UtcNow);
        var schema = new ExcelImporter.Application.Schema.DatabaseSchema([]);
        var result = Record.Exception(() => TemplateApplicator.Apply(template, ["A", "B"], schema));
        (result is null or TemplateInvalidException).ShouldBeTrue($"Neočekivan izuzetak: {result}");
    }
}
