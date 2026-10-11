// 定义消费者顾问 API 模型的命名空间
namespace ConsumerAdvisor.Api.Models;

// 用户设置模型，存储用户的个人财务配置和偏好
public class UserSettings
{
    // 月收入，默认8000元
    public decimal MonthlyIncome { get; set; } = 8000m;
    // 月支出，默认5000元
    public decimal MonthlyExpenses { get; set; } = 5000m;
    // 储蓄目标，默认2000元
    public decimal SavingsTarget { get; set; } = 2000m;
    // 冷静期时长（小时），默认24小时
    public int WaitingPeriodHours { get; set; } = 24;
    // 是否启用通知
    public bool EnableNotifications { get; set; } = true;
    // 通知偏好设置
    public NotificationType NotificationPreferences { get; set; } = new();
    // 需求匹配度阈值，低于此值会提醒不建议购买，默认70%
    public int DemandMatchThreshold { get; set; } = 70;
    // 最后更新时间
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

// 通知类型配置模型
public class NotificationType
{
    // 是否显示倒计时
    public bool ShowCountdown { get; set; } = true;
    // 是否显示徽章
    public bool ShowBadge { get; set; } = true;
    // 是否显示 Toast 提示
    public bool ShowToast { get; set; } = true;
    // 是否在12小时后提醒
    public bool EnableReminderAt12Hours { get; set; } = true;
    // 是否在1小时前提醒
    public bool EnableReminderAt1Hour { get; set; } = true;
    // 是否在过期时提醒
    public bool EnableReminderAtExpired { get; set; } = true;
}

// 用户设置数据模型，用于 JSON 序列化存储
public class UserSettingsData
{
    // 用户设置对象
    public UserSettings Settings { get; set; } = new();
    // 最后更新时间
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}