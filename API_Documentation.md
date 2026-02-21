# SoraEssayJudge API 文档

## 目录
- [AuthController](#authcontroller)
- [EssayAssignmentController](#1-essayassignmentcontroller)
- [StudentController](#2-studentcontroller)
- [ClassController](#3-classcontroller)
- [EssaySubmissionController](#4-essaysubmissioncontroller)
- [EssaySubmissionSearchController](#5-essaysubmissionsearchcontroller)
- [StudentUploadController](#6-studentuploadcontroller)
- [ExportController](#7-exportcontroller)
- [EssayFileController](#8-essayfilecontroller)
- [StatusController](#9-statuscontroller)
- [ApiKeyController](#10-apikeycontroller)
- [附录：错误码](#附录错误码)

本文档详细介绍了 SoraEssayJudge 项目的 API 端点。

> **安全说明**：除注册、登录和部分公开接口外，所有接口均需在请求头中携带有效的 JWT 令牌。
> 
> - 方式：`Authorization: Bearer <token>`
> - 获取方式：通过登录接口获取。
>
> **无需授权的接口**：
> - `POST /api/Auth/register` - 用户注册
> - `POST /api/Auth/login` - 用户登录
> - `POST /api/Auth/dingtalk-login` - 钉钉扫码登录
> - `POST /api/Auth/dingtalk-sso-login` - 钉钉单点登录
> - `GET /EssayFile/{fileName}` - 获取作文图片文件
>
> **角色权限说明**：
> - **角色 0（管理员）**：拥有所有接口的访问权限，包括 ApiKeyController 的所有接口
> - **其他角色**：拥有大部分接口的访问权限，但无法访问 ApiKeyController 的接口

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

### POST /api/Auth/dingtalk-login

钉钉扫码登录。

- **方法**: `POST`
- **路由**: `/api/Auth/dingtalk-login`
- **参数** (FORMDATA)：
  - `Code` (必需, `string`): 钉钉授权码
- **返回**: JWT 令牌和用户信息
- **示例**:
  ```bash
  curl -X POST -F "Code=xxx" http://localhost:5000/api/Auth/dingtalk-login
  ```

### POST /api/Auth/dingtalk-sso-login

钉钉单点登录。

- **方法**: `POST`
- **路由**: `/api/Auth/dingtalk-sso-login`
- **参数** (FORMDATA)：
  - `Code` (必需, `string`): 钉钉授权码
- **返回**: JWT 令牌和用户信息
- **示例**:
  ```bash
  curl -X POST -F "Code=xxx" http://localhost:5000/api/Auth/dingtalk-sso-login
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

  # 按标题"环保"进行模糊查询
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

### GET /EssayAssignment/{id}

根据ID查询单个作文题目。

- **方法**: `GET`
- **路由**: `/EssayAssignment/{id}`
- **参数**:
  - `id` (必需, `guid`): 作文题目的 GUID。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/EssayAssignment/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  ```
- **返回示例**:
  ```json
  {
    "id": "...",
    "grade": "高二",
    "totalScore": 60,
    "baseScore": 42,
    "titleContext": "论科技与人文",
    "description": "...",
    "scoringCriteria": "...",
    "createdAt": "2023-10-27T10:00:00Z"
  }
  ```

### GET /EssayAssignment/{id}/status

查询指定测验在指定班级的学生完成情况。

- **方法**: `GET`
- **路由**: `/EssayAssignment/{id}/status`
- **参数**:
  - `id` (必需, `guid`): 作文题目的 GUID。
  - `classId` (必需, `guid`): 班级的 GUID。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/EssayAssignment/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/status?classId=yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy"
  ```
- **返回示例**:
  ```json
  {
    "totalStudentCount": 45,
    "completedCount": 30,
    "pendingCount": 15,
    "completedSubmissions": [
      {
        "student": {
          "id": "...",
          "studentId": "20250001",
          "name": "张三",
          "classId": "...",
          "createdAt": "2023-01-01T12:00:00Z",
          "class": { ... }
        },
        "submission": {
          "id": "...",
          "titleContext": "论科技与人文",
          "finalScore": 55,
          "isError": false,
          "createdAt": "2023-10-27T10:00:00Z"
        }
      }
    ],
    "pendingStudents": [
      {
        "id": "...",
        "studentId": "20250002",
        "name": "李四",
        "classId": "...",
        "createdAt": "2023-01-01T12:00:00Z",
        "class": { ... }
      }
    ]
  }
  ```

### POST /EssayAssignment

新建一个作文题目。

- **方法**: `POST`
- **路由**: `/EssayAssignment`
- **参数** (表单数据):
  - `grade` (必需, `string`): 年级，例如 "高二"。
  - `totalScore` (必需, `int`): 满分，例如 60。
  - `baseScore` (必需, `int`): 基准分，例如 42。
  - `description` (可选, `string`): 题目描述。
  - `titleContext` (可选, `string`): 题目背景或具体题目。
  - `scoringCriteria` (可选, `string`): 具体的评分标准。
- **示例**:
  ```bash
  curl -X POST -F "grade=高三" -F "totalScore=60" -F "baseScore=42" -F "titleContext=论科技与人文" http://localhost:5000/EssayAssignment
  ```
- **返回示例** (成功时返回创建的作文题目ID):
  ```json
  {
    "assignmentId": "..."
  }
  ```

### PUT /EssayAssignment

更新一个作文题目。

- **方法**: `PUT`
- **路由**: `/EssayAssignment`
- **请求体** (JSON):
  ```json
  {
    "id": "...",  // 必需
    "grade": "高三",
    "totalScore": 60,
    "baseScore": 42,
    "description": "...",
    "titleContext": "论科技与人文",
    "scoringCriteria": "..."
  }
  ```
- **示例**:
  ```bash
  curl -X PUT -H "Content-Type: application/json" -d '{"id":"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", "grade":"高三", "totalScore":60, "baseScore":42}' http://localhost:5000/EssayAssignment
  ```
- **返回**: `204 No Content`

### DELETE /EssayAssignment/{id}

删除一个作文题目。

- **方法**: `DELETE`
- **路由**: `/EssayAssignment/{id}`
- **参数**:
  - `id` (必需, `guid`): 要删除的作文题目 ID。
- **示例**:
  ```bash
  curl -X DELETE http://localhost:5000/EssayAssignment/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```
- **返回**: `204 No Content`

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
  # 查询姓名为"张三"的学生
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

### PUT /Student/{id}

更新学生信息。

- **方法**: `PUT`
- **路由**: `/Student/{id}`
- **参数**:
  - `id` (必需, `guid`): 学生ID。
- **请求体** (JSON):
  ```json
  {
    "name": "李四",
    "studentId": "20250001",
    "classId": "..."
  }
  ```
- **示例**:
  ```bash
  curl -X PUT -H "Content-Type: application/json" -d '{"name":"张三", "studentId":"20250002"}' http://localhost:5000/Student/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```
- **返回**: `204 No Content`

### DELETE /Student/{id}

删除学生。

- **方法**: `DELETE`
- **路由**: `/Student/{id}`
- **参数**:
  - `id` (必需, `guid`): 学生ID。
- **示例**:
  ```bash
  curl -X DELETE http://localhost:5000/Student/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```
- **返回**: `204 No Content`

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

### GET /Class/{classId}

根据ID查询班级详情。

- **方法**: `GET`
- **路由**: `/Class/{classId}`
- **参数**:
  - `classId` (必需, `guid`): 班级ID。
- **示例**:
  ```bash
  curl -X GET http://localhost:5000/Class/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```
- **返回示例**:
  ```json
  {
    "id": "...",
    "name": "高三1班",
    "createdAt": "2023-01-01T12:00:00Z"
  }
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

### DELETE /Class/{id}

删除班级。

- **方法**: `DELETE`
- **路由**: `/Class/{id}`
- **参数**:
  - `id` (必需, `guid`): 班级ID。
- **示例**:
  ```bash
  curl -X DELETE http://localhost:5000/Class/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```
- **返回**: `204 No Content`

---

## 4. EssaySubmissionController

用于管理作文的提交与查询。

> **注意：以下所有接口均需在请求头中添加 `Authorization: Bearer <token>`**

### GET /EssaySubmission/summary

查询某个学生最近的 N 条作文概要。

- **方法**: `GET`
- **路由**: `/EssaySubmission/summary`
- **参数**:
  - `top` (可选, `int`): 获取最近的 N 条记录。
  - `studentId` (必需, `guid`): 学生的 GUID。
- **示例**:
  ```bash
  # 获取指定学生ID的最近5篇作文概要
  curl -X GET "http://localhost:5000/EssaySubmission/summary?top=5&studentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
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
  - `enableV3` (可选, `bool`): 是否启用V3批阅模式，默认为 false。
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

### POST /EssaySubmission/batch

批量提交作文进行批阅。

- **方法**: `POST`
- **路由**: `/EssaySubmission/batch`
- **参数** (表单数据):
  - `essayAssignmentId` (必需, `guid`): 对应的作文题目 GUID。
  - `imageFiles` (必需, `file[]`): 上传的作文图片文件数组。
  - `columnCount` (必需, `int`): 图片中的栏数 (2 或 3)。
  - `enableV3` (可选, `bool`): 是否启用V3批阅模式，默认为 false。
- **示例**:
  ```bash
  curl -X POST -F "essayAssignmentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -F "imageFiles=@/path/to/essay1.png" -F "imageFiles=@/path/to/essay2.png" -F "columnCount=3" http://localhost:5000/EssaySubmission/batch
  ```
- **返回示例** (成功时返回提交记录的ID列表):
  ```json
  {
    "submissionIds": ["...", "..."]
  }
  ```

### POST /EssaySubmission/V2

提交一篇作文进行批阅（V2版本，使用新的图片预处理和识别服务）。

- **方法**: `POST`
- **路由**: `/EssaySubmission/V2`
- **参数** (表单数据):
  - `essayAssignmentId` (必需, `guid`): 对应的作文题目 GUID。
  - `imageFile` (必需, `file`): 上传的作文图片文件。
- **示例**:
  ```bash
  curl -X POST -F "essayAssignmentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -F "imageFile=@/path/to/your/essay.png" http://localhost:5000/EssaySubmission/V2
  ```
- **返回示例** (成功时返回提交记录的ID):
  ```json
  {
    "submissionId": "..."
  }
  ```

### PATCH /EssaySubmission/{id}/rejudge

重新批阅一篇作文。

- **方法**: `PATCH`
- **路由**: `/EssaySubmission/{id}/rejudge`
- **参数**:
  - `id` (必需, `guid`): 要重新批阅的作文提交记录的 GUID。
- **示例**:
  ```bash
  curl -X PATCH http://localhost:5000/EssaySubmission/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/rejudge
  ```
- **返回示例**:
  ```json
  {
    "message": "Re-judging process started successfully."
  }
  ```

### PUT /EssaySubmission/{id}

更新一篇已存在的作文提交记录。

- **方法**: `PUT`
- **路由**: `/EssaySubmission/{id}`
- **参数**:
  - `id` (必需, `guid`): 要更新的作文提交记录的 GUID。
- **请求体** (表单数据):
  - `studentId` (可选, `guid`): 学生的 GUID。
  - `score` (可选, `int`): 分数。
  - `title` (可选, `string`): 标题。
  - `parsedText` (可选, `string`): 解析文本。
- **示例**:
  ```bash
  curl -X PUT -F "studentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -F "score=60" http://localhost:5000/EssaySubmission/yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
  ```
- **返回示例** (成功时返回更新后的提交记录详情):
  ```json
  {
    "id": "...",
    "essayAssignment": { ... },
    "student": { ... },
    "finalScore": 60,
    "comments": "...",
    "submissionDate": "...",
    "imageUrl": "...",
    "columnCount": 3,
    "parsedText": "...",
    "isError": false,
    "errorMessage": null,
    "aiResults": [ ... ]
  }
  ```

### DELETE /EssaySubmission

删除一篇作文提交记录。

- **方法**: `DELETE`
- **路由**: `/EssaySubmission`
- **参数**:
  - `id` (必需, `guid`): 要删除的作文提交记录的 GUID。
- **示例**:
  ```bash
  curl -X DELETE "http://localhost:5000/EssaySubmission?id=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  ```
- **返回**: `204 No Content`

---

## 5. EssaySubmissionSearchController

用于按标题模糊查询作文，返回作文、学生姓名/ID、分数。

### GET /EssaySubmissionSearch

- **方法**: `GET`
- **路由**: `/EssaySubmissionSearch`
- **参数**:
  - `assignmentId` (可选, `guid`): 作文题目ID。
  - `title` (可选, `string`): 作文标题关键字，支持模糊匹配。
  - `isError` (可选, `bool`): 是否只查询有错误的记录。
- **返回**: 作文ID、标题、创建时间、学生ID、学生姓名、最终分数。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/EssaySubmissionSearch?title=科技"
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

### GET /EssaySubmissionSearch/unsubmitted-students

查询指定测验未提交的学生列表。

- **方法**: `GET`
- **路由**: `/EssaySubmissionSearch/unsubmitted-students`
- **参数**:
  - `assignmentId` (必需, `guid`): 作文题目ID。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/EssaySubmissionSearch/unsubmitted-students?assignmentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "studentId": "20250001",
      "name": "张三",
      "classId": "...",
      "createdAt": "2023-01-01T12:00:00Z"
    }
  ]
  ```

---

## 6. StudentUploadController

学生端上传接口，用于查询、检查图片和提交作文。

### GET /essay/studentupload/query/essays/{stuId}

查询指定学生的所有作文提交记录。

- **方法**: `GET`
- **路由**: `/essay/studentupload/query/essays/{stuId}`
- **参数**:
  - `stuId` (必需, `string`): 学生学号（8位）。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/essay/studentupload/query/essays/20250001"
  ```
- **返回示例**:
  ```json
  {
    "student": {
      "id": "...",
      "name": "张三",
      "studentId": "20250001"
    },
    "submissions": [
      {
        "id": "...",
        "essayAssignmentId": "...",
        "title": "论科技与人文",
        "isError": false,
        "score": 0,
        "finalScore": 55,
        "createdAt": "2023-10-27T10:00:00Z"
      }
    ]
  }
  ```

### GET /essay/studentupload/query/{shortId}

根据短ID查询作文提交记录（GUID的最后8位）。

- **方法**: `GET`
- **路由**: `/essay/studentupload/query/{shortId}`
- **参数**:
  - `shortId` (必需, `string`): 提交记录GUID的最后8位字符。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/essay/studentupload/query/xxxxxxxx"
  ```
- **返回**: 完整的作文提交记录。

### GET /essay/studentupload/studentinfo

获取所有班级和学生信息。

- **方法**: `GET`
- **路由**: `/essay/studentupload/studentinfo`
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/essay/studentupload/studentinfo"
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

### GET /essay/studentupload/assignments/{studentId}

获取指定学生未完成的作业列表。

- **方法**: `GET`
- **路由**: `/essay/studentupload/assignments/{studentId}`
- **参数**:
  - `studentId` (必需, `string`): 学生学号（8位）。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/essay/studentupload/assignments/20250001"
  ```
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "grade": "高二",
      "totalScore": 60,
      "baseScore": 42,
      "description": "...",
      "titleContext": "论科技与人文",
      "scoringCriteria": "...",
      "createdAt": "2023-10-27T10:00:00Z"
    }
  ]
  ```

### POST /essay/studentupload/checkimg

检查并处理图片。

- **方法**: `POST`
- **路由**: `/essay/studentupload/checkimg`
- **参数** (表单数据):
  - `file` (必需, `file`): 上传的图片文件。
- **示例**:
  ```bash
  curl -X POST -F "file=@/path/to/your/image.png" http://localhost:5000/essay/studentupload/checkimg
  ```
- **返回示例**:
  ```json
  {
    "success": true,
    "processedImageUrl": "/essayfiles/xxx_processed.webp",
    "message": "图片处理成功"
  }
  ```

### POST /essay/studentupload/checkimg/V2

检查并处理图片（V2版本）。

- **方法**: `POST`
- **路由**: `/essay/studentupload/checkimg/V2`
- **参数** (表单数据):
  - `file` (必需, `file`): 上传的图片文件。
- **示例**:
  ```bash
  curl -X POST -F "file=@/path/to/your/image.png" http://localhost:5000/essay/studentupload/checkimg/V2
  ```
- **返回示例**:
  ```json
  {
    "success": true,
    "processedImageUrl": "/essayfiles/xxx_processed.webp",
    "message": "图片处理成功"
  }
  ```

### POST /essay/studentupload/checkimg/columns

检查、拼接并处理多栏图片。

- **方法**: `POST`
- **路由**: `/essay/studentupload/checkimg/columns`
- **参数** (表单数据):
  - `files` (必需, `file[]`): 上传的图片文件数组。
- **示例**:
  ```bash
  curl -X POST -F "files=@/path/to/col1.png" -F "files=@/path/to/col2.png" -F "files=@/path/to/col3.png" http://localhost:5000/essay/studentupload/checkimg/columns
  ```
- **返回示例**:
  ```json
  {
    "success": true,
    "processedImageUrl": "/essayfiles/xxx_stitched_processed.webp",
    "message": "图片拼接和处理成功"
  }
  ```

### POST /essay/studentupload/submit

提交作文。

- **方法**: `POST`
- **路由**: `/essay/studentupload/submit`
- **参数** (表单数据):
  - `studentId` (必需, `string`): 学生学号（8位）。
  - `essayAssignmentId` (必需, `guid`): 作文题目ID。
  - `processedImageUrl` (必需, `string`): 处理后的图片URL。
  - `columnCount` (必需, `int`): 栏数（2或3）。
- **示例**:
  ```bash
  curl -X POST -F "studentId=20250001" -F "essayAssignmentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -F "processedImageUrl=/essayfiles/xxx_processed.webp" -F "columnCount=3" http://localhost:5000/essay/studentupload/submit
  ```
- **返回示例**:
  ```json
  {
    "id": "..."
  }
  ```

### POST /essay/studentupload/submit/hasprased

提交已解析文本的作文。

- **方法**: `POST`
- **路由**: `/essay/studentupload/submit/hasprased`
- **参数** (表单数据):
  - `studentId` (必需, `string`): 学生学号（8位）。
  - `essayAssignmentId` (必需, `guid`): 作文题目ID。
  - `prasedText` (必需, `string`): 解析后的文本。
- **示例**:
  ```bash
  curl -X POST -F "studentId=20250001" -F "essayAssignmentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" -F "prasedText=这里是作文文本内容" http://localhost:5000/essay/studentupload/submit/hasprased
  ```
- **返回示例**:
  ```json
  {
    "id": "..."
  }
  ```

---

## 7. ExportController

用于导出作文数据为Excel文件。

> **注意：以下所有接口均需在请求头中添加 `Authorization: Bearer <token>`**

### POST /Export/essays

导出作文提交记录为Excel文件。

- **方法**: `POST`
- **路由**: `/Export/essays`
- **请求体** (JSON):
  ```json
  {
    "essayAssignmentId": "...",  // 可选，单个测验ID
    "essayAssignmentIds": ["...", "..."],  // 可选，多个测验ID
    "classId": "...",  // 可选，班级ID
    "startDate": "2023-01-01T00:00:00Z",  // 可选，开始日期
    "endDate": "2023-12-31T23:59:59Z"  // 可选，结束日期
  }
  ```
- **示例**:
  ```bash
  curl -X POST -H "Content-Type: application/json" -d '{"essayAssignmentId":"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"}' http://localhost:5000/Export/essays --output essay_report.xlsx
  ```
- **返回**: Excel文件流（`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`）

### GET /Export/essays

导出作文提交记录为Excel文件（GET方式）。

- **方法**: `GET`
- **路由**: `/Export/essays`
- **参数**:
  - `essayAssignmentId` (可选, `guid`): 单个测验ID。
  - `essayAssignmentIds` (可选, `string`): 多个测验ID，逗号分隔。
  - `classId` (可选, `guid`): 班级ID。
  - `startDate` (可选, `datetime`): 开始日期。
  - `endDate` (可选, `datetime`): 结束日期。
- **示例**:
  ```bash
  # 导出单个测验
  curl -X GET "http://localhost:5000/Export/essays?essayAssignmentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" --output essay_report.xlsx
  
  # 导出多个测验
  curl -X GET "http://localhost:5000/Export/essays?essayAssignmentIds=xxx,yyy,zzz" --output essay_report.xlsx
  
  # 导出班级数据
  curl -X GET "http://localhost:5000/Export/essays?classId=yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy" --output essay_report.xlsx
  ```
- **返回**: Excel文件流（`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`）

---

## 8. EssayFileController

用于获取作文图片文件。

> **注意：此接口不需要授权**

### GET /EssayFile/{fileName}

获取作文图片文件。

- **方法**: `GET`
- **路由**: `/EssayFile/{fileName}`
- **参数**:
  - `fileName` (必需, `string`): 文件名。
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/EssayFile/xxx_processed.webp" --output image.png
  ```
- **返回**: 图片文件（`image/jpeg`）

---

## 9. StatusController

用于获取系统状态信息。

> **注意：此接口需要授权**

### GET /api/Status

获取系统状态信息。

- **方法**: `GET`
- **路由**: `/api/Status`
- **示例**:
  ```bash
  curl -X GET "http://localhost:5000/api/Status"
  ```
- **返回示例**:
  ```json
  {
    "serverStatus": "Running",
    "serverTimeUtc": "2023-10-27T10:00:00Z",
    "uptime": "5.12:30:45",
    "build": {
      "version": "1.0.0",
      "gitCommit": "a1b2c3d"
    },
    "application": {
      "environment": "Production",
      "framework": ".NET 6.0.0",
      "processId": 12345,
      "memoryUsage": "256.50 MB",
      "totalAllocatedMemory": "128.25 MB",
      "threadCount": 8
    },
    "system": {
      "hostName": "SERVER01",
      "serverIpAddresses": "192.168.1.100, 192.168.1.101",
      "os": "Microsoft Windows 10.0.19041",
      "osArchitecture": "X64",
      "processorCount": 8
    },
    "request": {
      "clientIp": "192.168.1.200"
    },
    "databaseStatus": "Connected"
  }
  ```

---

## 10. ApiKeyController

用于管理 API 密钥和 AI 模型配置。

> **注意：以下所有接口均需在请求头中添加 `Authorization: Bearer <token>`**
> 
> **权限说明**：此控制器的所有接口仅限角色为 0（管理员）的用户访问。

### GET /api/ApiKey

获取所有 API 密钥列表。

- **方法**: `GET`
- **路由**: `/api/ApiKey`
- **返回**: `200 OK`
- **返回示例**:
  ```json
  [
    {
      "id": "...",
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

### GET /api/ApiKey/{id}

根据 ID 获取指定的 API 密钥。

- **方法**: `GET`
- **路由**: `/api/ApiKey/{id}`
- **参数**:
  - `id` (必需, `guid`): API 密钥的 ID。
- **返回**: `200 OK`
- **返回示例**:
  ```json
  {
    "id": "...",
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

### POST /api/ApiKey

创建一个新的 API 密钥。

- **方法**: `POST`
- **路由**: `/api/ApiKey`
- **参数** (表单数据):
  - `serviceType` (必需, `string`): 服务类型（如 OpenAI、Aliyun）
  - `key` (必需, `string`): 密钥
  - `secret` (可选, `string`): 密钥暗文
  - `endpoint` (可选, `string`): 端点
  - `description` (可选, `string`): 描述
  - `modelIds` (可选, `string`): 关联的模型ID列表，逗号分隔
- **返回**: `201 Created`
- **返回示例**:
  ```json
  {
    "id": "...",
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
- **示例**:
  ```bash
  curl -X POST -F "serviceType=OpenAI" -F "key=sk-xxx" -F "description=GPT-4 API" http://localhost:5000/api/ApiKey
  ```

### PUT /api/ApiKey/{id}

更新一个已存在的 API 密钥。

- **方法**: `PUT`
- **路由**: `/api/ApiKey/{id}`
- **参数**:
  - `id` (必需, `guid`): 要更新的 API 密钥的 ID。
- **请求体** (表单数据):
  - `serviceType` (可选, `string`): 服务类型
  - `key` (可选, `string`): 密钥
  - `secret` (可选, `string`): 密钥暗文
  - `endpoint` (可选, `string`): 端点
  - `description` (可选, `string`): 描述
  - `isEnabled` (可选, `bool`): 是否启用
  - `modelIds` (可选, `string`): 关联的模型ID列表，逗号分隔
- **返回**: `204 No Content`
- **示例**:
  ```bash
  curl -X PUT -F "key=new_key_value" -F "isEnabled=false" http://localhost:5000/api/ApiKey/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```

### PATCH /api/ApiKey/{id}/toggle

切换 API 密钥的启用/禁用状态。

- **方法**: `PATCH`
- **路由**: `/api/ApiKey/{id}/toggle`
- **参数**:
  - `id` (必需, `guid`): API 密钥的 ID。
- **返回**: `204 No Content`
- **示例**:
  ```bash
  curl -X PATCH http://localhost:5000/api/ApiKey/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/toggle
  ```

### DELETE /api/ApiKey/{id}

删除一个 API 密钥。

- **方法**: `DELETE`
- **路由**: `/api/ApiKey/{id}`
- **参数**:
  - `id` (必需, `guid`): API 密钥的 ID。
- **返回**: `204 No Content`
- **示例**:
  ```bash
  curl -X DELETE http://localhost:5000/api/ApiKey/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```

### GET /api/ApiKey/model-usage-settings

获取所有 AI 模型使用配置。

- **方法**: `GET`
- **路由**: `/api/ApiKey/model-usage-settings`
- **返回**: `200 OK`
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "usageType": "...",
      "isEnabled": true,
      "aiModelId": "...",
      "aiModel": { ... }
    }
  ]
  ```

### POST /api/ApiKey/model-usage-settings

创建新的 AI 模型使用配置。

- **方法**: `POST`
- **路由**: `/api/ApiKey/model-usage-settings`
- **参数** (表单数据):
  - `setting` (必需, JSON): AIModelUsageSetting 对象
- **返回**: `201 Created`
- **示例**:
  ```bash
  curl -X POST -F "setting={\"usageType\":\"Judging\",\"isEnabled\":true,\"aiModelId\":\"...\"}" http://localhost:5000/api/ApiKey/model-usage-settings
  ```

### PUT /api/ApiKey/model-usage-settings/{id}

更新 AI 模型使用配置。

- **方法**: `PUT`
- **路由**: `/api/ApiKey/model-usage-settings/{id}`
- **参数**:
  - `id` (必需, `guid`): 配置 ID。
- **请求体** (表单数据):
  - `usageType` (可选, `string`): 使用类型
  - `isEnabled` (可选, `bool`): 是否启用
  - `aiModelId` (可选, `guid`): AI 模型 ID
- **返回**: `204 No Content`
- **示例**:
  ```bash
  curl -X PUT -F "isEnabled=false" http://localhost:5000/api/ApiKey/model-usage-settings/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```

### DELETE /api/ApiKey/model-usage-settings/{id}

删除 AI 模型使用配置。

- **方法**: `DELETE`
- **路由**: `/api/ApiKey/model-usage-settings/{id}`
- **参数**:
  - `id` (必需, `guid`): 配置 ID。
- **返回**: `204 No Content`
- **示例**:
  ```bash
  curl -X DELETE http://localhost:5000/api/ApiKey/model-usage-settings/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
  ```

### GET /api/ApiKey/all-models

获取所有可用的 AI 模型。

- **方法**: `GET`
- **路由**: `/api/ApiKey/all-models`
- **返回**: `200 OK`
- **返回示例**:
  ```json
  [
    {
      "id": "...",
      "name": "GPT-4",
      "provider": "OpenAI",
      "maxTokens": 8192,
      "isEnabled": true
    }
  ]
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