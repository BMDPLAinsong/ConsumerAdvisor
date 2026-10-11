# ConsumerAdvisor · 消费建议分析器

一个基于 **AI 的理性消费决策工具**：输入你的购买想法，AI 分析需求类型与冲动程度，低理性度商品自动进入「冷静清单」，冷静期内后台生成预算分析和替代方案，帮你做出更理性的购买决策。

## ✨ 功能特性

- **AI 消费心理分析**：输入「我想买 X，因为 Y」，AI 判断需求类型、给出理性度评分（0-100）与建议等待时长，并识别非理性消费因素
- **冷静清单**：理性度低于阈值的商品自动建议加入冷静期，到期再决定购买/放弃，治一治冲动消费
- **智能预算分析**：冷静期内后台自动生成预算占比分析（结合你的月收入/支出）和替代方案推荐
- **成本计算器**：单次使用成本对比行业平均、等额本息分期方案模拟与负担评级
- **可配置**：月收入/支出、冷静期时长、需求匹配阈值均可自定义，数据本地保存

## 🧰 技术栈

| 组件 | 说明 |
|---|---|
| .NET 10 | 目标框架，Blazor Server 渲染模型 |
| Semantic Kernel 1.77 | AI 编排框架 |
| 阿里云 DashScope（qwen-plus） | 大模型服务，OpenAI 兼容端点 |
| JSON 文件 | 本地持久化（用户设置、冷静清单） |

## 🚀 运行方式

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 阿里云 DashScope API Key（[获取地址](https://bailian.console.aliyun.com/)）

### 启动步骤

```bash
# 1. 设置 API Key（Windows PowerShell）
$env:DASHSCOPE_API_KEY = "你的 Key"

# 2. 运行
dotnet run --project ConsumerAdvisor.csproj
```

启动后浏览器访问 <http://localhost:5000>。

> ⚠️ 应用启动时会校验 `DASHSCOPE_API_KEY` 环境变量，未设置将直接报错退出。

## 🗺️ 页面一览

| 路由 | 页面 | 说明 |
|---|---|---|
| `/` | 首页 | 输入购买想法，AI 分析并评分 |
| `/waiting-list` | 冷静清单 | 管理冷静期商品，查看预算分析与替代方案 |
| `/calculator` | 计算器 | 单次使用成本、分期方案模拟 |
| `/settings` | 设置 | 收入支出、冷静期时长、匹配阈值 |

## 📁 项目结构

```
ConsumerAdvisor/
├── Model/          # 数据模型（DTO、用户设置、冷静清单条目等）
├── Services/       # 业务服务（AI 分析、解析、冷静清单、计算器等）
├── Pages/          # Blazor 页面与宿主
├── Shared/         # 共享布局
├── Program.cs      # 入口：DI 装配、Semantic Kernel 配置
└── ConsumerAdvisor.csproj
```

## 🔧 说明

- 用户数据（设置、冷静清单）以 JSON 保存在程序运行目录的 `Data/` 下，不会进入版本库
- 大模型为 `qwen-plus`，接入点为 DashScope 的 OpenAI 兼容端点，理论上可替换为其他兼容服务
- 所有分析结果仅供参考，请理性消费
