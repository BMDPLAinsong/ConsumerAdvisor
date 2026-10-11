// 引入消费者顾问API的模型命名空间
using ConsumerAdvisor.Api.Models;
// 引入System.Text.Json命名空间，用于JSON序列化和反序列化
using System.Text.Json;
// 引入日志接口命名空间
using Microsoft.Extensions.Logging;

// 定义消费者顾问API的服务命名空间
namespace ConsumerAdvisor.Api.Services;

// 用户设置服务类，负责管理用户配置的读写操作
public class UserSettingsService
{
    // 存储用户设置数据文件的路径（只读字段）
    private readonly string _dataPath;
    // 日志记录器实例（只读字段）
    private readonly ILogger<UserSettingsService> _logger;
    // 缓存的用户设置对象，避免频繁读取文件（可空字段）
    private UserSettings? _cachedSettings;
    // 信号量锁，用于保证多线程环境下的并发安全（初始计数为1，最大计数为1）
    private readonly SemaphoreSlim _lock = new(1, 1);

    // 构造函数，接收日志记录器作为依赖注入参数
    public UserSettingsService(ILogger<UserSettingsService> logger)
    {
        // 将传入的日志记录器赋值给私有字段
        _logger = logger;
        // 拼接数据文件路径，位于应用基础目录下的Data文件夹中，文件名为user_settings.json
        _dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "user_settings.json");
        // 确保数据目录存在，如果不存在则创建
        EnsureDataDirectory();
    }

    // 私有方法：确保数据存储目录存在，不存在则创建
    private void EnsureDataDirectory()
    {
        // 获取数据文件所在的目录路径
        var directory = Path.GetDirectoryName(_dataPath);
        // 检查目录路径是否有效且目录是否不存在
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            // 创建不存在的目录（包括父目录）
            Directory.CreateDirectory(directory);
        }
    }

    // 异步获取用户设置，返回用户设置对象（如果缓存中有则直接返回，否则从文件加载）
    public async Task<UserSettings> GetSettingsAsync()
    {
        // 快速路径：如果缓存中已有设置，直接返回缓存结果，无需加锁
        if (_cachedSettings != null)
            return _cachedSettings;

        // 异步等待获取信号量锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 二次检查：在获取锁后再次检查缓存，防止其他线程已完成加载
            if (_cachedSettings != null)
                return _cachedSettings;

            // 检查设置文件是否存在
            if (!File.Exists(_dataPath))
            {
                // 文件不存在，创建新的默认用户设置实例
                _cachedSettings = new UserSettings();
                // 设置最后更新时间为当前UTC时间
                _cachedSettings.LastUpdated = DateTime.UtcNow;
                // 构建包含设置和时间戳的数据传输对象
                var newData = new UserSettingsData { Settings = _cachedSettings, LastUpdated = DateTime.UtcNow };
                // 将数据对象序列化为格式化的JSON字符串（缩进美观）
                var newJson = JsonSerializer.Serialize(newData, new JsonSerializerOptions { WriteIndented = true });
                // 异步将JSON字符串写入文件
                await File.WriteAllTextAsync(_dataPath, newJson);
                // 记录信息级日志：用户设置已保存
                _logger.LogInformation("用户设置已保存");
                // 返回新创建的缓存设置
                return _cachedSettings;
            }

            // 文件存在，异步读取全部JSON文本内容
            var json = await File.ReadAllTextAsync(_dataPath);
            // 将JSON字符串反序列化为UserSettingsData对象
            var data = JsonSerializer.Deserialize<UserSettingsData>(json);
            // 从反序列化结果中获取设置对象，如果为null则创建新的默认设置
            _cachedSettings = data?.Settings ?? new UserSettings();
            // 返回缓存的设置对象
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            // 捕获异常，记录错误级日志并包含异常详情
            _logger.LogError(ex, "加载用户设置失败");
            // 发生异常时创建默认设置作为降级处理
            _cachedSettings = new UserSettings();
            // 返回默认设置
            return _cachedSettings;
        }
        finally
        {
            // 无论成功或失败都释放信号量锁，确保不会死锁
            _lock.Release();
        }
    }

    // 异步保存用户设置到文件，接收用户设置对象作为参数
    public async Task SaveSettingsAsync(UserSettings settings)
    {
        // 异步等待获取信号量锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 更新设置的最后修改时间为当前UTC时间
            settings.LastUpdated = DateTime.UtcNow;
            // 构建包含设置和时间戳的数据传输对象
            var data = new UserSettingsData { Settings = settings, LastUpdated = DateTime.UtcNow };
            // 将数据对象序列化为格式化的JSON字符串（缩进美观）
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            // 异步将JSON字符串写入文件，覆盖原有内容
            await File.WriteAllTextAsync(_dataPath, json);
            // 更新内存缓存为最新保存的设置对象
            _cachedSettings = settings;
            // 记录信息级日志：用户设置已保存
            _logger.LogInformation("用户设置已保存");
        }
        catch (Exception ex)
        {
            // 捕获异常，记录错误级日志并包含异常详情
            _logger.LogError(ex, "保存用户设置失败");
            // 重新抛出异常，交由调用方处理
            throw;
        }
        finally
        {
            // 无论成功或失败都释放信号量锁，确保不会死锁
            _lock.Release();
        }
    }

    // 异步计算并返回每月可支配收入（月度收入减去月度支出和储蓄目标）
    public async Task<decimal> GetMonthlyDisposableIncomeAsync()
    {
        // 异步获取当前用户设置
        var settings = await GetSettingsAsync();
        // 计算可支配收入：收入 - 支出 - 储蓄目标，并返回结果
        return settings.MonthlyIncome - settings.MonthlyExpenses - settings.SavingsTarget;
    }
}