using System.Net;
using System.Text;
using Cnblogs.DashScope.Core;
using Cnblogs.DashScope.Tests.Shared.Utils;
using NSubstitute;
using NSubstitute.Extensions;

namespace Cnblogs.DashScope.Sdk.UnitTests;

public class ContactCenterAiClientTests
{
    private const string WorkspaceId = "ws-demo";
    private const string AppId = "app-demo";

    [Fact]
    public async Task AnalyzeConversation_SummaryBestPractice_SerializesResultTypesAndDialogueAsync()
    {
        var dialogue = CreateSampleDialogue();
        var request = AnalyzeConversationRequest.ForSummary(dialogue, CcaiResultTypes.Title, CcaiResultTypes.Keywords);
        var responseJson =
            """{"errorCode":"success","errorInfo":"success","finishReason":"stop","requestId":"req-1","success":true,"text":"客户咨询健康险产品","inputTokens":"10","outputTokens":"20","totalTokens":"30"}""";
        var (client, handler, bodies) = CreateClient(responseJson);

        var response = await client.AnalyzeConversationAsync(WorkspaceId, AppId, request);

        Assert.True(response.Success);
        Assert.Equal("客户咨询健康险产品", response.Text);
        Assert.Equal("req-1", response.RequestId);
        AssertSignedRequest(
            handler,
            HttpMethod.Post,
            $"/{WorkspaceId}/ccai/app/{AppId}/analyze_conversation",
            "AnalyzeConversation");
        AssertCapturedBodyContains(bodies, "\"resultTypes\":[\"summary\",\"title\",\"keywords\"]");
        AssertCapturedBodyContains(bodies, "\"role\":\"agent\"");
    }

    [Fact]
    public async Task AnalyzeConversation_FieldExtractionBestPractice_IncludesFieldsAsync()
    {
        var request = AnalyzeConversationRequest.ForFieldExtraction(
            CreateSampleDialogue(),
            new[]
            {
                new CcaiField { Name = "问题类型", Desc = "客户咨询的问题类型" },
                new CcaiField { Name = "公司名称", Desc = "客服所属的保险公司名称" }
            });
        var (client, handler, bodies) = CreateClient(
            """{"success":true,"text":"{\"问题类型\":\"健康险咨询\"}","requestId":"req-fields"}""");

        var response = await client.AnalyzeConversationAsync(WorkspaceId, AppId, request);

        Assert.Equal("req-fields", response.RequestId);
        AssertSignedRequest(
            handler,
            HttpMethod.Post,
            $"/{WorkspaceId}/ccai/app/{AppId}/analyze_conversation",
            "AnalyzeConversation");
        AssertCapturedBodyContains(bodies, "\"resultTypes\":[\"fields\"]");
        AssertCapturedBodyContains(bodies, "\"name\":\"问题类型\"");
    }

    [Fact]
    public async Task AnalyzeConversation_ServiceInspectionBestPractice_IncludesInspectionPayloadAsync()
    {
        var request = AnalyzeConversationRequest.ForServiceInspection(
            CreateSampleDialogue(),
            new CcaiServiceInspection
            {
                SceneIntroduction = "保险销售场景",
                InspectionIntroduction = "请检测客服是否存在服务不当的行为，包括：过度承诺、故意套取客户隐私信息等",
                InspectionContents = new List<CcaiInspectionContent>
                {
                    new()
                    {
                        Title = "客服是否过度承诺",
                        Content =
                            "客服在服务客户过程中，基于已有的服务标准是否存在过度承诺的行为，如：最快到货时间是12小时，无法给客户承诺更快的到货时间。"
                    },
                    new()
                    {
                        Title = "客户情绪是否正向",
                        Content =
                            "分析对话内容，输出用户在对话中表现出的情绪。"
                    }
                }
            });
        var (client, handler, bodies) = CreateClient(
            """{"success":true,"text":"未发现过度承诺","requestId":"req-qi"}""");

        var response = await client.AnalyzeConversationAsync(WorkspaceId, AppId, request);

        Assert.Equal("未发现过度承诺", response.Text);
        AssertSignedRequest(
            handler,
            HttpMethod.Post,
            $"/{WorkspaceId}/ccai/app/{AppId}/analyze_conversation",
            "AnalyzeConversation");
        AssertCapturedBodyContains(bodies, "\"resultTypes\":[\"service_inspection\"]");
        AssertCapturedBodyContains(bodies, "\"sceneIntroduction\":\"保险销售场景\"");
    }

    [Fact]
    public async Task AnalyzeConversationStream_ParsesSseChunksAsync()
    {
        var sse =
            "data:{\"success\":true,\"text\":\"客\",\"finishReason\":null}\n\n" +
            "data:{\"success\":true,\"text\":\"户咨询\",\"finishReason\":\"stop\",\"requestId\":\"sse-1\"}\n\n";
        var (client, _, _) = CreateClient(sse, "text/event-stream");
        var request = AnalyzeConversationRequest.ForSummary(CreateSampleDialogue());
        request.Stream = true;

        var chunks = await client.AnalyzeConversationStreamAsync(WorkspaceId, AppId, request).ToListAsync();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("客", chunks[0].Text);
        Assert.Equal("户咨询", chunks[1].Text);
        Assert.Equal("stop", chunks[1].FinishReason);
    }

    [Fact]
    public async Task CreateTask_TextSummary_PostsExpectedPathAsync()
    {
        var request = new CcaiCreateTaskRequest
        {
            TaskType = "text",
            ModelCode = "tyxmTurbo",
            Dialogue = CreateSampleDialogue(),
            ResultTypes = new List<string> { CcaiResultTypes.Summary }
        };
        var (client, handler, bodies) = CreateClient(
            """{"data":{"taskId":"task-1"},"requestId":"req-task","success":"True"}""");

        var response = await client.CreateTaskAsync(WorkspaceId, AppId, request);

        Assert.Equal("task-1", response.Data?.TaskId);
        AssertSignedRequest(
            handler,
            HttpMethod.Post,
            $"/{WorkspaceId}/ccai/app/{AppId}/createTask",
            "CreateTask");
        AssertCapturedBodyContains(bodies, "\"taskType\":\"text\"");
    }

    [Fact]
    public async Task GetTaskResult_AppendsQueryParametersAsync()
    {
        var (client, handler, _) = CreateClient(
            """{"data":{"taskId":"task-1","taskStatus":"FINISH","text":"done"},"requestId":"req-get","success":"True"}""");

        var response = await client.GetTaskResultAsync("task-1", new[] { "asr_result" });

        Assert.Equal("FINISH", response.Data?.TaskStatus);
        handler.Received().MockSend(
            Arg.Is<HttpRequestMessage>(m =>
                m.Method == HttpMethod.Get
                && m.RequestUri!.PathAndQuery.Contains("/ccai/app/getTaskResult", StringComparison.Ordinal)
                && m.RequestUri.Query.Contains("taskId=task-1", StringComparison.Ordinal)
                && m.RequestUri.Query.Contains("requiredFieldList=asr_result", StringComparison.Ordinal)
                && m.Headers.Contains("Authorization")
                && m.Headers.GetValues("x-acs-action").Single() == "GetTaskResult"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCompletion_UsesPascalCaseBodyAsync()
    {
        var request = new CcaiRunCompletionRequest
        {
            TemplateIds = new List<long> { 47 },
            ModelCode = "tyxmTurbo",
            Stream = false,
            Dialogue = new CcaiRunCompletionDialogue
            {
                SessionId = "session-01",
                Sentences = new List<CcaiRunCompletionSentence>
                {
                    new() { Role = "user", Text = "我要办理信用卡", ChatId = "chat_1" }
                }
            }
        };
        var (client, handler, bodies) = CreateClient(
            """{"FinishReason":"stop","RequestId":"run-1","Text":"ok","inputTokens":"1","outputTokens":"2","totalTokens":"3"}""");

        var response = await client.RunCompletionAsync(WorkspaceId, AppId, request);

        Assert.Equal("ok", response.Text);
        AssertSignedRequest(
            handler,
            HttpMethod.Post,
            $"/{WorkspaceId}/ccai/app/{AppId}/completion",
            "RunCompletion");
        AssertCapturedBodyContains(bodies, "\"Dialogue\":{");
        AssertCapturedBodyContains(bodies, "\"TemplateIds\":[47]");
    }

    [Fact]
    public async Task AnalyzeConversation_HttpError_ThrowsContactCenterAiExceptionAsync()
    {
        var (client, _, _) = CreateClient(
            """{"errorCode":"CCAI.ParamInvalid.IllegalParamValue","errorInfo":"bad param","requestId":"err-1"}""",
            statusCode: HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<ContactCenterAiException>(() =>
            client.AnalyzeConversationAsync(
                WorkspaceId,
                AppId,
                AnalyzeConversationRequest.ForSummary(CreateSampleDialogue())));

        Assert.Equal(400, ex.Status);
        Assert.Equal("CCAI.ParamInvalid.IllegalParamValue", ex.ErrorCode);
        Assert.Equal("bad param", ex.ErrorInfo);
        Assert.Equal("err-1", ex.RequestId);
    }

    [Fact]
    public void AnalyzeConversationRequest_Factories_SetExpectedResultTypes()
    {
        var dialogue = CreateSampleDialogue();
        Assert.Equal(
            new[] { CcaiResultTypes.Summary },
            AnalyzeConversationRequest.ForSummary(dialogue).ResultTypes);
        Assert.Equal(
            new[] { CcaiResultTypes.Fields },
            AnalyzeConversationRequest.ForFieldExtraction(
                dialogue,
                new[] { new CcaiField { Name = "a", Desc = "b" } }).ResultTypes);
        Assert.Equal(
            new[] { CcaiResultTypes.ServiceInspection },
            AnalyzeConversationRequest.ForServiceInspection(
                dialogue,
                new CcaiServiceInspection
                {
                    InspectionContents = new List<CcaiInspectionContent>
                    {
                        new() { Title = "t", Content = "c" }
                    },
                    InspectionIntroduction = "i",
                    SceneIntroduction = "s"
                }).ResultTypes);
    }

    private static CcaiDialogue CreateSampleDialogue()
        => new()
        {
            SessionId = "session-adslsddxxxx",
            Sentences = new List<CcaiSentence>
            {
                new() { Role = "agent", Text = "您好，这里是xxx保险公司，请问有什么可以帮您" },
                new() { Role = "user", Text = "嗯，我想办理一个健康险，帮我介绍下有哪些" }
            }
        };

    private static (ContactCenterAiClient Client, MockHttpMessageHandler Handler, List<string> CapturedBodies)
        CreateClient(
            string responseBody,
            string mediaType = "application/json",
            HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var capturedBodies = new List<string>();
        var handler = Substitute.ForPartsOf<MockHttpMessageHandler>();
        handler.Configure()
            .MockSend(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var message = ci.ArgAt<HttpRequestMessage>(0);
                if (message.Content is not null)
                {
                    capturedBodies.Add(message.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                }

                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
                };
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://contactcenterai.cn-shanghai.aliyuncs.com/")
        };
        var client = new ContactCenterAiClient(
            httpClient,
            new ContactCenterAiOptions
            {
                AccessKeyId = "LTAI5tTestAccessKeyId",
                AccessKeySecret = "TestAccessKeySecret",
                Endpoint = "contactcenterai.cn-shanghai.aliyuncs.com"
            });
        return (client, handler, capturedBodies);
    }

    private static void AssertSignedRequest(
        MockHttpMessageHandler handler,
        HttpMethod method,
        string path,
        string action)
    {
        handler.Received().MockSend(
            Arg.Is<HttpRequestMessage>(m =>
                m.Method == method
                && m.RequestUri!.AbsolutePath == path
                && m.Headers.GetValues("x-acs-action").Single() == action
                && m.Headers.Contains("Authorization")
                && m.Headers.Contains("x-acs-content-sha256")
                && m.Headers.Contains("x-acs-date")
                && m.Headers.Contains("x-acs-signature-nonce")
                && m.Headers.Contains("x-acs-version")),
            Arg.Any<CancellationToken>());
    }

    private static void AssertCapturedBodyContains(IReadOnlyList<string> bodies, string expectedBodySubstring)
    {
        Assert.Contains(bodies, body => body.Contains(expectedBodySubstring, StringComparison.Ordinal));
    }
}
