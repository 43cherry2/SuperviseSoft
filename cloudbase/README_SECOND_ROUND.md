# 第二轮：一次性学习任务组、图片/文字 AI 分析

## 当前设计

任务不是长期任务库，而是“一次性任务组”：

1. Unity 创建一个本次任务组。
2. 任务详情里可以输入文字让 AI 分析，也可以拖拽 JPG/PNG，或打开相机拍照后自动上传并分析。
3. AI 结果按本次任务组一个一个展示，可上一项/下一项查看。
4. 点击“结束本任务”后，云函数返回统计结果。
5. 统计返回后，服务端删除本次任务组、上传文件记录、AI job、AI result，并尝试删除 CloudBase 云存储文件。
6. `usage_logs` 只保留聚合统计，不保存题目文字、图片路径、AI 明细。

保留任务/文件/AI 明细的旧版本已在 git 提交 `5b30fc0` 中快照，后续需要恢复可以从该提交找回。

## 手动开启能力

1. CloudBase 身份认证继续开启。
2. CloudBase 云数据库集合：`study_tasks`、`uploaded_files`、`ai_jobs`、`ai_results`、`usage_logs`。
3. CloudBase 云存储可用。
4. HTTP 路由：`createTask`、`getTaskList`、`getTaskDetail`、`updateTaskStatus`、`getUploadInfo`、`uploadFile`、`confirmFileUploaded`、`analyzeHomeworkImage`、`analyzeStudyText`、`getAiResult`、`finishTask`。
5. 真实 AI 配置在云函数环境变量里：`AI_API_KEY`、`AI_API_URL`、`AI_MODEL`。
6. 未配置真实 AI 时，云函数使用 MockAiProvider 跑通流程。

## 数据库集合

### study_tasks

字段：`_id`、`userId`、`title`、`description`、`estimatedMinutes`、`actualMinutes`、`status`、`createdAt`、`updatedAt`

说明：`userId` 必须来自 Authorization 登录态。任务结束后该记录会被删除。

### uploaded_files

字段：`_id`、`userId`、`taskId`、`fileName`、`fileType`、`storagePath`、`fileUrl`、`cloudFileId`、`status`、`fileSize`、`createdAt`、`updatedAt`

说明：只作为任务进行中的临时上传记录。`finishTask` 会删除记录，并尝试删除云存储文件。

### ai_jobs

字段：`_id`、`userId`、`taskId`、`fileId`、`inputType`、`status`、`model`、`errorMessage`、`createdAt`、`updatedAt`

`inputType`：`image` 或 `text`。任务结束后删除。

### ai_results

字段：`_id`、`userId`、`taskId`、`fileId`、`jobId`、`inputType`、`inputText`、`fileName`、`summary`、`estimatedMinutes`、`suggestedSteps`、`rawResponse`、`createdAt`

任务结束后删除。文字内容只在任务进行中临时保存。

### usage_logs

字段：`_id`、`userId`、`type`、`taskId`、`fileId`、`jobId`、`model`、`tokenUsage`、`summary`、`createdAt`

`finishTask` 会写入一条 `type=task_finish` 的聚合统计，不保存图片路径、文字原文或 AI 明细。

## 接口

所有业务接口都需要：

```http
Authorization: Bearer <access_token>
Content-Type: application/json
```

### POST /createTask

```json
{
  "title": "数学作业",
  "description": "本次练习",
  "estimatedMinutes": 40
}
```

返回 `data.task`，状态默认为 `running`。

### GET /getTaskList

返回当前用户还没有结束的任务组。

### GET /getTaskDetail?taskId=task_id

返回任务组、已上传图片、AI jobs、AI results。Unity 详情页按 `aiResults` 一项一项展示。

### POST /analyzeStudyText

```json
{
  "taskId": "task_id",
  "text": "完成第 3 页应用题，并整理错题原因"
}
```

返回：

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "job": { "_id": "job_id", "inputType": "text", "status": "success" },
    "result": {
      "_id": "result_id",
      "inputType": "text",
      "summary": "已分析文字任务...",
      "estimatedMinutes": 30,
      "suggestedSteps": []
    }
  }
}
```

### 图片分析流程

Unity 不再要求用户手填路径。

1. 用户拖拽 JPG/PNG 到详情页拖拽区，或打开相机拍照。
2. Unity 读取图片字节。
3. Unity 调用 `getUploadInfo`。
4. Unity 调用 `uploadFile` 上传到 CloudBase 云存储。
5. Unity 调用 `confirmFileUploaded`。
6. Unity 自动调用 `analyzeHomeworkImage`。

### POST /finishTask

```json
{
  "taskId": "task_id",
  "actualMinutes": 38
}
```

返回：

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "summary": {
      "taskId": "task_id",
      "title": "数学作业",
      "itemCount": 3,
      "imageItemCount": 2,
      "textItemCount": 1,
      "estimatedMinutes": 40,
      "aiEstimatedMinutes": 55,
      "actualMinutes": 38,
      "startedAt": "2026-05-21T00:00:00.000Z",
      "finishedAt": "2026-05-21T00:38:00.000Z",
      "durationMinutes": 38
    }
  }
}
```

返回后服务端会清理本次任务明细。

## Unity UI

`TestSessage` 场景里直接包含第二轮 Panel，不靠运行时动态生成：

- `TaskListPanel`：查看进行中的任务组、创建任务组、打开详情。
- `CreateTaskPanel`：创建本次任务组，成功后直接进入详情。
- `TaskDetailPanel`：文字分析、拖拽图片、打开相机、拍照并分析、上一项/下一项、结束本任务。
- `AiResultPanel`：展示单项 AI 结果或结束统计。

拖拽图片说明：当前使用 Unity Editor 的 `UnityEditor.DragAndDrop` 接收外部图片拖拽，适合编辑器调试。打包到桌面运行时如果要继续支持系统级拖拽，需要后续接原生窗口拖拽插件。

拍照说明：使用 `WebCamTexture`，需要设备有可用摄像头，并在平台权限中允许相机访问。

## 部署

```bash
tcb fn deploy createTask
tcb fn deploy getTaskList
tcb fn deploy getTaskDetail
tcb fn deploy updateTaskStatus
tcb fn deploy getUploadInfo
tcb fn deploy uploadFile
tcb fn deploy confirmFileUploaded
tcb fn deploy analyzeHomeworkImage
tcb fn deploy analyzeStudyText
tcb fn deploy getAiResult
tcb fn deploy finishTask
```

同时在 HTTP 访问服务里配置对应路径。

## 测试

1. 登录账号 A。
2. 创建本次任务组。
3. 输入一段文字，点击“分析文字”，确认出现一项 AI 结果。
4. 拖拽一张 JPG/PNG 到拖拽区，确认自动上传并分析。
5. 打开相机，拍照并分析。
6. 用上一项/下一项查看本次任务组内结果。
7. 点击“结束本任务”，确认 Unity 展示统计。
8. 检查 `study_tasks`、`uploaded_files`、`ai_jobs`、`ai_results` 中本次任务明细被删除。
9. 检查 `usage_logs` 有一条 `task_finish` 聚合日志。
