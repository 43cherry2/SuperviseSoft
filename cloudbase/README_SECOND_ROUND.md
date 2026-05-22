# 第二轮：学习任务、图片上传、AI 分析

## 手动开启能力

1. CloudBase 身份认证继续保持开启。
2. CloudBase 云数据库创建集合：`study_tasks`、`uploaded_files`、`ai_jobs`、`ai_results`、`usage_logs`。
3. CloudBase 云存储保持可用。
4. HTTP 访问服务里新增路由：`createTask`、`getTaskList`、`getTaskDetail`、`updateTaskStatus`、`getUploadInfo`、`uploadFile`、`confirmFileUploaded`、`analyzeHomeworkImage`、`getAiResult`。
5. 如果接真实 AI，在 `analyzeHomeworkImage` 云函数环境变量配置：
   - `AI_API_KEY`
   - `AI_API_URL`
   - `AI_MODEL`
6. 未配置 `AI_API_KEY` 或 `AI_API_URL` 时，云函数会使用 `MockAiProvider` 跑通流程。

## 数据库集合

### study_tasks

字段：`_id`、`userId`、`title`、`description`、`estimatedMinutes`、`actualMinutes`、`status`、`createdAt`、`updatedAt`

说明：
- `userId` 必须来自 Authorization 登录态解析出的 `authUid`。
- `status` 可选：`created`、`running`、`paused`、`finished`、`cancelled`。

### uploaded_files

字段：`_id`、`userId`、`taskId`、`fileName`、`fileType`、`storagePath`、`fileUrl`、`cloudFileId`、`status`、`fileSize`、`createdAt`、`updatedAt`

说明：
- `taskId` 必须属于当前 `userId`。
- `storagePath` 由 `getUploadInfo` 生成，格式：`users/{userId}/tasks/{taskId}/{timestamp}_{fileName}`。
- `status` 可选：`created`、`uploaded`、`deleted`。
- 当前 MVP 额外保存 `cloudFileId`，用于记录 CloudBase 云存储返回的 fileID。

### ai_jobs

字段：`_id`、`userId`、`taskId`、`fileId`、`status`、`model`、`errorMessage`、`createdAt`、`updatedAt`

`status` 可选：`pending`、`processing`、`success`、`failed`。

### ai_results

字段：`_id`、`userId`、`taskId`、`fileId`、`jobId`、`summary`、`estimatedMinutes`、`suggestedSteps`、`rawResponse`、`createdAt`

### usage_logs

字段：`_id`、`userId`、`type`、`taskId`、`fileId`、`jobId`、`model`、`tokenUsage`、`createdAt`

## HTTP 接口示例

所有业务接口都需要：

```http
Authorization: Bearer <access_token>
Content-Type: application/json
```

### POST /createTask

请求：

```json
{
  "title": "数学作业",
  "description": "完成第 3 页练习",
  "estimatedMinutes": 40
}
```

返回：

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "task": {
      "_id": "task_id",
      "userId": "authUid",
      "title": "数学作业",
      "description": "完成第 3 页练习",
      "estimatedMinutes": 40,
      "actualMinutes": 0,
      "status": "created",
      "createdAt": "2026-05-21T00:00:00.000Z",
      "updatedAt": "2026-05-21T00:00:00.000Z"
    }
  }
}
```

### GET /getTaskList

返回当前用户自己的任务：

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "tasks": []
  }
}
```

### GET /getTaskDetail?taskId=task_id

返回任务、已上传文件、AI 任务和最近 AI 结果。

### POST /updateTaskStatus

```json
{
  "taskId": "task_id",
  "status": "finished",
  "actualMinutes": 38
}
```

### POST /getUploadInfo

```json
{
  "taskId": "task_id",
  "fileName": "homework.jpg",
  "fileType": "image/jpeg",
  "fileSize": 123456
}
```

返回：

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "taskId": "task_id",
    "fileName": "homework.jpg",
    "fileType": "image/jpeg",
    "storagePath": "users/authUid/tasks/task_id/1779340800000_homework.jpg",
    "uploadUrl": "/uploadFile",
    "maxBytes": 5242880
  }
}
```

### POST /uploadFile

这是 Unity HTTP 上传适配接口，Body 是 JPG/PNG 字节流，Header：

```http
x-task-id: task_id
x-storage-path: users/authUid/tasks/task_id/1779340800000_homework.jpg
x-file-type: image/jpeg
```

说明：该方式适合 MVP 和小文件。后期可替换成 Unity 客户端直传 CloudBase 云存储。

### POST /confirmFileUploaded

```json
{
  "taskId": "task_id",
  "fileName": "homework.jpg",
  "fileType": "image/jpeg",
  "storagePath": "users/authUid/tasks/task_id/1779340800000_homework.jpg",
  "fileId": "cloud://xxx",
  "fileUrl": "",
  "fileSize": 123456
}
```

返回的 `data.file._id` 是后续 `analyzeHomeworkImage` 使用的 `fileId`。

### POST /analyzeHomeworkImage

```json
{
  "taskId": "task_id",
  "fileId": "uploaded_files_record_id"
}
```

返回：

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "job": {
      "_id": "job_id",
      "status": "success",
      "model": "mock-homework-v1"
    },
    "result": {
      "summary": "已读取作业图片，建议先完成基础题...",
      "estimatedMinutes": 40,
      "suggestedSteps": [
        {
          "title": "完成选择题",
          "minutes": 10,
          "description": "先完成低难度题目，快速建立进度。"
        }
      ]
    }
  }
}
```

### GET /getAiResult?jobId=job_id

返回指定 AI job 的结果。`jobId` 必须属于当前登录用户。

## Unity UI 绑定说明

新增脚本：

- `CreateTaskPanel`：绑定标题、描述、预计分钟数输入框，创建按钮，返回按钮。
- `TaskListPanel`：绑定任务列表文本、任务 ID 输入框、刷新、创建、打开详情按钮。
- `TaskDetailPanel`：绑定详情文本、状态按钮、本地图片路径输入框、上传按钮、AI 分析按钮。
- `AiResultPanel`：绑定 AI 状态、summary、estimatedMinutes、suggestedSteps 文本和关闭按钮。

主页可选绑定：

- 在 `MainEntryPanel.openTaskListButton` 绑定一个“学习任务”按钮。
- 在 `MainEntryPanel.taskListPanel` 绑定任务列表面板。

隐藏面板建议继续使用 `CanvasGroup`，项目里的 `PanelVisibility` 会自动设置 `blocksRaycasts=false`，避免隐藏面板挡住按钮。

## 部署步骤

1. 在 CloudBase 控制台创建新增集合。
2. 确认云存储可用。
3. 如需真实 AI，给 `analyzeHomeworkImage` 配置环境变量 `AI_API_KEY`、`AI_API_URL`、`AI_MODEL`。
4. 在 `cloudbase` 目录执行依赖安装和部署：

```bash
cd cloudbase
tcb fn deploy createTask
tcb fn deploy getTaskList
tcb fn deploy getTaskDetail
tcb fn deploy updateTaskStatus
tcb fn deploy getUploadInfo
tcb fn deploy uploadFile
tcb fn deploy confirmFileUploaded
tcb fn deploy analyzeHomeworkImage
tcb fn deploy getAiResult
```

5. 在 HTTP 访问服务配置对应路径。

## 测试步骤

1. 未登录时调用 `CreateTaskPanel` 创建任务，应提示请先登录。
2. 登录后创建任务，检查 `study_tasks.userId` 是当前 `authUid`。
3. 刷新任务列表，只能看到当前账号任务。
4. 打开任务详情，更新状态为 `running/paused/finished`。
5. 在本地图片路径输入框填入 JPG/PNG 绝对路径，点击上传。
6. 检查 `uploaded_files` 记录从 `created` 变为 `uploaded`。
7. 点击 AI 分析。
8. 检查 `ai_jobs` 为 `success`，`ai_results` 有结果，`usage_logs` 有记录。
9. Unity 应展示 `summary`、`estimatedMinutes`、`suggestedSteps`。
10. 换账号登录后，不应看到另一个账号的任务、文件或 AI 结果。
