namespace ExcelImporter.Domain.Entities;

/// <summary>Konekcija ka ciljnoj bazi. Connection string se nikad ne čuva: <see cref="ConfigKey"/> pokazuje na secret / env varijablu.</summary>
public sealed class ConnectionConfig
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string ConfigKey { get; set; }
}
