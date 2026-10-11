// 引入消费者顾问 API 模型命名空间
using ConsumerAdvisor.Api.Models;
// 引入微软语义内核命名空间，用于 AI 提示词调用
using Microsoft.SemanticKernel;
// 引入微软日志扩展命名空间，用于依赖注入日志功能
using Microsoft.Extensions.Logging;
// 引入 JSON 序列化命名空间，用于解析和生成 JSON 数据
using System.Text.Json;
// 引入正则表达式命名空间，用于文本格式清理
using System.Text.RegularExpressions;

// 声明消费者顾问 API 服务命名空间
namespace ConsumerAdvisor.Api.Services;

// 声明扩展分析服务类，提供预算分析和替代方案增强功能
public class ExtendedAnalysisService
{
    // 声明只读语义内核字段，用于调用 AI 服务
    private readonly Kernel _kernel;
    // 声明只读日志字段，用于记录服务运行日志
    private readonly ILogger<ExtendedAnalysisService> _logger;

    // 声明预算分析提示词常量，用于指导 AI 进行消费预算分析
    private const string BudgetAnalysisPrompt = @"
你是一位消费理财专家。请对以下购买需求进行详细的预算分析。

用户信息：
- 月收入：{{$monthlyIncome}}元
- 月固定支出：{{$monthlyExpenses}}元
- 月储蓄目标：{{$savingsTarget}}元
- 可支配收入：{{$disposableIncome}}元

购买需求：
- 商品名称：{{$product}}
- 购买原因：{{$reason}}
- 商品价格：{{$productPrice}}元

请输出严格 JSON 格式（不要包含任何 Markdown 或其他文字）：
{
  ""incomePercentage"": 数值（价格占可支配收入的百分比，保留1位小数）,
  ""budgetRating"": ""轻松购买""（低于10%）|""适量购买""（10%-25%）|""需要慎重""（25%-50%）|""超出预算""（50%以上）,
  ""disposableIncome"": 计算后的可支配收入,
  ""similarPurchases"": [
    {
      ""description"": ""历史相似消费描述"",
      ""price"": 预估价格,
      ""outcome"": ""使用效果或后悔程度""
    }
  ],
  ""needMatchScore"": 0-100的需求匹配度分数（考虑原因是否充分）,
  ""reasoning"": ""2-3句话的简要分析理由""
}
"; // 预算分析提示词常量定义结束

    // 声明替代方案增强提示词常量，用于指导 AI 评估和优化替代商品
    private const string AlternativeEnhancementPrompt = @"
你是一位消费顾问。用户正在考虑购买以下商品，请对已有的替代方案进行评估和优化。

原商品：{{$product}}
购买原因：{{$reason}}
已有替代方案：{{$alternativesJson}}

请评估并优化替代方案，输出严格 JSON 格式：
{
  ""enhancedAlternatives"": [
    {
      ""name"": ""替代商品名称"",
      ""price"": 价格,
      ""pros"": [""优点1"", ""优点2""],
      ""cons"": [""缺点1""],
      ""suitability"": ""适合人群描述"",
      ""recommendationLevel"": ""强烈推荐|推荐|可选""
    }
  ]
}
"; // 替代方案增强提示词常量定义结束

    // 构造函数，初始化语义内核和日志依赖
    public ExtendedAnalysisService(Kernel kernel, ILogger<ExtendedAnalysisService> logger)
    {
        // 保存注入的语义内核实例
        _kernel = kernel;
        // 保存注入的日志实例
        _logger = logger;
    }

    // 异步方法：执行预算分析，返回预算分析结果
    public async Task<BudgetAnalysisResult> AnalyzeBudgetAsync(
        string product,           // 商品名称参数
        string reason,            // 购买原因参数
        decimal? productPrice,    // 商品价格参数（可空，为空时自动估算）
        decimal monthlyIncome,    // 月收入参数
        decimal monthlyExpenses,  // 月固定支出参数
        decimal savingsTarget,    // 月储蓄目标参数
        decimal disposableIncome) // 可支配收入参数
    {
        // 获取商品价格，若未提供则调用估算方法进行价格估算
        var price = productPrice ?? await EstimatePriceAsync(product);
        // 根据月收入、月支出、储蓄目标计算实际可支配收入
        var actualDisposable = monthlyIncome - monthlyExpenses - savingsTarget;
        // 若计算出的可支配收入小于等于零，则默认使用月收入的20%作为可支配收入
        if (actualDisposable <= 0) actualDisposable = monthlyIncome * 0.2m;

        // 创建内核参数对象，用于传递变量给 AI 提示词
        var arguments = new KernelArguments
        {
            // 添加月收入参数，格式化为无小数的字符串
            ["monthlyIncome"] = monthlyIncome.ToString("F0"),
            // 添加月支出参数，格式化为无小数的字符串
            ["monthlyExpenses"] = monthlyExpenses.ToString("F0"),
            // 添加储蓄目标参数，格式化为无小数的字符串
            ["savingsTarget"] = savingsTarget.ToString("F0"),
            // 添加可支配收入参数，格式化为无小数的字符串
            ["disposableIncome"] = actualDisposable.ToString("F0"),
            // 添加商品名称参数
            ["product"] = product,
            // 添加购买原因参数
            ["reason"] = reason,
            // 添加商品价格参数，格式化为无小数的字符串
            ["productPrice"] = price.ToString("F0")
        };

        // try 块：尝试调用 AI 进行预算分析
        try
        {
            // 调用语义内核，使用预算分析提示词和参数执行 AI 分析
            var response = await _kernel.InvokePromptAsync(BudgetAnalysisPrompt, arguments);
            // 将 AI 响应转换为字符串
            var rawJson = response.ToString();
            // 记录 AI 返回的原始 JSON 数据到日志
            _logger.LogInformation("预算分析 AI 返回: {RawJson}", rawJson);

            // 使用正则表达式清理 JSON 字符串，移除可能的 Markdown 代码块标记
            var cleaned = Regex.Replace(rawJson, @"^```(?:json)?\s*|\s*```$", "", RegexOptions.Multiline).Trim();
            // 反序列化清理后的 JSON 字符串为 AI 预算分析对象，配置属性名不区分大小写
            var aiResult = JsonSerializer.Deserialize<AiBudgetAnalysis>(cleaned, new JsonSerializerOptions
            {
                // 设置属性名不区分大小写，以便灵活映射 JSON 字段
                PropertyNameCaseInsensitive = true
            });

            // 检查反序列化结果是否为空
            if (aiResult == null)
            {
                // 记录警告日志，表示 AI 返回结果无法解析
                _logger.LogWarning("AI 返回结果无法解析，使用默认分析");
                // 返回默认预算分析结果
                return CreateDefaultBudgetAnalysis(product, price, actualDisposable);
            }

            // 返回从 AI 结果映射的预算分析结果对象
            return new BudgetAnalysisResult
            {
                // 设置月收入
                MonthlyIncome = monthlyIncome,
                // 设置可支配收入
                DisposableIncome = actualDisposable,
                // 设置商品价格
                ProductPrice = price,
                // 设置收入百分比（价格占可支配收入的比例）
                IncomePercentage = aiResult.IncomePercentage,
                // 设置预算评级
                BudgetRating = aiResult.BudgetRating,
                // 根据预算评级获取对应的 CSS 样式类名
                BudgetRatingClass = GetBudgetRatingClass(aiResult.BudgetRating),
                // 映射 AI 返回的相似购买记录列表
                SimilarPurchases = aiResult.SimilarPurchases?.Select(s => new HistoricalRecord
                {
                    // 设置历史记录描述
                    Description = s.Description,
                    // 设置历史记录价格
                    Price = s.Price,
                    // 设置历史记录结果
                    Outcome = s.Outcome
                }).ToList(),
                // 设置需求匹配分数
                NeedMatchScore = aiResult.NeedMatchScore,
                // 设置分析理由
                Reasoning = aiResult.Reasoning
            };
        }
        // catch 块：捕获 AI 分析过程中的异常
        catch (Exception ex)
        {
            // 记录错误日志，包含异常信息
            _logger.LogError(ex, "预算分析失败");
            // 返回默认预算分析结果
            return CreateDefaultBudgetAnalysis(product, price, actualDisposable);
        }
    }

    // 异步方法：增强替代方案列表，返回优化后的替代商品列表
    public async Task<List<AlternativeProduct>> EnhanceAlternativesAsync(
        string product,                          // 原商品名称参数
        string reason,                           // 购买原因参数
        List<AlternativeProduct> baseAlternatives) // 基础替代方案列表
    {
        // 若无基础替代方案则直接返回原列表
        if (!baseAlternatives.Any()) return baseAlternatives;

        // 将基础替代方案列表序列化为 JSON 字符串，用于传递给 AI
        var alternativesJson = System.Text.Json.JsonSerializer.Serialize(baseAlternatives.Select(a => new
        {
            // 设置替代方案名称
            name = a.Name,
            // 设置替代方案价格
            price = a.Price,
            // 设置替代方案优点列表
            pros = a.Pros,
            // 设置替代方案缺点列表
            cons = a.Cons,
            // 设置替代方案适合人群
            suitability = a.Suitability
        }));

        // 创建内核参数对象，用于传递变量给 AI 提示词
        var arguments = new KernelArguments
        {
            // 添加原商品名称参数
            ["product"] = product,
            // 添加购买原因参数
            ["reason"] = reason,
            // 添加替代方案 JSON 参数
            ["alternativesJson"] = alternativesJson
        };

        // try 块：尝试调用 AI 进行替代方案增强
        try
        {
            // 调用语义内核，使用替代方案增强提示词和参数执行 AI 分析
            var response = await _kernel.InvokePromptAsync(AlternativeEnhancementPrompt, arguments);
            // 将 AI 响应转换为字符串
            var rawJson = response.ToString();
            // 记录 AI 返回的原始 JSON 数据到日志
            _logger.LogInformation("替代方案 AI 分析返回: {RawJson}", rawJson);

            // 使用正则表达式清理 JSON 字符串，移除可能的 Markdown 代码块标记
            var cleaned = Regex.Replace(rawJson, @"^```(?:json)?\s*|\s*```$", "", RegexOptions.Multiline).Trim();
            // 反序列化清理后的 JSON 字符串为 AI 替代方案增强对象，配置属性名不区分大小写
            var aiResult = JsonSerializer.Deserialize<AiAlternativeEnhancement>(cleaned, new JsonSerializerOptions
            {
                // 设置属性名不区分大小写，以便灵活映射 JSON 字段
                PropertyNameCaseInsensitive = true
            });

            // 检查 AI 返回结果中的增强替代方案是否存在
            if (aiResult?.EnhancedAlternatives != null)
            {
                // 返回映射后的增强替代方案列表
                return aiResult.EnhancedAlternatives.Select(a => new AlternativeProduct
                {
                    // 设置替代方案名称
                    Name = a.Name,
                    // 设置替代方案价格
                    Price = a.Price,
                    // 设置优点列表，若为空则初始化为空列表
                    Pros = a.Pros ?? new List<string>(),
                    // 设置缺点列表，若为空则初始化为空列表
                    Cons = a.Cons ?? new List<string>(),
                    // 设置适合人群，若为空则设为空字符串
                    Suitability = a.Suitability ?? "",
                    // 设置来源为"AI优化"
                    Source = "AI优化"
                }).ToList();
            }
        }
        // catch 块：捕获 AI 分析过程中的异常
        catch (Exception ex)
        {
            // 记录错误日志，包含异常信息
            _logger.LogError(ex, "替代方案 AI 分析失败");
        }

        // 若 AI 增强失败则返回基础替代方案列表
        return baseAlternatives;
    }

    // 私有异步方法：根据商品名称估算价格
    private async Task<decimal> EstimatePriceAsync(string product)
    {
        // 简单的价格估算逻辑，此处使用同步方式处理，await 用于保持异步接口一致性
        await Task.CompletedTask;

        // 创建商品价格估算字典，忽略大小写比较
        var priceEstimates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            // 添加手机类商品价格估算
            { "iPhone", 6000 }, { "手机", 3000 }, { "MacBook", 15000 },
            // 添加电脑及配件类商品价格估算
            { "电脑", 5000 }, { "相机", 4000 }, { "戴森", 3000 },
            // 添加家用电器类商品价格估算
            { "吹风机", 300 }, { "吸尘器", 2000 }, { "耳机", 500 },
            // 添加其他消费品类价格估算
            { "手表", 2000 }, { "平板", 3000 }, { "音响", 1000 }
        };

        // 遍历价格估算字典，查找匹配的商品
        foreach (var estimate in priceEstimates)
        {
            // 检查商品名称是否包含字典中的关键字（忽略大小写）
            if (product.Contains(estimate.Key, StringComparison.OrdinalIgnoreCase))
                // 返回匹配的估算价格
                return estimate.Value;
        }

        // 若未匹配到任何商品，返回默认价格 1000 元
        return 1000;
    }

    // 私有方法：创建默认预算分析结果，用于 AI 分析失败时的降级处理
    private BudgetAnalysisResult CreateDefaultBudgetAnalysis(string product, decimal price, decimal disposable)
    {
        // 计算商品价格占可支配收入的百分比，若可支配收入为 0 则设为 100%
        var percentage = disposable > 0 ? (double)(price / disposable * 100) : 100;
        // 根据百分比确定预算评级
        var rating = percentage < 10 ? "轻松购买" :           // 低于 10%：轻松购买
                     percentage < 25 ? "适量购买" :            // 10%-25%：适量购买
                     percentage < 50 ? "需要慎重" : "超出预算"; // 25%-50%：需要慎重，高于 50%：超出预算

        // 返回默认预算分析结果对象
        return new BudgetAnalysisResult
        {
            // 设置默认月收入为 8000 元
            MonthlyIncome = 8000,
            // 设置可支配收入
            DisposableIncome = disposable,
            // 设置商品价格
            ProductPrice = price,
            // 设置收入百分比，保留 1 位小数
            IncomePercentage = Math.Round(percentage, 1),
            // 设置预算评级
            BudgetRating = rating,
            // 根据预算评级获取对应的 CSS 样式类名
            BudgetRatingClass = GetBudgetRatingClass(rating),
            // 设置默认需求匹配分数为 50
            NeedMatchScore = 50,
            // 设置默认分析理由
            Reasoning = "基于常规分析，建议根据实际需求和预算综合考虑。"
        };
    }

    // 私有方法：根据预算评级获取对应的 CSS 样式类名
    private string GetBudgetRatingClass(string rating) => rating switch
    {
        // "轻松购买" 对应 "rating-easy" 类
        "轻松购买" => "rating-easy",
        // "适量购买" 对应 "rating-moderate" 类
        "适量购买" => "rating-moderate",
        // "需要慎重" 对应 "rating-careful" 类
        "需要慎重" => "rating-careful",
        // "超出预算" 对应 "rating-over" 类
        "超出预算" => "rating-over",
        // 默认返回 "rating-moderate" 类
        _ => "rating-moderate"
    };

    // 内部类：用于反序列化 AI 返回的预算分析 JSON 数据
    private class AiBudgetAnalysis
    {
        // 收入百分比属性：价格占可支配收入的比例
        public double IncomePercentage { get; set; }
        // 预算评级属性：描述预算压力等级
        public string BudgetRating { get; set; } = string.Empty;
        // 可支配收入属性
        public decimal DisposableIncome { get; set; }
        // 相似购买记录列表属性：可为空
        public List<AiHistoricalRecord>? SimilarPurchases { get; set; }
        // 需求匹配分数属性：0-100 分
        public double NeedMatchScore { get; set; }
        // 分析理由属性
        public string Reasoning { get; set; } = string.Empty;
    }

    // 内部类：用于反序列化 AI 返回的历史购买记录数据
    private class AiHistoricalRecord
    {
        // 描述属性：历史消费描述
        public string Description { get; set; } = string.Empty;
        // 价格属性：可为空
        public decimal? Price { get; set; }
        // 结果属性：使用效果或后悔程度，可为空
        public string? Outcome { get; set; }
    }

    // 内部类：用于反序列化 AI 返回的替代方案增强数据
    private class AiAlternativeEnhancement
    {
        // 增强替代方案列表属性：可为空
        public List<AiEnhancedAlternative>? EnhancedAlternatives { get; set; }
    }

    // 内部类：用于反序列化 AI 返回的单个增强替代方案数据
    private class AiEnhancedAlternative
    {
        // 名称属性：替代商品名称
        public string Name { get; set; } = string.Empty;
        // 价格属性：可为空
        public decimal? Price { get; set; }
        // 优点列表属性：可为空
        public List<string>? Pros { get; set; }
        // 缺点列表属性：可为空
        public List<string>? Cons { get; set; }
        // 适合人群属性：可为空
        public string? Suitability { get; set; }
        // 推荐级别属性：可为空
        public string? RecommendationLevel { get; set; }
    }
}