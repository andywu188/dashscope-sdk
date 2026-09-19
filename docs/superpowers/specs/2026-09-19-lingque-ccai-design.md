# 伶鹊 CCAI-对话分析 AIO 接入设计

> 产品正式名称统一为 **伶鹊 CCAI-对话分析 AIO**（旧称「通义晓蜜 CCAI-对话分析 AIO」仅作文档对照，代码与注释一律使用新名称）。

## 背景

阿里云 ContactCenterAI (`2024-06-03`) 提供对话分析能力，与现有 DashScope Bearer API Key 调用不同：

- Endpoint：`contactcenterai.cn-shanghai.aliyuncs.com`（公网上海；已用真实 AK/SK 联调通过）
- 鉴权：AccessKey + ACS3-HMAC-SHA256（ROA），**不能**用 DashScope `sk-` API Key
- 路径形如：`/{workspaceId}/ccai/app/{appId}/analyze_conversation`

**易混淆点（联调结论）：** 百炼业务空间控制台里的「API Host」（形如 `{workspaceId}.cn-beijing.maas.aliyuncs.com`）是 DashScope/MaaS 应用调用入口，**不是**伶鹊 CCAI ROA Endpoint。对该 Host 请求 `/ccai/...` 会 404；`/api/v1/apps/{appId}/completion` 属于百炼应用协议。CCAI 请始终使用 `contactcenterai.cn-shanghai.aliyuncs.com` + AK/SK。

官方最佳实践中的服务质检、字段抽取、摘要/标题/关键词，均通过 `AnalyzeConversation` 的 `resultTypes` 完成。

## 目标

在本 SDK 中新增独立的 ContactCenterAI 客户端，覆盖对话分析主链路，并按现有单元测试风格提供序列化与签名回归测试。

## 非目标（本期不做）

- AnalyzeAudioSync（文档标注不推荐）
- 百炼 ASR / `speech-biasing` 定制热词（与 CCAI Vocab **不是同一套**，`vocabularyId` 不可混用）
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
3. **RunCompletionMessage**（同步 + SSE）——原生 Prompt / Message 协议调用
4. **AnalyzeImage** / **GeneralAnalyzeImage**（同步 + SSE）——图片水印检测与通用图片分析
5. **CreateTask** / **GetTaskResult**——离线异步任务
6. **热词管理**（Create/Update/List/Delete/GetVocab）——伶鹊 CCAI 专用；创建得到的 `vocabularyId` 可传给 `CreateTask.transcription.vocabularyId`

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
4. **Vocab CRUD**：Create/Update/List/Get/Delete 路径、body 与响应反序列化

## 文档与命名

代码、设计、PR、注释统一使用「伶鹊 CCAI」；不新增「通义晓蜜」字样。
