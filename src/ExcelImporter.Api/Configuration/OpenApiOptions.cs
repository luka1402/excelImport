namespace ExcelImporter.Api.Configuration;

public sealed class OpenApiSettings
{
    public const string SectionName = "OpenApi";

    /// <summary>Isključuje se u okruženjima gde opis API-ja ne sme da bude javan.</summary>
    public bool Enabled { get; init; } = true;
}