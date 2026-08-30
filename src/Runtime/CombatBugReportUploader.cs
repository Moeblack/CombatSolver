using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace CombatSolver;

internal static class CombatBugReportUploader
{
    private const string UploadEndpoint = "https://combatsolver.iryougi.com/api/v1/reports";
    private const string UploadTokenHeaderName = "X-CombatSolver-Key";

    // 仅用于过滤扫描器和误触的公共流量，不是访问控制；问题包内容仍以隐私提示为准。
    private const string UploadToken = "9d61747056101b511150372adcf98bf61aa49394803dcdd4";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };

    public static async Task<string> UploadAsync(
        string zipPath,
        string description,
        string contact,
        CancellationToken cancellationToken = default)
    {
        await using FileStream stream = new(zipPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        using StreamContent fileContent = new(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using MultipartFormDataContent form = new()
        {
            { fileContent, "report", Path.GetFileName(zipPath) },
            { new StringContent(description), "description" },
            { new StringContent(contact), "contact" },
        };
        using HttpRequestMessage request = new(HttpMethod.Post, UploadEndpoint) { Content = form };
        request.Headers.Add(UploadTokenHeaderName, UploadToken);

        using HttpResponseMessage response = await Client.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"上传服务器返回 HTTP {(int)response.StatusCode}：{body}");

        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("id", out JsonElement id)
            ? id.GetString() ?? body
            : body;
    }
}
