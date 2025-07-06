# SoraEssayJudge API 文档

本文档详细介绍了 SoraEssayJudge 项目的 API 端点。

> **安全说明**：除注册和登录接口外，所有接口均需在请求头中携带有效的 JWT 令牌。
> 
> - 方式：`Authorization: Bearer <token>`
> - 获取方式：通过登录接口获取。

---

## AuthController

用于用户注册与登录。

### POST /api/Auth/register

用户注册（受配置开关控制）。

- **方法**: `POST`
- **路由**: `/api/Auth/register`
- **参数** (FROMDATA)：
  - `username` (必需, `string`): 用户名
  - `password` (必需, `string`): 密码（明文）
  - `name` (可选, `string`): 真实姓名
  - `phoneNumber` (可选, `string`): 手机号
- **示例**:
  ```bash
  curl -X POST -F "username=testuser" -F "password=123456" -F "name=张三" -F "phoneNumber=13800000000" http://localhost:5000/api/Auth/register
  ```

### POST /api/Auth/login

用户登录，返回 JWT 令牌和用户基本信息。

- **方法**: `POST`
- **路由**: `/api/Auth/login`
- **参数** (FORMDATA)：
  - `username` (必需, `string`): 用户名
  - `password` (必需, `string`): 密码（明文）
- **返回**:
  - `token` (`string`): JWT 令牌
  - `name` (`string`): 用户姓名
  - `phoneNumber` (`string`): 手机号
- **示例**:
  ```bash
  curl -X POST -F "username=testuser" -F "password=123456" http://localhost:5000/api/Auth/login
  ```

---

## 1. EssayAssignmentController

用于管理作文题目。

> **注意：以下所有接口均需在请求头中添加 `Authorization: Bearer <token>`**

### GET /EssayAssignment

查询作文题目。

- **方法**: `GET`
- **路由**: `/EssayAssignment`
- **参数**:
  - `top` (可选, `int`): 获取最近的 N 个作文题目。
  - `id` (可选, `guid`): 按 GUID 精确查询一个作文题目。
  - `title` (可选, `string`): 按标题进行模糊查询。
- **示例**:
  ```bash
  # 获取最近的5个作文题目
  curl -X GET "http://localhost:5000/EssayAssignment?top=5"

  # 按标题“环保”进行模糊查询
  curl -X GET "http://localhost:5000/EssayAssignment?title=环保"
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "grade": "高二",
      "totalScore": 60,
      "titleContext": "论科技与人文",
      "createdAt": "2023-10-27T10:00:00Z"
    }
  ]
  ```

### POST /EssayAssignment

新建一个作文题目。

- **方法**: `POST`
- **路由**: `/EssayAssignment`
- **参数** (表单数据):
  - `grade` (必需, `string`): 年级，例如 "高二"。
  - `totalScore` (必需, `int`): 满分，例如 60。
  - `baseScore` (必需, `int`): 基准分，例如 42。
  - `titleContext` (可选, `string`): 题目背景或具体题目。
  - `scoringCriteria` (可选, `string`): 具体的评分标准。
- **示例**:
  ```bash
  curl -X POST -F "grade=高三" -F "totalScore=60" -F "baseScore=42" -F "titleContext=论科技与人文" http://localhost:5000/EssayAssignment
  ```
- **返回示例** (成功时返回创建的作文题目信息):
  ```json
  {
    "id": "...",
    "grade": "
    "totalScore": 60,
    "baseScore": 42,
    "titleContext": "论科技与人文",
    "scoringCriteria": ""
  }
  ```

---

## 2. StudentController

用于管理学生信息。

### GET /Student

查询学生信息。

- **方法**: `GET`
- **路由**: `/Student`
- **参数**:
  - `id` (可选, `guid`): 按 GUID 精确查询一个学生。
  - `name` (可选, `string`): 按姓名进行模糊查询。
  - `classId` (可选, `guid`): 按班级ID查询。
- **示例**:
  ```bash
  # 查询姓名为“张三”的学生
  curl -X GET "http://localhost:5000/Student?name=张三"
  # 查询某班级下所有学生
  curl -X GET "http://localhost:5000/Student?classId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "name": "张三",
      "studentId": "20250001",
      "classId": "..."
    }
  ]
  ```

### POST /Student

新建一个学生。

- **方法**: `POST`
- **路由**: `/Student`
- **参数** (表单数据):
  - `name` (必需, `string`): 学生姓名。
  - `studentId` (必需, `string`): 8位学号。
  - `classId` (必需, `guid`): 班级ID。
- **示例**:
  ```bash
  curl -X POST -F "name=李四" -F "studentId=20250001" -F "classId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" http://localhost:5000/Student
  ```
- **返回示例** (成功时返回创建的学生信息):
  ```json
  {
    "id": "...",
    "name": "李四",
    "studentId": "20250001",
    "classId": "..."
  }
  ```

---

## 3. ClassController

用于管理班级信息及查询班级下的学生。

### GET /Class

查询所有班级及其学生。

- **方法**: `GET`
- **路由**: `/Class`
- **返回**: 班级及其学生列表（学生信息为概要信息，包括ID、学号和姓名）。
- **示例**:
  ```bash
  curl -X GET http://localhost:5000/Class
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "name": "高三1班",
      "createdAt": "2023-01-01T12:00:00Z",
      "students": [
        {
          "id": "...",
          "studentId": "20250001",
          "name": "李四"
        }
      ]
    }
  ]
  ```

### GET /Class/{classId}/students

查询某班级下所有学生。

- **方法**: `GET`
- **路由**: `/Class/{classId}/students`
- **参数**:
  - `classId` (必需, `guid`): 班级ID。
- **示例**:
  ```bash
  curl -X GET http://localhost:5000/Class/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/students
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "studentId": "20250001",
      "name": "李四",
      "createdAt": "2023-01-01T12:00:00Z",
      "classId": "..."
    }
  ]
  ```

### POST /Class

新建一个班级。

- **方法**: `POST`
- **路由**: `/Class`
- **参数** (表单数据):
  - `name` (必需, `string`): 班级名称。
- **示例**:
  ```bash
  curl -X POST -F "name=高三1班" http://localhost:5000/Class
  ```
- **返回示例** (成功时返回创建的班级信息):
  ```json
  {
    "id": "...",
    "name": "高三1班"
  }
  ```

---

## 4. EssaySubmissionController

用于管理作文的提交与查询。

### GET /EssaySubmission/summary

查询某个学生最近的 N 条作文概要。

- **方法**: `GET`
- **路由**: `/EssaySubmission/summary`
- **参数**:
  - `top` (必需, `int`): 获取最近的 N 条记录。
  - `studentId` (可选, `guid`): 学生的 GUID。**注意：必须提供 studentId 或 studentName 中的一个。**
  - `studentName` (可选, `string`): 学生的 姓名 (如果未提供 `studentId`)。**注意：必须提供 studentId 或 studentName 中的一个。**
- **示例**:
  ```bash
  # 获取指定学生ID的最近5篇作文概要
  curl -X GET "http://localhost:5000/EssaySubmission/summary?top=5&studentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  # 获取指定学生姓名的最近5篇作文概要
  curl -X GET "http://localhost:5000/EssaySubmission/summary?top=5&studentName=李四"
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "titleContext": "论科技与人文",
      "finalScore": 55,
      "isError": false,
      "createdAt": "2023-10-27T10:00:00Z"
    }
  ]
  ```

### GET /EssaySubmission/{id}

查询单篇作文的完整详情。

- **方法**: `GET`
- **路由**: `/EssaySubmission/{id}`
- **参数**:
  - `id` (必需, `guid`): 作文提交记录的 GUID。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/EssaySubmission/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  ```
- **返回示例**:
  - **批阅进行中 (仅有解析文本)**:
    ```json
    {
      "status": "Judging is in progress.",
      "parsedText": "..."
    }
    ```
  - **批阅进行中 (已有AI结果)**:
    ```json
    {
      "status": "Judging is in progress.",
      "parsedText": "...",
      "aiResults": [
        {
          // AIResult 对象的结构
        }
      ]
    }
    ```
  - **批阅完成或出错**:
    ```json
    {
      "id": "...",
      "essayAssignment": {
        "id": "...",
        "titleContext": "论科技与人文",
        "totalScore": 60
      },
      "student": {
        "id": "...",
        "name": "李四"
      },
      "finalScore": 55,
      "comments": "...", // 如果有评论
      "submissionDate": "2023-10-27T10:00:00Z",
      "imageUrl": "...",
      "columnCount": 3,
      "parsedText": "...",
      "isError": false,
      "errorMessage": null, // 如果 isError 为 true，这里会有错误信息
      "aiResults": [
        {
          // AIResult 对象的结构
        }
      ]
    }
    ```

### POST /EssaySubmission

提交一篇作文进行批阅。

- **方法**: `POST`
- **路由**: `/EssaySubmission`
- **参数** (表单数据):
  - `essayAssignmentId` (必需, `guid`): 对应的作文题目 GUID。
  - `imageFile` (必需, `file`): 上传的作文图片文件。
  - `columnCount` (必需, `int`): 图片中的栏数 (2 或 3)。
- **示例**:
  ```bash
  curl -X POST -F "essayAssignmentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -F "imageFile=@/path/to/your/essay.png" -F "columnCount=3" http://localhost:5000/EssaySubmission
  ```
- **返回示例** (成功时返回提交记录的ID):
  ```json
  {
    "submissionId": "..."
  }
  ```

### PUT /EssaySubmission/{id}

更新一篇已存在的作文提交记录。

- **方法**: `PUT`
- **路由**: `/EssaySubmission/{id}`
- **参数**:
  - `id` (必需, `guid`): 要更新的作文提交记录的 GUID。
- **请求体** (JSON):
  ```json
  {
    "studentId": "...", // 可选，学生的 GUID
    "finalScore": 55 // 可选，最终分数
  }
  ```
- **示例**:
  ```bash
  curl -X PUT -H "Content-Type: application/json" -d '{"studentId":"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", "finalScore":60}' http://localhost:5000/EssaySubmission/yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
  ```
- **返回示例** (成功时返回更新后的提交记录详情):
  ```json
  {
    "id": "...",
    "essayAssignment": { ... }, // 完整的 EssayAssignment 对象
    "student": { ... }, // 完整的 Student 对象
    "finalScore": 60,
    "comments": "...",
    "submissionDate": "...",
    "imageUrl": "...",
    "columnCount": 3,
    "parsedText": "...",
    "isError": false,
    "errorMessage": null,
    "aiResults": [ ... ] // AIResult 对象列表
  }
  ```

---

## 5. EssaySubmissionSearchController

用于按标题模糊查询作文，返回作文、学生姓名/ID、分

### GET /EssaySubmissionSearch

- **方法**: `GET`
- **路由**: `/EssaySubmissionSearch`
- **参数**:
  - `title` (可选, `string`): 作文标题关键字，支持模糊匹配。不传则返回最新前N条。
  - `top` (可选, `int`): 返回前N条，默认10。
- **返回**: 作文ID、标题、创建时间、学生ID、学生姓名、最终分数。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/EssaySubmissionSearch?title=科技&top=5"
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "title": "论科技与人文",
      "createdAt": "2023-10-27T10:00:00Z",
      "studentId": "...",
      "studentName": "李四",
      "finalScore": 55
    }
  ]
  ```

---

## 6. ApiKeyController

用于管理 API 密钥。

### GET /ApiKey

获取所有 API 密钥列表。

**返回:** `200 OK`

```json
[
  {
    "id": "...",  // GUID
    "key": "...",
    "serviceType": "...",
    "secret": "...",
    "endpoint": "...",
    "description": "...",
    "isEnabled": true,
    "isDeleted": false,
    "createdAt": "2023-01-01T12:00:00Z",
    "updatedAt": "2023-01-01T12:00:00Z"
  }
]
```

### GET /ApiKey/{id}

根据 ID 获取指定的 API 密钥。

**参数:**
- `id` (必需, `guid`): API 密钥的 ID。

**返回:** `200 OK`

```json
{
  "id": "...",  // GUID
  "key": "...",
  "serviceType": "...", //OpenAI or Aliyun
  "secret": "...",
  "endpoint": "...",
  "description": "...",
  "isEnabled": true,
  "isDeleted": false,
  "createdAt": "2023-01-01T12:00:00Z",
  "updatedAt": "2023-01-01T12:00:00Z"
}
```

### POST /ApiKey

创建一个新的 API 密钥。

**请求体 (表单数据):**

- `serviceType` (必需, `string`): 服务类型
- `key` (必需, `string`): 密钥
- `secret` (可选, `string`): 密钥暗文
- `endpoint` (可选, `string`): 端点
- `description` (可选, `string`): 描述

**返回:** `201 Created`

```json
{
  "id": "...",  // GUID
  "key": "...",
  "serviceType": "...",
  "secret": "...",
  "endpoint": "...",
  "description": "...",
  "isEnabled": true,
  "isDeleted": false,
  "createdAt": "2023-01-01T12:00:00Z",
  "updatedAt": "2023-01-01T12:00:00Z"
}
```

### PUT /ApiKey/{id}

更新一个已存在的 API 密钥。

**参数:**
- `id` (必需, `guid`): 要更新的 API 密钥的 ID。

**请求体 (JSON):**

```json
{
  "id": "...",  // GUID (必须与路由中的 ID 匹配)
  "serviceType": "...", // 可选
  "key": "...", // 可选
  "secret": "...", // 可选
  "endpoint": "...", // 可选
  "description": "...", // 可选
  "isEnabled": true, // 可选
  "isDeleted": false // 可选
  // createdAt 和 updatedAt 字段通常不需要在请求体中提供
}
```

**返回:** `204 No Content` (成功时)

**示例:**
```bash
curl -X PUT -H "Content-Type: application/json" -d '{"id":"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", "key":"new_key_value", "isEnabled":false}' http://localhost:5000/api/ApiKey/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```


### DELETE /ApiKey/{id}

删除一个 API 密钥。

**参数:**
- `id` (必需, `guid`): API 密钥的 ID。

**返回:** `204 No Content` (成功时)

**示例:**
```bash
curl -X DELETE http://localhost:5000/api/ApiKey/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

---

## 附录：错误码

| 错误码 | 描述 |
| ------ | ---- |
| 400    | 请求参数错误 |
| 401    | 未授权，JWT 令牌无效或已过期 |
| 403    | 禁止访问，权限不足 |
| 404    | 资源未找到 |
| 500    | 服务器内部错误 |