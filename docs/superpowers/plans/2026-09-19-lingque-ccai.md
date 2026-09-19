# 伶鹊 CCAI-对话分析 AIO Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add LingQue CCAI Conversation Analysis AIO client (`AnalyzeConversation`, `RunCompletion`, `CreateTask`, `GetTaskResult`) with ACS3 signing and unit tests.

**Architecture:** Independent `IContactCenterAiClient` with AccessKey ACS3-HMAC-SHA256 signing, separate from DashScope Bearer auth.

**Tech Stack:** .NET 6/8/10, System.Text.Json, xUnit, NSubstitute

## Global Constraints

- Product name in docs/comments: LingQue CCAI Conversation Analysis AIO (never 通义晓蜜)
- XML comments in English
- Follow existing SDK style (records/classes, DashScopeException-like errors)
- No official Aliyun Tea SDK dependency

---

### Task 1: Core models + ACS3 signer + client

- [ ] Add ContactCenterAi defaults, options, API links, ACS3 signer
- [ ] Add request/response models and result type constants
- [ ] Add `IContactCenterAiClient` / `ContactCenterAiClient`
- [ ] Add AspNetCore DI registration
- [ ] Unit tests for signer + AnalyzeConversation best-practice scenarios
- [ ] Commit and push
