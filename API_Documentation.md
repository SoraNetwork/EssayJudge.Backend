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
- **参数** (JSON)：
  - `username` (必需, `string`): 用户名
  - `password` (必需, `string`): 密码（明文）
  - `name` (可选, `string`): 真实姓名
  - `phoneNumber` (可选, `string`): 手机号
- **示例**:
  ```bash
  curl -X POST -H "Content-Type: application/json" -d '{"username":"testuser","password":"123456","name":"张三","phoneNumber":"13800000000"}' http://localhost:5000/api/Auth/register
  ```

### POST /api/Auth/login

用户登录，返回 JWT 令牌和用户基本信息。

- **方法**: `POST`
- **路由**: `/api/Auth/login`
- **参数** (JSON)：
  - `username` (必需, `string`): 用户名
  - `password` (必需, `string`): 密码（明文）
- **返回**:
  - `token` (`string`): JWT 令牌
  - `name` (`string`): 用户姓名
  - `phoneNumber` (`string`): 手机号
- **示例**:
  ```bash
  curl -X POST -H "Content-Type: application/json" -d '{"username":"testuser","password":"123456"}' http://localhost:5000/api/Auth/login
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

---

## 4. EssaySubmissionController

用于管理作文的提交与查询。

### GET /EssaySubmission/summary

查询某个学生最近的 N 条作文概要。

- **方法**: `GET`
- **路由**: `/EssaySubmission/summary`
- **参数**:
  - `top` (必需, `int`): 获取最近的 N 条记录。
  - `studentId` (可选, `guid`): 学生的 GUID。
  - `studentName` (可选, `string`): 学生的 姓名 (如果未提供 `studentId`)。
- **示例**:
  ```bash
  # 获取指定学生ID的最近5篇作文概要
  curl -X GET "http://localhost:5000/EssaySubmission/summary?top=5&studentId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
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

---

## 5. EssaySubmissionSearchController

用于按标题模糊查询作文，返回作文、学生姓名/ID、分数、日期等。

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
