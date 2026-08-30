using System.Net;
using System.Net.Http;
using System.Text;
using Godot;
using NetHttpClient = System.Net.Http.HttpClient;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private async Task AssertBugReportUploadBoundariesAsync()
    {
        string directory = ProjectSettings.GlobalizePath("user://combat-solver-test-upload");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"upload-{_request.RunId}.zip");
        File.WriteAllBytes(path, new byte[200 * 1024]);
        try
        {
            string submissionId = Guid.NewGuid().ToString("N");
            List<CombatBugReportUploadProgress> progress = [];
            using FakeUploadHandler successHandler = new(
                HttpStatusCode.OK,
                "accepted-without-json");
            using NetHttpClient successClient = new(successHandler);
            CombatBugReportUploadReceipt receipt = await CombatBugReportUploader.UploadForTestingAsync(
                successClient,
                path,
                "玩家描述\n\n【CombatSolver 提交编号】" + submissionId,
                "123456",
                submissionId,
                new DirectTestProgress<CombatBugReportUploadProgress>(progress.Add));
            if (receipt.ReportId != submissionId
                || receipt.SubmissionId != submissionId
                || progress.Count == 0
                || progress[^1].Percentage != 100
                || !successHandler.SawMultipartReport
                || !successHandler.SawDescription
                || !successHandler.SawContact
                || !successHandler.SawUploadToken)
            {
                throw new InvalidOperationException(
                    "问题包上传成功回执、multipart 字段或传输进度不完整：" +
                    $"report_id={receipt.ReportId == submissionId} " +
                    $"submission_id={receipt.SubmissionId == submissionId} " +
                    $"progress_count={progress.Count} " +
                    $"progress_last={(progress.Count == 0 ? -1 : progress[^1].Percentage)} " +
                    $"report={successHandler.SawMultipartReport} " +
                    $"description={successHandler.SawDescription} " +
                    $"contact={successHandler.SawContact} " +
                    $"token={successHandler.SawUploadToken}。");
            }

            using FakeUploadHandler numericIdHandler = new(HttpStatusCode.OK, "{\"id\":123}");
            using NetHttpClient numericIdClient = new(numericIdHandler);
            CombatBugReportUploadReceipt numericIdReceipt = await CombatBugReportUploader.UploadForTestingAsync(
                numericIdClient,
                path,
                "测试\n\n【CombatSolver 提交编号】" + submissionId,
                string.Empty,
                submissionId);
            if (numericIdReceipt.ReportId != submissionId)
                throw new InvalidOperationException("非字符串服务端编号没有回退到客户端提交编号。");

            using FakeUploadHandler arrayResponseHandler = new(HttpStatusCode.OK, "[]");
            using NetHttpClient arrayResponseClient = new(arrayResponseHandler);
            CombatBugReportUploadReceipt arrayResponseReceipt = await CombatBugReportUploader.UploadForTestingAsync(
                arrayResponseClient,
                path,
                "测试\n\n【CombatSolver 提交编号】" + submissionId,
                string.Empty,
                submissionId);
            if (arrayResponseReceipt.ReportId != submissionId)
                throw new InvalidOperationException("非对象成功响应没有回退到客户端提交编号。");

            string oversizedResponse = new string('x', 70_000) + "\nUNBOUNDED_RESPONSE_TAIL";
            using FakeUploadHandler failureHandler = new(
                HttpStatusCode.InternalServerError,
                oversizedResponse);
            using NetHttpClient failureClient = new(failureHandler);
            try
            {
                await CombatBugReportUploader.UploadForTestingAsync(
                    failureClient,
                    path,
                    "测试\n\n【CombatSolver 提交编号】" + submissionId,
                    string.Empty,
                    submissionId);
                throw new InvalidOperationException("服务端错误响应没有终止上传。");
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Length > 1_100
                    || ex.Message.Contains("UNBOUNDED_RESPONSE_TAIL", StringComparison.Ordinal)
                    || ex.Message.Contains('\n'))
                {
                    throw new InvalidOperationException("服务端错误响应没有限制长度或折叠换行。", ex);
                }
            }

            using FakeUploadHandler unusedHandler = new(HttpStatusCode.OK, "{}");
            using NetHttpClient unusedClient = new(unusedHandler);
            try
            {
                await CombatBugReportUploader.UploadForTestingAsync(
                    unusedClient,
                    path,
                    new string('界', CombatBugReportUploader.MaximumDescriptionUtf8Bytes),
                    string.Empty,
                    submissionId);
                throw new InvalidOperationException("超长问题描述没有在发出请求前失败。");
            }
            catch (InvalidDataException)
            {
            }
            if (unusedHandler.RequestCount != 0)
                throw new InvalidOperationException("超长问题描述仍然访问了上传服务。");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class DirectTestProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class FakeUploadHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool SawMultipartReport { get; private set; }
        public bool SawDescription { get; private set; }
        public bool SawContact { get; private set; }
        public bool SawUploadToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            SawUploadToken = request.Headers.Contains("X-CombatSolver-Key");
            if (request.Content is MultipartFormDataContent multipart)
            {
                string[] names = multipart
                    .Select(content => content.Headers.ContentDisposition?.Name?.Trim('"'))
                    .Where(name => name != null)
                    .Cast<string>()
                    .ToArray();
                SawMultipartReport = names.Contains("report", StringComparer.Ordinal);
                SawDescription = names.Contains("description", StringComparer.Ordinal);
                SawContact = names.Contains("contact", StringComparer.Ordinal);
            }
            using MemoryStream serialized = new();
            if (request.Content != null)
                await request.Content.CopyToAsync(serialized, cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8),
            };
        }
    }
}
