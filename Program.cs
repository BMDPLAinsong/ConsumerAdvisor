using ConsumerAdvisor.Api.Services; // 引入业务服务命名空间
using Microsoft.AspNetCore.Builder; // 引入 ASP.NET Core 应用构建器
using Microsoft.AspNetCore.Http; // 引入 HTTP 上下文相关类型
using Microsoft.Extensions.DependencyInjection; // 引入依赖注入容器
using Microsoft.Extensions.Hosting; // 引入主机环境相关类型
using Microsoft.Extensions.Logging; // 引入日志相关类型
using Microsoft.SemanticKernel; // 引入 Semantic Kernel 核心库

namespace ConsumerAdvisor.Api; // 项目命名空间

public class Program // 程序入口类
{
    public static void Main(string[] args) // 程序主入口方法
    {
        var builder = WebApplication.CreateBuilder(args); // 创建 Web 应用构建器

        var apiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY"); // 从环境变量读取 DashScope API 密钥
        if (string.IsNullOrEmpty(apiKey)) // 检查 API 密钥是否为空
            throw new InvalidOperationException("请设置环境变量 DASHSCOPE_API_KEY"); // 若为空则抛出异常

        var kernelBuilder = Kernel.CreateBuilder(); // 创建 Semantic Kernel 构建器
        kernelBuilder.AddOpenAIChatCompletion( // 注册 OpenAI 风格的聊天完成服务
            modelId: "qwen3.7-flash-2026-07-15", // 指定模型 ID 为阿里云千问 Plus
            apiKey: apiKey, // 使用从环境变量获取的 API 密钥
            orgId: null, // 不需要组织 ID
            endpoint: new Uri("https://dashscope.aliyuncs.com/compatible-mode/v1") // 指定 DashScope 兼容端点
        );
        var kernel = kernelBuilder.Build(); // 构建 Semantic Kernel 实例

        builder.Services.AddSingleton(kernel); // 注册 Kernel 为单例服务
        builder.Services.AddScoped<RequirementAnalysisService>(); // 注册需求分析服务为作用域服务
        builder.Services.AddScoped<ExtendedAnalysisService>(); // 注册扩展分析服务为作用域服务
        builder.Services.AddSingleton<ProductCatalogService>(); // 注册产品目录服务为单例服务
        builder.Services.AddSingleton<UserSettingsService>(); // 注册用户设置服务为单例服务
        builder.Services.AddScoped<WaitingListService>(); // 注册等待列表服务为作用域服务
        builder.Services.AddScoped<CalculatorService>(); // 注册计算器服务为作用域服务
        builder.Services.AddScoped<InputParserService>(); // 注册输入解析服务为作用域服务

        builder.Services.AddRazorPages(); // 注册 Razor Pages 服务
        builder.Services.AddServerSideBlazor(); // 注册 Blazor Server 服务
        builder.Services.AddHttpContextAccessor(); // 注册 HTTP 上下文访问器

        var app = builder.Build(); // 构建 Web 应用实例

        if (app.Environment.IsDevelopment()) // 判断是否为开发环境
        {
            app.UseDeveloperExceptionPage(); // 开发环境使用开发者异常页面
        }
        else // 非开发环境
        {
            app.UseExceptionHandler("/Error"); // 生产环境使用全局异常处理页面
            app.UseHsts(); // 启用 HTTP 严格传输安全协议
        }

        app.MapStaticAssets(); // 启用静态文件服务（.NET 9+ Static Web Assets 端点，同时提供 _framework 下的 Blazor JS 资源）
        app.UseRouting(); // 启用路由匹配

        app.MapBlazorHub(); // 映射 Blazor Hub 端点
        app.MapFallbackToPage("/_Host"); // 将请求回退到 _Host 页面

        app.Run(); // 运行应用
    }
}
