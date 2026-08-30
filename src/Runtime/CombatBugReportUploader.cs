using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CombatSolver;

internal readonly record struct CombatBugReportUploadProgress(long BytesSent, long TotalBytes)
{
    public int Percentage => TotalBytes <= 0
        ? 0
        : (int)Math.Clamp(BytesSent * 100L / TotalBytes, 0L, 100L);
}

internal sealed record CombatBugReportUploadReceipt(string ReportId, string SubmissionId);

internal static class CombatBugReportUploader
{
    public const long MaximumArchiveBytes = 128L * 1024 * 1024;
    public const int MaximumDescriptionUtf8Bytes = 64 * 1024;
    private const int MaximumResponseBytes = 64 * 1024;
    private const int MaximumDisplayedResponseCharacters = 1_000;
    private const string UploadEndpoint = "https://combatsolver.iryougi.com/api/v1/reports";
    private const string UploadTokenHeaderName = "X-CombatSolver-Key";

    // 仅用于过滤扫描器和误触的公共流量，不是访问控制。
    private const string UploadToken = "9d61747056101b511150372adcf98bf61aa49394803dcdd4";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };

    public static Task<CombatBugReportUploadReceipt> UploadAsync(
        string zipPath,
        string description,
        string contact,
        string submissionId,
        IProgress<CombatBugReportUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => UploadCoreAsync(
            Client,
            new Uri(UploadEndpoint),
            zipPath,
            description,
            contact,
            submissionId,
            progress,
            cancellationToken);

    internal static Task<CombatBugReportUploadReceipt> UploadForTestingAsync(
        HttpClient client,
        string zipPath,
        string description,
        string contact,
        string submissionId,
        IProgress<CombatBugReportUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => UploadCoreAsync(
            client,
            new Uri("https://combat-solver.test/reports"),
            zipPath,
            description,
            contact,
            submissionId,
            progress,
            cancellationToken);

    private static async Task<CombatBugReportUploadReceipt> UploadCoreAsync(
        HttpClient client,
        Uri endpoint,
        string zipPath,
        string description,
        string contact,
        string submissionId,
        IProgress<CombatBugReportUploadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateInputs(zipPath, description, contact, submissionId);
        await using FileStream stream = new(
            zipPath,
            FileMode.Open,
            System.IO.FileAccess.Read,
            FileShare.Read);
        using ProgressFileContent fileContent = new(stream, progress);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using MultipartFormDataContent form = new()
        {
            { fileContent, "report", Path.GetFileName(zipPath) },
            { new StringContent(description, Encoding.UTF8), "description" },
            { new StringContent(contact, Encoding.UTF8), "contact" },
        };
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint) { Content = form };
        request.Headers.Add(UploadTokenHeaderName, UploadToken);

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        string body = await ReadResponseBodyAsync(response.Content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"上传服务器返回 HTTP {(int)response.StatusCode}：{DescribeResponse(body)}");
        }

        string reportId = TryReadReportId(body) ?? submissionId;
        return new CombatBugReportUploadReceipt(reportId, submissionId);
    }

    private static void ValidateInputs(
        string zipPath,
        string description,
        string contact,
        string submissionId)
    {
        FileInfo archive = new(zipPath);
        if (!archive.Exists)
            throw new FileNotFoundException("待上传的问题包不存在。", zipPath);
        if (archive.Length <= 0 || archive.Length > MaximumArchiveBytes)
        {
            throw new InvalidDataException(
                $"问题包大小必须在 1–{MaximumArchiveBytes} 字节之间，实际为 {archive.Length} 字节。");
        }
        int descriptionBytes = Encoding.UTF8.GetByteCount(description);
        if (descriptionBytes > MaximumDescriptionUtf8Bytes)
        {
            throw new InvalidDataException(
                $"问题描述超过 {MaximumDescriptionUtf8Bytes} 字节，实际为 {descriptionBytes} 字节。");
        }
        if (contact.Length > 64)
            throw new InvalidDataException("联系信息最长 64 个字符。");
        if (!Guid.TryParseExact(submissionId, "N", out _))
            throw new InvalidDataException("提交编号格式无效。");
    }

    private static async Task<string> ReadResponseBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using Stream input = await content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream output = new(MaximumResponseBytes);
        byte[] buffer = new byte[8 * 1024];
        int remaining = MaximumResponseBytes;
        while (remaining > 0)
        {
            int read = await input.ReadAsync(
                buffer.AsMemory(0, Math.Min(buffer.Length, remaining)),
                cancellationToken);
            if (read == 0)
                break;
            output.Write(buffer, 0, read);
            remaining -= read;
        }
        return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
    }

    private static string? TryReadReportId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("id", out JsonElement id)
                || id.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            string? value = id.GetString();
            return string.IsNullOrWhiteSpace(value)
                ? null
                : CombatBugReportDescription.NormalizeDetail(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string DescribeResponse(string body)
    {
        string description = CombatBugReportDescription.NormalizeDetail(body) ?? "无响应正文";
        return description.Length <= MaximumDisplayedResponseCharacters
            ? description
            : description[..MaximumDisplayedResponseCharacters] + "…";
    }

    private sealed class ProgressFileContent(
        FileStream source,
        IProgress<CombatBugReportUploadProgress>? progress) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = source.Length;
            return true;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => WriteContentAsync(stream, CancellationToken.None);

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
            => WriteContentAsync(stream, cancellationToken);

        private async Task WriteContentAsync(Stream destination, CancellationToken cancellationToken)
        {
            long total = source.Length;
            long sent = 0;
            source.Position = 0;
            progress?.Report(new CombatBugReportUploadProgress(0, total));
            byte[] buffer = new byte[80 * 1024];
            while (true)
            {
                int read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                    break;
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                sent += read;
                progress?.Report(new CombatBugReportUploadProgress(sent, total));
            }
        }
    }
}
