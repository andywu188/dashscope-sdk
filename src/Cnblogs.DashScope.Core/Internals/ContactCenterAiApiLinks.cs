namespace Cnblogs.DashScope.Core.Internals;

internal static class ContactCenterAiApiLinks
{
    public static string AnalyzeConversation(string workspaceId, string appId)
        => $"/{workspaceId}/ccai/app/{appId}/analyze_conversation";

    public static string RunCompletion(string workspaceId, string appId)
        => $"/{workspaceId}/ccai/app/{appId}/completion";

    public static string CreateTask(string workspaceId, string appId)
        => $"/{workspaceId}/ccai/app/{appId}/createTask";

    public const string GetTaskResult = "/ccai/app/getTaskResult";
}
