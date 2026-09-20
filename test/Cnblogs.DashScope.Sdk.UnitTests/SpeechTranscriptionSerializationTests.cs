using System.Net.Http.Headers;
using Cnblogs.DashScope.Core;
using Cnblogs.DashScope.Tests.Shared.Utils;
using NSubstitute;
using NSubstitute.Extensions;

namespace Cnblogs.DashScope.Sdk.UnitTests;

public class SpeechTranscriptionSerializationTests
{
    [Fact]
    public async Task SpeechTranscription_CreateTask_SuccessAsync()
    {
        // Arrange
        const bool sse = false;
        var testCase = Snapshots.SpeechTranscription.CreateTask;
        var (client, handler) = await Sut.GetTestClientAsync(sse, testCase);

        // Act
        var response = await client.CreateSpeechTranscriptionTaskAsync(testCase.RequestModel);

        // Assert
        handler.Received().MockSend(
            Arg.Is<HttpRequestMessage>(m
                => m.Headers.Contains("X-DashScope-Async")
                   && Checkers.IsJsonEquivalent(m.Content!, testCase.GetRequestJson(sse))),
            Arg.Any<CancellationToken>());
        Assert.Equivalent(testCase.ResponseModel, response);
    }

    [Fact]
    public async Task SpeechTranscription_CreateTaskWithOss_SuccessAsync()
    {
        // Arrange
        const bool sse = false;
        var testCase = Snapshots.SpeechTranscription.CreateTaskWithOss;
        var (client, handler) = await Sut.GetTestClientAsync(sse, testCase);

        // Act
        var response = await client.CreateSpeechTranscriptionTaskAsync(testCase.RequestModel);

        // Assert
        handler.Received().MockSend(
            Arg.Is<HttpRequestMessage>(m
                => m.Headers.Contains("X-DashScope-Async")
                   && m.Headers.GetValues("X-DashScope-OssResourceResolve").First() == "enable"
                   && Checkers.IsJsonEquivalent(m.Content!, testCase.GetRequestJson(sse))),
            Arg.Any<CancellationToken>());
        Assert.Equivalent(testCase.ResponseModel, response);
    }

    [Fact]
    public async Task SpeechTranscription_GetTask_SuccessAsync()
    {
        // Arrange
        const bool sse = false;
        var testCase = Snapshots.SpeechTranscription.GetTaskSuccess;
        var (client, _) = await Sut.GetTestClientAsync(sse, testCase);

        // Act
        var task = await client.GetSpeechTranscriptionTaskAsync(testCase.ResponseModel.Output.TaskId);

        // Assert
        Assert.Equivalent(testCase.ResponseModel, task);
    }

    [Fact]
    public async Task SpeechTranscription_DownloadResult_SuccessAsync()
    {
        // Arrange
        const bool sse = false;
        var testCase = Snapshots.SpeechTranscription.DownloadResult;
        var (client, _) = await Sut.GetTestClientAsync(sse, testCase);

        // Act
        var result = await client.GetSpeechTranscriptionResultAsync(
            "https://dashscope-result-bj.oss-cn-beijing.aliyuncs.com/result.json");

        // Assert
        Assert.Equivalent(testCase.ResponseModel, result);
    }

    [Fact]
    public async Task SpeechTranscription_DownloadResult_DoesNotForwardDashScopeHeadersAsync()
    {
        // Arrange
        const bool sse = false;
        var testCase = Snapshots.SpeechTranscription.DownloadResult;
        var handler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        var response = await testCase.ToResponseMessageAsync(sse);
        handler.Configure().MockSend(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
            .Returns(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret");
        httpClient.DefaultRequestHeaders.Add("X-DashScope-WorkSpace", "workspace-id");
        var downloadClient = new HttpClient(handler, disposeHandler: false);
        var client = new DashScopeClientCore(
            httpClient,
            new DashScopeClientWebSocketPool(new DashScopeClientWebSocketFactory(), new DashScopeOptions()),
            downloadClient);

        // Act
        var result = await client.GetSpeechTranscriptionResultAsync(
            "https://dashscope-result-bj.oss-cn-beijing.aliyuncs.com/result.json");

        // Assert
        Assert.Equivalent(testCase.ResponseModel, result);
        handler.Received().MockSend(
            Arg.Is<HttpRequestMessage>(m
                => m.RequestUri != null
                   && m.RequestUri.ToString() == "https://dashscope-result-bj.oss-cn-beijing.aliyuncs.com/result.json"
                   && m.Headers.Authorization == null
                   && m.Headers.Contains("X-DashScope-WorkSpace") == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SpeechTranscription_DownloadResult_InvalidHost_ExceptionAsync()
    {
        // Arrange
        var (client, handler) = Sut.GetTestClient();

        // Act
        var act = async () => await client.GetSpeechTranscriptionResultAsync("https://example.com/result.json");

        // Assert
        var ex = await Assert.ThrowsAsync<DashScopeException>(act);
        Assert.Equal("https://example.com/result.json", ex.ApiUrl);
        handler.DidNotReceive().MockSend(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
    }
}
