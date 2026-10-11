// 引入 JSON 序列化相关命名空间
using System.Text.Json.Serialization;

// 定义消费者顾问 API 模型的命名空间
namespace ConsumerAdvisor.Api.Models;

// 冷静清单项模型，记录用户加入冷静期的商品
public class WaitingListItem
{
    // 唯一标识符
    public Guid Id { get; set; } = Guid.NewGuid();
    // 商品名称
    public string Product { get; set; } = string.Empty;
    // 购买原因
    public string Reason { get; set; } = string.Empty;
    // 添加时间
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    // 过期时间（冷静期结束时间）
    public DateTime ExpiresAt { get; set; }
    // 商品价格，可选
    public decimal? ProductPrice { get; set; }
    // 快速分析结果
    public AnalysisResultDto? QuickAnalysis { get; set; }
    // 预算分析结果
    public BudgetAnalysisResult? BudgetAnalysis { get; set; }
    // 替代方案列表
    public List<AlternativeProduct>? Alternatives { get; set; }
    // 状态：待冷静/已过期/已完成/已取消
    public WaitingListStatus Status { get; set; } = WaitingListStatus.Pending;
    // 备注，可选
    public string? Notes { get; set; }
}

// 冷静清单状态枚举
public enum WaitingListStatus
{
    Pending,    // 待冷静
    Expired,    // 已过期（冷静期结束但未购买）
    Completed,  // 已完成购买
    Cancelled   // 已取消
}

// 预算分析结果模型
public class BudgetAnalysisResult
{
    // 月收入
    public decimal MonthlyIncome { get; set; }
    // 可支配收入
    public decimal DisposableIncome { get; set; }
    // 商品价格
    public decimal ProductPrice { get; set; }
    // 收入占比（商品价格占月收入百分比）
    public double IncomePercentage { get; set; }
    // 预算评级文本
    public string BudgetRating { get; set; } = string.Empty;
    // 预算评级 CSS 类名
    public string BudgetRatingClass { get; set; } = string.Empty;
    // 历史相似购买记录
    public List<HistoricalRecord>? SimilarPurchases { get; set; }
    // 需求匹配度评分（0-100）
    public double NeedMatchScore { get; set; }
    // 分析推理说明
    public string Reasoning { get; set; } = string.Empty;
}

// 历史购买记录模型
public class HistoricalRecord
{
    // 描述
    public string Description { get; set; } = string.Empty;
    // 价格
    public decimal? Price { get; set; }
    // 购买日期
    public DateTime? PurchaseDate { get; set; }
    // 购买结果
    public string? Outcome { get; set; }
}

// 替代商品模型
public class AlternativeProduct
{
    // 替代方案名称
    public string Name { get; set; } = string.Empty;
    // 价格
    public decimal? Price { get; set; }
    // 优点列表
    public List<string> Pros { get; set; } = new();
    // 缺点列表
    public List<string> Cons { get; set; } = new();
    // 适用度描述
    public string Suitability { get; set; } = string.Empty;
    // 来源：商品库 / AI推荐
    public string Source { get; set; } = "商品库";
}

// 冷静清单数据模型，用于 JSON 序列化存储
public class WaitingListData
{
    // 冷静清单项列表
    public List<WaitingListItem> Items { get; set; } = new();
    // 最后更新时间
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}