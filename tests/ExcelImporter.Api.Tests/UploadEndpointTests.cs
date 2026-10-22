using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public sealed class UploadEndpointTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"upload-tests-{Guid.NewGuid():N}");
    private readonly HttpClient client;

    public UploadEndpointTests(ApiFactory factory)
    {
        client = factory.WithWebHostBuilder(builder => builder.UseSetting("UploadStorage:Directory", directory)).CreateClient();
    }

    [Fact]
    public async Task Upload_ValidWorkbook_CanBeAnalyzed()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "TestData", "samples", "01-valid.xlsx"), TestContext.Current.CancellationToken)), "file", "sample.xlsx");
        using var response = await client.PostAsync("/api/files", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var uploaded = await response.Content.ReadFromJsonAsync<ExcelImporter.Api.Files.UploadResponse>(TestContext.Current.CancellationToken);
        uploaded.ShouldNotBeNull();
        using var analysis = await client.GetAsync($"/api/files/{uploaded.FileId}/analyze", TestContext.Current.CancellationToken);
        analysis.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("bad.xlsx", HttpStatusCode.BadRequest)]
    [InlineData("bad.txt", HttpStatusCode.UnsupportedMediaType)]
    public async Task Upload_InvalidFile_IsRejectedWithoutStoring(string name, HttpStatusCode expected)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("invalid workbook"u8.ToArray()), "file", name);
        using var response = await client.PostAsync("/api/files", form, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(expected);
        if (Directory.Exists(directory))
        {
            Directory.GetFiles(directory).ShouldBeEmpty();
        }
    }

    public void Dispose()
    {
        client.Dispose();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
