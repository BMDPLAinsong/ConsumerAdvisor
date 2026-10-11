// 定义消费者顾问 API 模型的命名空间
namespace ConsumerAdvisor.Api.Models;

// 购买分析请求 DTO，接收用户输入的购买意图
public class PurchaseAnalysisRequest
{
    // 用户原始输入文本，如 "我想买戴森吹风机，因为女朋友生日快到了"
    public string Text { get; set; }
}

// 分析结果 DTO，返回给前端的结构化分析结果
public class AnalysisResultDto
{
    // 识别出的商品名称
    public string Product { get; set; }
    // 识别出的购买原因
    public string Reason { get; set; }
    // 需求类型：必需品/改善型/炫耀型/跟风型
    public string DemandType { get; set; }
    // 理性度评分，范围 0-100
    public int RationalityScore { get; set; }
    // 非理性因素标签列表，如"冲动消费"、"情绪购买"
    public List<string> Motivations { get; set; }
    // 建议等待时长（小时）
    public int SuggestedWaitingHours { get; set; }
    // 相似用户购买频率：高频/中频/低频
    public string SimilarUserFrequency { get; set; }
}