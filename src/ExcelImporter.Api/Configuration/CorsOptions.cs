using System.ComponentModel.DataAnnotations;

namespace ExcelImporter.Api.Configuration;

public sealed class CorsOptions : IValidatableObject
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Frontend";

    /// <summary>Dozvoljeni origin-i frontenda (npr. http://localhost:5173). Prazno znači da CORS nije dozvoljen nikome.</summary>
    public string[] AllowedOrigins { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var origin in AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.PathAndQuery != "/" || origin == "*")
            {
                yield return new ValidationResult($"Neispravan origin: '{origin}'.", [nameof(AllowedOrigins)]);
            }
        }
    }
}