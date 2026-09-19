# 伶鹊 CCAI-对话分析 AIO 接入设计

> 产品正式名称统一为 **伶鹊 CCAI-对话分析 AIO**（旧称「通义晓蜜 CCAI-对话分析 AIO」仅作文档对照，代码与注释一律使用新名称）。

## 背景

阿里云 ContactCenterAI (`2024-06-03`) 提供对话分析能力，与现有 DashScope Bearer API Key 调用不同：

- Endpoint：`contactcenterai.cn-shanghai.aliyuncs.com`
- 鉴权：AccessKey + ACS3-HMAC-SHA256（ROA）
- 路径形如：`/{workspaceId}/ccai/app/{appId}/analyze_conversation`

官方最佳实践中的服务质检、字段抽取、摘要/标题/关键词，均通过 `AnalyzeConversation` 的 `resultTypes` 完成。

## 目标

在本 SDK 中新增独立的 ContactCenterAI 客户端，覆盖对话分析主链路，并按现有单元测试风格提供序列化与签名回归测试。

## 非目标（本期不做）

- 热词管理（Create/Update/List/Delete/GetVocab）——与百炼非实时 ASR 的 `speech-biasing` 定制热词**不是同一套**（不同 Endpoint/鉴权/词表 ID，不可混用）
- AnalyzeImage / GeneralAnalyzeImage
- AnalyzeAudioSync（文档标注不推荐）
- 依赖官方 Tea/`Aliyun.SDK` NuGet（自研 ACS3，避免额外重量级依赖）

## 方案对比

| 方案 | 优点 | 缺点 |
| --- | --- | --- |
| A. 扩展 `IDashScopeClient` | 单一入口 | 鉴权/Endpoint 混杂，破坏现有语义 |
| B. 独立 `IContactCenterAiClient` + ACS3（推荐） | 边界清晰，可单独配置 AK/SK | 需维护签名实现 |
| C. 包装官方 Tea SDK | 签名现成 | 依赖重、风格与本仓库不一致 |

**采用方案 B。**

## 架构

```
ContactCenterAiOptions (AK/SK, Endpoint, Region)
        │
        ▼
IContactCenterAiClient / ContactCenterAiClient
        │  builds HttpRequestMessage + body JSON (camelCase)
        ▼
Acs3Signer (internal) ──► Authorization / x-acs-* headers
        │
        ▼
HttpClient → contactcenterai.cn-shanghai.aliyuncs.com
```

### 公开 API（一期）

1. **AnalyzeConversation**（同步 + SSE）——对应质检 / 字段抽取 / 摘要最佳实践
2. **RunCompletion**（同步 + SSE）——按模板 ID 调用
3. **CreateTask** / **GetTaskResult**——离线异步任务

### 模型约定

- 命名空间：`Cnblogs.DashScope.Core`
- 类型前缀：`ContactCenterAi*` / `Ccai*`（技术标识仍对应产品 ContactCenterAI）
- XML 注释英文；产品显示名写 *LingQue CCAI Conversation Analysis AIO*
- JSON：按 OpenAPI 字段名用 `[JsonPropertyName]`，AnalyzeConversation / CreateTask 为 camelCase；RunCompletion 按官方示例为 PascalCase
- `resultTypes` 常量集中在 `CcaiResultTypes`

### 错误处理

HTTP 非成功时抛出 `ContactCenterAiException`（含 status、requestId、errorCode、errorInfo）。

### DI

在 `Cnblogs.DashScope.AspNetCore` 增加 `AddContactCenterAiClient`，配置节建议 `contactCenterAi`。

## 测试策略

1. **Acs3SignerTests**：固定 AK/SK、日期、nonce、body，断言 CanonicalRequest / Signature / Authorization
2. **AnalyzeConversationSerializationTests**：质检 / 字段 / 摘要三类请求体与响应反序列化（对齐现有 Snapshot + MockHttp 模式）
3. **CreateTask / GetTaskResult / RunCompletion**：请求路径、方法、body/query 断言

## 文档与命名

代码、设计、PR、注释统一使用「伶鹊 CCAI」；不新增「通义晓蜜」字样。
