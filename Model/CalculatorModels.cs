// 定义消费者顾问 API 模型的命名空间
namespace ConsumerAdvisor.Api.Models;

// 成本计算器输入模型，用于计算单次使用成本
public class CostCalculatorInput
{
    // 商品价格（单位：元）
    public decimal ProductPrice { get; set; }
    // 预计使用年限，默认3年
    public int ExpectedYears { get; set; } = 3;
    // 预计每年使用次数，默认365天（每天一次）
    public int ExpectedTimesPerYear { get; set; } = 365;
    // 商品分类，可选
    public string? Category { get; set; }
    // 维护成本，默认0元
    public decimal MaintenanceCost { get; set; } = 0;
}

// 成本计算结果模型，返回给用户的成本分析结果
public class CostCalculationResult
{
    // 单次使用成本
    public decimal SingleUseCost { get; set; }
    // 行业平均单次使用成本，用于对比
    public decimal? IndustryAverageCost { get; set; }
    // 性价比指数，越高代表越值得购买
    public decimal ValueIndex { get; set; }
    // 建议文本
    public string Recommendation { get; set; } = "";
    // 替代方案列表
    public List<AlternativeOption> Alternatives { get; set; } = new();
    // 总成本（购买价+维护费）
    public decimal TotalCost { get; set; }
    // 预计总使用次数
    public decimal TotalUses { get; set; }
    // 成本评级
    public string Rating { get; set; } = "";
}

// 替代方案选项模型
public class AlternativeOption
{
    // 替代方案名称
    public string Name { get; set; } = "";
    // 单次使用成本
    public decimal CostPerUse { get; set; }
    // 替代方案描述
    public string Description { get; set; } = "";
}

// 支付计划输入模型，用于分期付款计算
public class PaymentPlanInput
{
    // 商品总金额
    public decimal TotalAmount { get; set; }
    // 分期月数，默认12个月
    public int InstallmentMonths { get; set; } = 12;
    // 年利率，默认3.5%
    public decimal AnnualInterestRate { get; set; } = 3.5m;
    // 首付金额，默认0元
    public decimal DownPayment { get; set; } = 0;
}

// 支付计划结果模型，返回分期付款详情
public class PaymentPlanResult
{
    // 实际首付金额
    public decimal DownPayment { get; set; }
    // 贷款金额（总金额-首付）
    public decimal LoanAmount { get; set; }
    // 每月还款金额
    public decimal MonthlyPayment { get; set; }
    // 总利息
    public decimal TotalInterest { get; set; }
    // 总还款金额（本金+利息）
    public decimal TotalAmountWithInterest { get; set; }
    // 年利率
    public decimal AnnualInterestRate { get; set; }
    // 分期月数
    public int InstallmentMonths { get; set; }
    // 还款明细列表
    public List<PaymentScheduleItem> Schedule { get; set; } = new();
    // 可负担性评级
    public string AffordabilityRating { get; set; } = "";
    // 可负担性描述
    public string AffordabilityDescription { get; set; } = "";
}

// 还款计划明细项
public class PaymentScheduleItem
{
    // 期数（第几月）
    public int Month { get; set; }
    // 当月还款金额
    public decimal Payment { get; set; }
    // 当月本金
    public decimal Principal { get; set; }
    // 当月利息
    public decimal Interest { get; set; }
    // 剩余本金
    public decimal RemainingPrincipal { get; set; }
}

// 成本评级枚举
public enum CostRating
{
    VeryLow,    // 非常低
    Low,        // 低
    Moderate,   // 中等
    High,       // 高
    VeryHigh    // 非常高
}

// 可负担性评级枚举
public enum AffordabilityRating
{
    VeryAffordable, // 非常可负担
    Affordable,     // 可负担
    Stretching,     // 紧张
    Struggling,     // 挣扎
    Unaffordable    // 无法负担
}