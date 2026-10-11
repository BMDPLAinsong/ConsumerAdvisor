﻿// 引入 ConsumerAdvisor.Api.Models 命名空间，提供模型类
using ConsumerAdvisor.Api.Models;
// 引入 ConsumerAdvisor.Api.Services 命名空间，提供服务接口
using ConsumerAdvisor.Api.Services;
// 引入 Microsoft.Extensions.Logging 命名空间，用于日志记录
using Microsoft.Extensions.Logging;
// 引入 Microsoft.SemanticKernel 命名空间，用于 AI 内核调用
using Microsoft.SemanticKernel;
// 引入 System.Text.Json 命名空间，用于 JSON 序列化与反序列化
using System.Text.Json;
// 引入 System.Text.RegularExpressions 命名空间，用于正则表达式处理
using System.Text.RegularExpressions;

// 声明所属命名空间为 ConsumerAdvisor.Api.Services
namespace ConsumerAdvisor.Api.Services;

// 定义需求分析服务类，用于分析用户的购买需求
public class RequirementAnalysisService
{
    // 声明只读字段 _kernel，用于调用 AI 内核
    private readonly Kernel _kernel;
    // 声明只读字段 _logger，用于记录日志
    private readonly ILogger<RequirementAnalysisService> _logger;

    // 定义提示词模板常量，用于指导 AI 进行消费心理与库存分析
    private const string AnalysisPrompt = @"
你是一位消费心理学与库存管理专家。请分析以下购买需求，输出JSON。

关键注意：
1. 如果用户提到已有库存（如'已有2瓶'、'家里还有'），请明确标记为'囤货行为'。
2. 评估商品是否真正'必需'，需结合消耗速度。例如：
   - 玻璃水：2L可用2-3个月，若已有2瓶则至少够用4-6个月，非迫切需求。
   - 纸巾：囤货相对合理，因为消耗快。
3. 理性评分应考虑：是否急需？是否在促销敏感期？是否超出合理库存量？

输出字段：
- demandType: [""必需品"",""改善型"",""炫耀型"",""跟风型"",""囤货行为""]
- rationalityScore: 0-100整数（有库存且非急需时，应<60）
- motivations: 字符串数组
- suggestedWaitingHours: 建议等待小时数（若有库存，至少72小时）
- similarUserFrequency: 商品消耗频率（高频消耗品/中频/低频消耗品）
- hasStock: 布尔值
- stockQuantity: 库存数量（如2瓶）
- estimatedUsageDuration: 库存可用时长（月）
- isUrgent: 是否紧急需要

购买原因：{{$reason}}
商品：{{$product}}
";

    // 构造函数，初始化 AI 内核与日志记录器
    public RequirementAnalysisService(Kernel kernel, ILogger<RequirementAnalysisService> logger)
{
    // 将传入的 kernel 参数赋值给 _kernel 字段
    _kernel = kernel;
    // 将传入的 logger 参数赋值给 _logger 字段
    _logger = logger;
}

// 异步方法，分析用户的购买需求，返回分析结果 DTO
public async Task<AnalysisResultDto> AnalyzeAsync(string product, string reason)
{
    // 1. 构造 Prompt 参数
    var arguments = new KernelArguments
    {
        // 设置模板中的 product 参数值
        ["product"] = product,
        // 设置模板中的 reason 参数值
        ["reason"] = reason
    };

    // 使用 try-catch 块捕获可能的异常
    try
    {
        // 2. 调用 AI，传入提示词模板与参数，获取 AI 响应
        var response = await _kernel.InvokePromptAsync(AnalysisPrompt, arguments);
        // 将 AI 响应转换为字符串
        var rawJson = response.ToString();

        // 记录 AI 返回的原始内容到日志
        _logger.LogInformation("AI 返回原始内容: {RawJson}", rawJson);

        // 3. 从 AI 响应中提取 JSON 部分
        var cleaned = ExtractJsonFromResponse(rawJson);

        // 4. 将清洗后的 JSON 反序列化为内部模型
        var aiResult = DeserializeAiResult(cleaned);

        // 检查反序列化结果是否为空
        if (aiResult == null)
        {
            // 记录错误日志，说明 AI 返回结果无法反序列化
            _logger.LogError("AI 返回结果无法反序列化。清洗后的内容: {CleanedJson}", cleaned);
            // 抛出操作异常，提示 AI 返回结果格式异常
            throw new InvalidOperationException("AI 返回结果格式异常，无法解析为 JSON");
        }

        // 5. 将内部模型映射为 DTO 并返回
        return new AnalysisResultDto
        {
            // 映射商品名称
            Product = product,
            // 映射购买原因
            Reason = reason,
            // 映射需求类型
            DemandType = aiResult.DemandType,
            // 映射理性评分
            RationalityScore = aiResult.RationalityScore,
            // 映射动机列表，若为空则初始化为空列表
            Motivations = aiResult.Motivations ?? new List<string>(),
            // 映射建议等待小时数
            SuggestedWaitingHours = aiResult.SuggestedWaitingHours,
            // 映射相似用户频率
            SimilarUserFrequency = aiResult.SimilarUserFrequency
        };
    }
    // 捕获所有异常
    catch (Exception ex)
    {
        // 记录错误日志，包含异常详情
        _logger.LogError(ex, "分析需求时发生错误");
        // 重新抛出异常以通知调用方
        throw;
    }
}

// 从 AI 响应字符串中提取 JSON 内容的私有方法
private string ExtractJsonFromResponse(string response)
{
    // 检查响应是否为空或仅包含空白字符
    if (string.IsNullOrWhiteSpace(response))
        // 若为空则直接返回原字符串
        return response;

    // 去除响应字符串首尾的空白字符
    var cleaned = response.Trim();

    // 移除开头可能存在的 Markdown 代码块标记（如 ```json）
    cleaned = Regex.Replace(cleaned, @"^```(?:json)?\s*", "", RegexOptions.Multiline);
    // 移除结尾可能存在的 Markdown 代码块标记（如 ```）
    cleaned = Regex.Replace(cleaned, @"\s*```$", "", RegexOptions.Multiline);

    // 查找第一个左花括号的位置
    var firstBrace = cleaned.IndexOf('{');
    // 查找最后一个右花括号的位置
    var lastBrace = cleaned.LastIndexOf('}');
    // 确认找到了有效的花括号对
    if (firstBrace >= 0 && lastBrace >= firstBrace)
    {
        // 截取 JSON 对象部分
        cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    // 返回清洗后的字符串并去除首尾空白
    return cleaned.Trim();
}

// 将 JSON 字符串反序列化为 AI 分析结果的私有方法
private AiAnalysisResult? DeserializeAiResult(string json)
{
    // 检查 JSON 字符串是否为空或仅包含空白字符
    if (string.IsNullOrWhiteSpace(json))
        // 若为空则返回 null
        return null;

    // 尝试第一次反序列化
    try
    {
        // 配置 JSON 序列化选项，忽略属性名大小写
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // 直接反序列化为 AiAnalysisResult 类型并返回
        return JsonSerializer.Deserialize<AiAnalysisResult>(json, options);
    }
    // 捕获 JSON 解析异常，静默处理
    catch (JsonException) { }

    // 尝试第二次反序列化（兼容不同的 JSON 结构）
    try
    {
        // 反序列化为 RawAiAnalysisResult 类型以兼容更多格式
        var rawResult = JsonSerializer.Deserialize<RawAiAnalysisResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        // 若反序列化结果为空则返回 null
        if (rawResult == null) return null;

        // 将 RawAiAnalysisResult 映射为 AiAnalysisResult
        return new AiAnalysisResult
        {
            // 映射需求类型，优先取字符串，其次取数组首元素，再次取对象的字符串表示
            DemandType = rawResult.DemandType is string s ? s : 
                         rawResult.DemandTypeArray?.FirstOrDefault() ?? 
                         rawResult.DemandType?.ToString() ?? string.Empty,
            // 映射理性评分
            RationalityScore = rawResult.RationalityScore,
            // 映射动机列表，若为空则初始化为空列表
            Motivations = rawResult.Motivations ?? new List<string>(),
            // 映射建议等待小时数
            SuggestedWaitingHours = rawResult.SuggestedWaitingHours,
            // 映射相似用户频率，优先取字符串，其次取数组首元素
            SimilarUserFrequency = rawResult.SimilarUserFrequency is string sf ? sf : 
                                  rawResult.SimilarUserFrequencyArray?.FirstOrDefault() ?? string.Empty
        };
    }
    // 捕获所有异常，静默处理
    catch { }

    // 若前面均失败，使用正则表达式方式解析
    return ParseWithRegex(json);
}

// 使用正则表达式解析 JSON 的后备方法
private AiAnalysisResult? ParseWithRegex(string json)
{
    // 使用 try-catch 包裹整个方法体
    try
    {
        // 创建空的分析结果对象
        var result = new AiAnalysisResult();

        // 使用正则匹配 demandType 字段
        var demandTypeMatch = Regex.Match(json, @"demandType[""']?\s*[:：]\s*([^,\}\]]+)", RegexOptions.IgnoreCase);
        // 检查 demandType 匹配是否成功
        if (demandTypeMatch.Success)
        {
            // 提取并清理 demandType 值，去除引号、空格和括号
            result.DemandType = demandTypeMatch.Groups[1].Value.Trim('"', '\'', ' ', '[', ']');
        }

        // 使用正则匹配 rationalityScore 字段
        var scoreMatch = Regex.Match(json, @"rationalityScore[""']?\s*[:：]\s*(\d+)", RegexOptions.IgnoreCase);
        // 检查 score 匹配是否成功并尝试解析为整数
        if (scoreMatch.Success && int.TryParse(scoreMatch.Groups[1].Value, out var score))
        {
            // 将解析成功的分数赋值给结果
            result.RationalityScore = score;
        }

        // 使用正则匹配 motivations 数组字段
        var motivationsMatch = Regex.Match(json, @"motivations[""']?\s*[:：]\s*\[([^\]]+)\]", RegexOptions.IgnoreCase);
        // 检查 motivations 匹配是否成功
        if (motivationsMatch.Success)
        {
            // 按逗号分割动机字符串，清理引号和空格，过滤空项，转为列表
            var items = motivationsMatch.Groups[1].Value.Split(',')
                .Select(s => s.Trim('"', '\'', ' '))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            // 将处理后的动机列表赋值给结果
            result.Motivations = items;
        }

        // 使用正则匹配 suggestedWaitingHours 字段
        var hoursMatch = Regex.Match(json, @"suggestedWaitingHours[""']?\s*[:：]\s*(\d+)", RegexOptions.IgnoreCase);
        // 检查 hours 匹配是否成功并尝试解析为整数
        if (hoursMatch.Success && int.TryParse(hoursMatch.Groups[1].Value, out var hours))
        {
            // 将解析成功的小时数赋值给结果
            result.SuggestedWaitingHours = hours;
        }

        // 使用正则匹配 similarUserFrequency 字段
        var frequencyMatch = Regex.Match(json, @"similarUserFrequency[""']?\s*[:：]\s*([^,\}\]]+)", RegexOptions.IgnoreCase);
        // 检查 frequency 匹配是否成功
        if (frequencyMatch.Success)
        {
            // 提取并清理 similarUserFrequency 值
            result.SimilarUserFrequency = frequencyMatch.Groups[1].Value.Trim('"', '\'', ' ', '[', ']');
        }

        // 检查是否至少解析出了一个有效字段
        if (!string.IsNullOrEmpty(result.DemandType) || result.RationalityScore > 0)
        {
            // 返回解析成功的结果
            return result;
        }

        // 若未解析出任何有效字段则返回 null
        return null;
    }
    // 捕获所有异常
    catch
    {
        // 发生异常时返回 null
        return null;
    }
}

// 定义原始 AI 分析结果类，用于兼容不同的 JSON 结构
private class RawAiAnalysisResult
{
    // 需求类型字段，可能为字符串或其他类型
    public object? DemandType { get; set; }
    // 需求类型数组字段，用于兼容数组格式
    public List<string>? DemandTypeArray { get; set; }
    // 理性评分字段
    public int RationalityScore { get; set; }
    // 动机列表字段
    public List<string>? Motivations { get; set; }
    // 建议等待小时数字段
    public int SuggestedWaitingHours { get; set; }
    // 相似用户频率字段，可能为字符串或其他类型
    public object? SimilarUserFrequency { get; set; }
    // 相似用户频率数组字段，用于兼容数组格式
    public List<string>? SimilarUserFrequencyArray { get; set; }
}

// 定义 AI 分析结果内部模型类
private class AiAnalysisResult
{
    // 需求类型，默认空字符串
    public string DemandType { get; set; } = string.Empty;
    // 理性评分，默认 0
    public int RationalityScore { get; set; }
    // 动机列表，默认空列表
    public List<string> Motivations { get; set; } = new List<string>();
    // 建议等待小时数，默认 0
    public int SuggestedWaitingHours { get; set; }
    // 相似用户频率，默认空字符串
    public string SimilarUserFrequency { get; set; } = string.Empty;
}
}