using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace ExcelImporter.Api.Tests;

public sealed class AnalyzeEndpointTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly string uploadDirectory = Path.Combine(Path.GetTempPath(), $"analyze-tests-{Guid.NewGuid():N}");
    private readonly HttpClient client;

    public AnalyzeEndpointTests(ApiFactory factory)
    {
        Directory.CreateDirectory(uploadDirectory);
        client = factory.WithWebHostBuilder(builder => builder.UseSetting("UploadStorage:Directory", uploadDirectory)).CreateClient();
    }

    public void Dispose()
    {
        client.Dispose();
        Directory.Delete(uploadDirectory, recursive: true);
    }

    private Guid Upload(string testDataFile)
    {
        var fileId = Guid.NewGuid();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", testDataFile), Path.Combine(uploadDirectory, $"{fileId:N}.xlsx"));
        return fileId;
    }

    [Fact]
    public async Task Analyze_ReturnsAllSheetsAndTwentyRowPreview_ForFirstVisibleSheet()
    {
        var fileId = Upload("samples/04-multi-sheet.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("sheets").GetArrayLength().ShouldBe(5);
        body.GetProperty("selectedSheetIndex").GetInt32().ShouldBe(0);
        body.GetProperty("previewRows").GetArrayLength().ShouldBeLessThanOrEqualTo(20);
    }

    [Fact]
    public async Task Analyze_ReturnsPreviewOfRequestedSheet_AndCapsAtTwentyRows()
    {
        var fileId = Upload("samples/01-valid.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze?sheet=0", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        body.GetProperty("previewRows").GetArrayLength().ShouldBe(20);
    }

    [Fact]
    public async Task Analyze_ReturnsCyrillicSheetNameWithoutCorruption()
    {
        var fileId = Upload("samples/07-cyrillic-special.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonDocument.Parse(json).RootElement.GetProperty("sheets")[0].GetProperty("name").GetString().ShouldBe("Подаци");
    }

    [Fact]
    public async Task Analyze_Returns404_WhenSheetDoesNotExist()
    {
        var fileId = Upload("samples/01-valid.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze?sheet=7", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analyze_ReturnsDetectedHeaderAndColumnTypes()
    {
        var fileId = Upload("samples/01-valid.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("headerRowIndex").GetInt32().ShouldBe(0);
        body.GetProperty("headerChosenManually").GetBoolean().ShouldBeFalse();
        body.GetProperty("columns").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_UsesManuallyChosenHeaderRow()
    {
        var fileId = Upload("samples/01-valid.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze?headerRow=2", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        body.GetProperty("headerRowIndex").GetInt32().ShouldBe(2);
        body.GetProperty("headerChosenManually").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Analyze_Returns400_WhenHeaderRowDoesNotExist()
    {
        var fileId = Upload("samples/01-valid.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze?headerRow=100000", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Analyze_Returns404_WhenFileIsUnknown()
    {
        var response = await client.GetAsync($"/api/files/{Guid.NewGuid()}/analyze", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analyze_Returns404_WhenFileIdIsNotAGuid_SoPathsCannotBeInjected()
    {
        var response = await client.GetAsync("/api/files/..%2F..%2Fsecret/analyze", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Analyze_Returns400ProblemDetails_WhenContentIsNotXlsx()
    {
        var fileId = Upload("malicious/m01-xlsx-renamed-from-txt.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Analyze_Returns400_WhenFileIsZipBomb()
    {
        var fileId = Upload("malicious/m08-zip-bomb.xlsx");

        var response = await client.GetAsync($"/api/files/{fileId}/analyze", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
