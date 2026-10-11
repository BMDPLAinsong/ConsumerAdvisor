// 引入项目模型命名空间
using ConsumerAdvisor.Api.Models;
// 引入日志扩展命名空间
using Microsoft.Extensions.Logging;
// 引入语义内核命名空间
using Microsoft.SemanticKernel;
// 引入JSON序列化命名空间
using System.Text.Json;

// 定义项目服务命名空间
namespace ConsumerAdvisor.Api.Services;

// 定义计算器服务类
public class CalculatorService
{
    // 私有只读语义内核字段，可为空
    private readonly Kernel? _kernel;
    // 私有只读日志记录器字段
    private readonly ILogger<CalculatorService> _logger;
    // 私有只读用户设置服务字段
    private readonly UserSettingsService _settingsService;

    // 私有静态只读行业平均成本字典，忽略大小写比较
    private static readonly Dictionary<string, decimal> IndustryAverageCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        // 汽车行业平均成本50元
        { "汽车", 50m },
        // 电子产品行业平均成本5元
        { "电子产品", 5m },
        // 服装行业平均成本10元
        { "服装", 10m },
        // 家居行业平均成本20元
        { "家居", 20m },
        // 食品行业平均成本5元
        { "食品", 5m },
        // 家电行业平均成本8元
        { "家电", 8m },
        // 数码行业平均成本6元
        { "数码", 6m },
        // 运动行业平均成本15元
        { "运动", 15m },
        // 美妆行业平均成本25元
        { "美妆", 25m },
        // 书籍行业平均成本3元
        { "书籍", 3m }
    };

    // 构造函数：初始化日志、设置服务和语义内核
    public CalculatorService(ILogger<CalculatorService> logger, UserSettingsService settingsService, Kernel? kernel = null)
    {
        // 赋值日志记录器
        _logger = logger;
        // 赋值用户设置服务
        _settingsService = settingsService;
        // 赋值语义内核
        _kernel = kernel;
    }

    // 异步计算使用成本的公开方法
    public async Task<CostCalculationResult> CalculateUsageCostAsync(CostCalculatorInput input)
    {
        // 记录日志：计算使用成本，包括价格和类别
        _logger.LogInformation("计算使用成本: 价格={Price}, 类别={Category}", input.ProductPrice, input.Category);

        // 计算总使用次数 = 预计使用年数 × 每年使用次数
        var totalUses = input.ExpectedYears * input.ExpectedTimesPerYear;
        // 如果总使用次数小于等于0则抛出异常
        if (totalUses <= 0)
            throw new ArgumentException("使用次数必须大于0");

        // 计算总成本 = 商品价格 + 维护成本
        var totalCost = input.ProductPrice + input.MaintenanceCost;
        // 计算单次使用成本 = 总成本 / 总使用次数
        var singleCost = totalCost / totalUses;

        // 初始化行业平均成本为可空类型，默认null
        decimal? averageCost = null;
        // 如果类别不为空
        if (!string.IsNullOrEmpty(input.Category))
        {
            // 从预定义字典中尝试获取该类别的行业平均成本
            IndustryAverageCosts.TryGetValue(input.Category, out var avg);
            // 赋值获取到的平均成本
            averageCost = avg;
        }

        // 如果没有行业平均成本且AI内核可用
        if (!averageCost.HasValue && _kernel != null)
        {
            // 通过AI获取行业平均成本，类别默认为"通用"
            averageCost = await FetchAverageCostFromAIAsync(input.Category ?? "通用", input.ProductPrice);
        }

        // 计算价值指数 = 单次成本 / 行业平均成本，无平均数据时为0
        decimal valueIndex = averageCost.HasValue && averageCost.Value > 0
            ? singleCost / averageCost.Value
            : 0m;

        // 声明推荐建议字符串变量
        string recommendation;
        // 声明评级字符串变量
        string rating;
        // 如果价值指数大于1.2，成本偏高
        if (valueIndex > 1.2m)
        {
            // 设置偏高推荐文案
            recommendation = "单次使用成本高于行业平均水平，建议考虑租赁或购买二手产品。";
            // 设置偏高评级
            rating = "⚠️ 偏高";
        }
        // 否则如果价值指数小于0.8且大于0，成本划算
        else if (valueIndex < 0.8m && valueIndex > 0)
        {
            // 设置划算推荐文案
            recommendation = "单次使用成本较低，相对划算。";
            // 设置划算评级
            rating = "👍 划算";
        }
        // 否则如果价值指数大于0，成本适中
        else if (valueIndex > 0)
        {
            // 设置适中推荐文案
            recommendation = "单次使用成本与行业平均相当，可以接受。";
            // 设置适中评级
            rating = "😊 适中";
        }
        // 其他情况，无法获取数据
        else
        {
            // 设置参考推荐文案
            recommendation = "无法获取行业平均数据，仅供参考。";
            // 设置参考评级
            rating = "📊 参考";
        }

        // 异步生成替代方案
        var alternatives = await GenerateAlternativesAsync(input, singleCost);

        // 返回成本计算结果对象
        return new CostCalculationResult
        {
            // 单次使用成本保留4位小数
            SingleUseCost = Math.Round(singleCost, 4),
            // 行业平均成本保留2位小数，无数据则为null
            IndustryAverageCost = averageCost.HasValue ? Math.Round(averageCost.Value, 2) : null,
            // 价值指数保留4位小数
            ValueIndex = Math.Round(valueIndex, 4),
            // 推荐建议
            Recommendation = recommendation,
            // 替代方案列表
            Alternatives = alternatives,
            // 总成本
            TotalCost = totalCost,
            // 总使用次数
            TotalUses = totalUses,
            // 评级
            Rating = rating
        };
    }

    // 私有异步方法：通过AI获取行业平均成本
    private async Task<decimal?> FetchAverageCostFromAIAsync(string category, decimal price)
    {
        // 如果内核不可用则返回null
        if (_kernel == null) return null;
        // 构造AI提示词
        var prompt = $@"
你是一位市场分析师。请估算在以下商品类别中，用户每次使用的平均成本（单位：元）。只需返回一个数字，不要其他文字。
类别：{category}
商品参考价格：{price}元
";
        try
        {
            // 调用AI内核执行提示词
            var response = await _kernel.InvokePromptAsync(prompt);
            // 获取响应字符串并去除空格
            var raw = response.ToString().Trim();
            // 尝试解析为小数
            if (decimal.TryParse(raw, out var avg))
                // 返回解析成功的平均值
                return avg;
            // 解析失败返回null
            return null;
        }
        catch (Exception ex)
        {
            // 记录AI获取失败的警告日志
            _logger.LogWarning(ex, "获取AI平均成本失败");
            // 返回null
            return null;
        }
    }

    // 私有异步方法：生成替代方案列表
    private async Task<List<AlternativeOption>> GenerateAlternativesAsync(CostCalculatorInput input, decimal currentCost)
    {
        // 如果AI内核可用
        if (_kernel != null)
        {
            // 构造AI提示词，请求替代方案
            var prompt = $@"
请为以下商品推荐2-3个替代方案（包括租赁、共享服务等），并评估每次使用成本。输出JSON格式：[{{""Name"":""方案A"",""CostPerUse"":数值,""Description"":""描述""}}]
商品：价格{input.ProductPrice}元，预计每年使用{input.ExpectedTimesPerYear}次，使用{input.ExpectedYears}年。
";
            try
            {
                // 调用AI内核执行提示词
                var response = await _kernel.InvokePromptAsync(prompt);
                // 获取响应字符串
                var json = response.ToString();
                // 反序列化为替代方案列表
                var list = JsonSerializer.Deserialize<List<AlternativeOption>>(json);
                // 如果反序列化成功则返回列表
                if (list != null) return list;
            }
            // 忽略异常
            catch { }
        }

        // 返回默认的替代方案列表（AI不可用或调用失败时）
        return new List<AlternativeOption>
        {
            // 租赁方案，成本为当前成本的60%
            new AlternativeOption { Name = "租赁", CostPerUse = Math.Round(currentCost * 0.6m, 2), Description = "短期租赁，降低一次性支出" },
            // 二手购买方案，成本为当前成本的70%
            new AlternativeOption { Name = "二手购买", CostPerUse = Math.Round(currentCost * 0.7m, 2), Description = "购买二手商品，降低成本" },
            // 共享服务方案，成本为当前成本的80%
            new AlternativeOption { Name = "共享服务", CostPerUse = Math.Round(currentCost * 0.8m, 2), Description = "使用共享服务，按需付费" }
        };
    }

    // 异步计算支付方案的公开方法
    public async Task<PaymentPlanResult> CalculatePaymentPlanAsync(PaymentPlanInput input)
    {
        // 记录日志：计算支付方案
        _logger.LogInformation("计算支付方案: 总价={Total}, 分期={Months}, 利率={Rate}%",
            input.TotalAmount, input.InstallmentMonths, input.AnnualInterestRate);

        // 计算贷款金额 = 总价 - 首付
        var loanAmount = input.TotalAmount - input.DownPayment;
        // 计算月利率 = 年利率 / 100 / 12
        var monthlyRate = input.AnnualInterestRate / 100 / 12;
        // 获取分期月数
        var months = input.InstallmentMonths;

        // 声明月供金额变量
        decimal monthlyPayment;
        // 如果月利率为0（无息分期）
        if (monthlyRate == 0)
        {
            // 月供 = 贷款金额 / 月数
            monthlyPayment = loanAmount / months;
        }
        // 有利率的情况
        else
        {
            // 使用等额本息公式计算月供
            monthlyPayment = loanAmount * monthlyRate * (decimal)Math.Pow((double)(1 + monthlyRate), months)
                / ((decimal)Math.Pow((double)(1 + monthlyRate), months) - 1);
        }

        // 计算带利息的总金额 = 月供 × 月数 + 首付
        var totalAmountWithInterest = monthlyPayment * months + input.DownPayment;
        // 计算总利息 = 带息总额 - 商品原价
        var totalInterest = totalAmountWithInterest - input.TotalAmount;

        // 生成还款时间表
        var schedule = GeneratePaymentSchedule(loanAmount, monthlyRate, monthlyPayment, months);

        // 异步获取支付能力评级和描述
        var (affordabilityRating, affordabilityDesc) = await GetAffordabilityRatingAsync(monthlyPayment);

        // 返回支付方案结果对象
        return new PaymentPlanResult
        {
            // 首付金额
            DownPayment = input.DownPayment,
            // 贷款金额
            LoanAmount = loanAmount,
            // 月供金额保留2位小数
            MonthlyPayment = Math.Round(monthlyPayment, 2),
            // 总利息保留2位小数
            TotalInterest = Math.Round(totalInterest, 2),
            // 带息总金额保留2位小数
            TotalAmountWithInterest = Math.Round(totalAmountWithInterest, 2),
            // 年利率
            AnnualInterestRate = input.AnnualInterestRate,
            // 分期月数
            InstallmentMonths = months,
            // 还款时间表
            Schedule = schedule,
            // 支付能力评级
            AffordabilityRating = affordabilityRating,
            // 支付能力描述
            AffordabilityDescription = affordabilityDesc
        };
    }

    // 私有方法：生成还款时间表
    private List<PaymentScheduleItem> GeneratePaymentSchedule(decimal loanAmount, decimal monthlyRate, decimal monthlyPayment, int months)
    {
        // 初始化还款时间表列表
        var schedule = new List<PaymentScheduleItem>();
        // 初始化剩余本金为贷款金额
        var remainingPrincipal = loanAmount;

        // 循环生成每月还款明细
        for (var i = 1; i <= months; i++)
        {
            // 计算当月利息 = 剩余本金 × 月利率
            var interest = remainingPrincipal * monthlyRate;
            // 计算当月本金 = 月供 - 利息
            var principal = monthlyPayment - interest;
            // 从剩余本金中扣除已还本金
            remainingPrincipal -= principal;

            // 如果剩余本金小于0则设为0（防止浮点误差）
            if (remainingPrincipal < 0) remainingPrincipal = 0;

            // 添加当月还款明细到时间表
            schedule.Add(new PaymentScheduleItem
            {
                // 月份
                Month = i,
                // 当月付款额保留2位小数
                Payment = Math.Round(monthlyPayment, 2),
                // 当月本金保留2位小数
                Principal = Math.Round(principal, 2),
                // 当月利息保留2位小数
                Interest = Math.Round(interest, 2),
                // 剩余本金保留2位小数
                RemainingPrincipal = Math.Round(remainingPrincipal, 2)
            });
        }

        // 返回完整的还款时间表
        return schedule;
    }

    // 私有异步方法：获取每月可支配收入
    private async Task<decimal> GetMonthlyDisposableIncome()
    {
        // 从设置服务获取用户设置
        var settings = await _settingsService.GetSettingsAsync();
        // 可支配收入 = 月收入 - 月支出 - 储蓄目标
        return settings.MonthlyIncome - settings.MonthlyExpenses - settings.SavingsTarget;
    }

    // 私有异步方法：根据月供获取支付能力评级
    private async Task<(string Rating, string Description)> GetAffordabilityRatingAsync(decimal monthlyPayment)
    {
        // 获取每月可支配收入
        var disposableIncome = await GetMonthlyDisposableIncome();

        // 如果可支配收入小于等于0
        if (disposableIncome <= 0)
            // 返回无参考评级
            return ("⚠️ 无参考", "请先在设置中配置月收入");

        // 计算月供占可支配收入的百分比
        var percentage = (monthlyPayment / disposableIncome) * 100;

        // 月供占比小于10%，轻松承担
        if (percentage < 10)
            return ("🟢 轻松承担", $"月供占可支配收入 {percentage:F1}%，毫无压力");
        // 月供占比小于20%，可以承担
        if (percentage < 20)
            return ("🟡 可以承担", $"月供占可支配收入 {percentage:F1}%，较为轻松");
        // 月供占比小于30%，略有压力
        if (percentage < 30)
            return ("🟠 略有压力", $"月供占可支配收入 {percentage:F1}%，需要控制支出");
        // 月供占比小于50%，压力较大
        if (percentage < 50)
            return ("🔴 压力较大", $"月供占可支配收入 {percentage:F1}%，生活质量可能受影响");
        // 月供占比超过50%，难以承担
        return ("🚫 难以承担", $"月供占可支配收入 {percentage:F1}%，建议降低预算");
    }
}