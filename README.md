# SoraEssayJudge - AI 作文批改系统

SoraEssayJudge 是一个基于 ASP.NET Core 构建的现代化 Web API，旨在利用人工智能技术自动化作文批改流程。系统能够处理上传的手写作文图片，通过先进的 OCR 技术识别文字，并借助大语言模型（如 OpenAI GPT）进行智能评分和提供反馈。

## ✨ 主要功能

- **用户认证**:安全的 JWT 认证机制，支持用户注册和登录。
- **班级与学生管理**: 方便地创建和管理班级及学生信息。
- **作文任务管理**: 发布和管理作文题目与要求。
- **手写作文提交**: 学生可以上传手写作文的图片文件。
- **OCR 文字识别**: 集成阿里云 OCR 服务，高精度识别图片中的手写文字。
- **AI 智能批改**: 对接 OpenAI 服务，对识别出的文本进行分析、评分，并生成评语。
- **结果查询**: 查看和检索作文的批改结果和详情。

## 🛠️ 技术栈

- **后端**: ASP.NET Core 8
- **数据库**: Entity Framework Core + SQLite
- **认证**: JWT (JSON Web Tokens)
- **API 文档**: Swashbuckle (Swagger)
- **AI 服务**:
  - **文字识别 (OCR)**: 阿里云手写文字识别
  - **作文批改**: OpenAI API
- **图像处理**: SixLabors.ImageSharp

## 🚀 如何开始

### 先决条件

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 或其他代码编辑器
- Postman 或类似的 API 测试工具

### 安装与配置

1.  **克隆仓库**
    ```bash
    git clone <your-repository-url>
    cd SoraEssayJudge
    ```

2.  **配置数据库与基础设置**
    打开 `appsettings.json` 或 `appsettings.Development.json` 文件，填入基础配置信息（如数据库连接、JWT 密钥等），**无需在此处填写 OpenAI/Aliyun 的 API Key**。

    ```json
    {
      "Logging": {
        // ...
      },
      "AllowedHosts": "*",
      "ConnectionStrings": {
        "DefaultConnection": "Data Source=SEJDataBase.db"
      },
      "Jwt": {
        "Key": "YOUR_SUPER_SECRET_KEY_HERE", // 替换为你的密钥
        "Issuer": "SoraEssayJudge",
        "Audience": "SoraEssayJudge"
      },
      "Features": {
        "AllowUserRegistration": true
      }
    }
    ```

    > **注意**: 项目默认使用 SQLite 数据库，方便快速启动和开发。对于生产环境，建议通过修改 `ConnectionStrings` 并使用 Entity Framework Core 的数据库迁移功能，更换为更健壮的数据库，如 SQL Server, PostgreSQL 或 MySQL。

3.  **还原依赖并运行项目**
    在项目根目录打开终端，执行以下命令：
    ```bash
    dotnet restore
    dotnet run
    ```
    或者直接在 Visual Studio 中按 F5 启动项目。

4.  **通过 APIKey 接口配置第三方服务密钥**
    启动服务后，使用管理员账号登录系统，通过 `/api/ApiKey` 接口添加 OpenAI 和阿里云的 API Key。  
    你可以在 Swagger UI 或 Postman 中调用该接口，示例：

    - **添加 OpenAI 密钥**
      ```bash
      curl -X POST http://localhost:5000/api/ApiKey \
        -F "serviceType=OpenAI" \
        -F "key=YOUR_OPENAI_API_KEY" \
        -F "endpoint=YOUR_OPENAI_ENDPOINT" \
        -F "description=OpenAI 主账号"
      ```

    - **添加阿里云 OCR 密钥**
      ```bash
      curl -X POST http://localhost:5000/api/ApiKey \
        -F "serviceType=Aliyun" \
        -F "key=YOUR_ALIYUN_ACCESS_KEY_ID" \
        -F "secret=YOUR_ALIYUN_ACCESS_KEY_SECRET" \
        -F "description=阿里云手写文字识别"
      ```

    > 详细字段说明请参考 [API_Documentation.md](./API_Documentation.md) 的 ApiKeyController 部分。

### 使用

项目启动后，API 服务将在 `https://localhost:xxxx` 和 `http://localhost:yyyy` 上运行（具体端口号请查看 `Properties/launchSettings.json`）。

你可以通过访问 `https://localhost:xxxx/swagger` 来打开 Swagger UI，浏览所有可用的 API 端点并进行在线测试。

## 📖 API 端点概览

- `api/Auth`: 用户注册与登录
- `api/Class`: 班级管理
- `api/Student`: 学生管理
- `api/EssayAssignment`: 作文任务管理
- `api/EssaySubmission`: 提交作文、获取批改结果
- `api/EssayFile`: 上传作文图片文件
- `api/EssaySubmissionSearch`: 搜索作文提交记录

详细请见[API_Documentation.md](./API_Documentation.md)
