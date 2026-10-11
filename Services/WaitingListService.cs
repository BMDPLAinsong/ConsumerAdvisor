// 引入 ConsumerAdvisor.Api.Models 命名空间，提供冷静清单相关的数据模型类
using ConsumerAdvisor.Api.Models;
// 引入 System.Text.Json 命名空间，用于 JSON 序列化和反序列化
using System.Text.Json;
// 引入 Microsoft.Extensions.Logging 命名空间，用于日志记录
using Microsoft.Extensions.Logging;

// 定义 ConsumerAdvisor.Api.Services 命名空间，包含业务服务类
namespace ConsumerAdvisor.Api.Services;

// 定义 WaitingListService 类，提供冷静清单的核心业务逻辑
public class WaitingListService
{
    // 定义只读字段 _dataPath，存储数据文件的路径
    private readonly string _dataPath;
    // 定义只读字段 _logger，用于记录日志信息
    private readonly ILogger<WaitingListService> _logger;
    // 定义只读字段 _settingsService，用于获取用户设置
    private readonly UserSettingsService _settingsService;
    // 定义只读字段 _productCatalog，用于商品目录相关操作
    private readonly ProductCatalogService _productCatalog;
    // 定义只读字段 _analysisService，用于增强分析服务
    private readonly ExtendedAnalysisService _analysisService;
    // 定义可空字段 _cachedData，缓存从文件加载的冷静清单数据
    private WaitingListData? _cachedData;
    // 定义信号量 _lock，用于实现异步锁控制，确保线程安全
    private readonly SemaphoreSlim _lock = new(1, 1);

    // 定义事件 DataChanged，当数据发生变化时触发通知
    public event Action? DataChanged;

    // 定义构造函数，初始化 WaitingListService 类的实例
    public WaitingListService(
        // 参数 logger：日志记录器实例
        ILogger<WaitingListService> logger,
        // 参数 settingsService：用户设置服务实例
        UserSettingsService settingsService,
        // 参数 productCatalog：商品目录服务实例
        ProductCatalogService productCatalog,
        // 参数 analysisService：增强分析服务实例
        ExtendedAnalysisService analysisService)
    {
        // 将传入的 logger 参数赋值给 _logger 字段
        _logger = logger;
        // 将传入的 settingsService 参数赋值给 _settingsService 字段
        _settingsService = settingsService;
        // 将传入的 productCatalog 参数赋值给 _productCatalog 字段
        _productCatalog = productCatalog;
        // 将传入的 analysisService 参数赋值给 _analysisService 字段
        _analysisService = analysisService;
        // 组合数据文件路径，指向应用基目录下的 Data/waiting_list.json
        _dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "waiting_list.json");
        // 确保数据目录存在，不存在则创建
        EnsureDataDirectory();
    }

    // 定义私有方法 EnsureDataDirectory，确保数据存储目录存在
    private void EnsureDataDirectory()
    {
        // 获取数据文件所在的目录路径
        var directory = Path.GetDirectoryName(_dataPath);
        // 检查目录路径不为空且目录不存在
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            // 创建不存在的目录
            Directory.CreateDirectory(directory);
        }
    }

    // 定义公开异步方法 GetAllAsync，获取所有冷静清单项
    public async Task<List<WaitingListItem>> GetAllAsync()
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 返回所有清单项，按添加时间倒序排列
        return _cachedData!.Items.OrderByDescending(x => x.AddedAt).ToList();
    }

    // 定义公开异步方法 GetPendingAsync，获取所有待处理的冷静清单项
    public async Task<List<WaitingListItem>> GetPendingAsync()
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 返回状态为 Pending 的清单项，按过期时间升序排列
        return _cachedData!.Items
            .Where(x => x.Status == WaitingListStatus.Pending)
            .OrderBy(x => x.ExpiresAt)
            .ToList();
    }

    // 定义公开异步方法 GetByIdAsync，根据唯一标识获取单个冷静清单项
    public async Task<WaitingListItem?> GetByIdAsync(Guid id)
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 在缓存数据中查找匹配 ID 的项
        return _cachedData!.Items.FirstOrDefault(x => x.Id == id);
    }

    // 定义公开异步方法 AddAsync，添加新的冷静清单项
    public async Task<WaitingListItem> AddAsync(string product, string reason, decimal? price = null)
    {
        // 确保数据已加载
        await EnsureDataLoadedAsync();

        // 获取用户设置，用于计算冷静期时长
        var settings = await _settingsService.GetSettingsAsync();
        // 创建新的冷静清单项对象
        var item = new WaitingListItem
        {
            // 设置商品名称
            Product = product,
            // 设置购买理由
            Reason = reason,
            // 设置商品价格（可选）
            ProductPrice = price,
            // 记录添加时间为当前 UTC 时间
            AddedAt = DateTime.UtcNow,
            // 计算过期时间：添加时间加上设置的冷静期小时数
            ExpiresAt = DateTime.UtcNow.AddHours(settings.WaitingPeriodHours),
            // 设置初始状态为待处理
            Status = WaitingListStatus.Pending
        };

        // 等待获取异步锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 将新项添加到缓存的集合中
            _cachedData!.Items.Add(item);
            // 持久化保存数据到文件
            await SaveDataAsync();
        }
        finally
        {
            // 释放异步锁
            _lock.Release();
        }

        // 记录信息日志，提示商品已加入冷静清单
        _logger.LogInformation("商品 '{Product}' 已加入冷静清单", product);

        // 异步生成分析（不阻塞添加操作）
        _ = Task.Run(async () =>
        {
            try
            {
                // 为该项生成预算、替代方案等分析内容
                await GenerateAnalysisAsync(item.Id);
                // 触发数据变更事件，通知 UI 层刷新
                DataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                // 记录错误日志，提示分析生成失败
                _logger.LogError(ex, "生成分析失败 for item {Id}", item.Id);
                // 即使失败也触发数据变更事件，确保 UI 同步
                DataChanged?.Invoke();
            }
        });

        // 返回新创建的冷静清单项
        return item;
    }

    // 定义公开异步方法 RemoveAsync，根据 ID 移除冷静清单项
    public async Task RemoveAsync(Guid id)
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 等待获取异步锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 在缓存中查找匹配 ID 的项
            var item = _cachedData!.Items.FirstOrDefault(x => x.Id == id);
            // 如果找到了该项
            if (item != null)
            {
                // 从集合中移除该项
                _cachedData!.Items.Remove(item);
                // 持久化保存数据到文件
                await SaveDataAsync();
                // 记录信息日志，提示商品已从冷静清单移除
                _logger.LogInformation("商品 '{Product}' 已从冷静清单移除", item.Product);
            }
        }
        finally
        {
            // 释放异步锁
            _lock.Release();
        }
    }

    // 定义公开异步方法 CompletePurchaseAsync，标记商品为已完成购买
    public async Task CompletePurchaseAsync(Guid id)
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 等待获取异步锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 在缓存中查找匹配 ID 的项
            var item = _cachedData!.Items.FirstOrDefault(x => x.Id == id);
            // 如果找到了该项
            if (item != null)
            {
                // 将该项状态更新为已完成
                item.Status = WaitingListStatus.Completed;
                // 持久化保存数据到文件
                await SaveDataAsync();
                // 记录信息日志，提示商品已标记为已完成购买
                _logger.LogInformation("商品 '{Product}' 已标记为已完成购买", item.Product);
            }
        }
        finally
        {
            // 释放异步锁
            _lock.Release();
        }
    }

    // 定义公开异步方法 UpdateStatusAsync，根据 ID 更新冷静清单项的状态
    public async Task UpdateStatusAsync(Guid id, WaitingListStatus status)
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 等待获取异步锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 在缓存中查找匹配 ID 的项
            var item = _cachedData!.Items.FirstOrDefault(x => x.Id == id);
            // 如果找到了该项
            if (item != null)
            {
                // 更新该项的状态为传入的新状态
                item.Status = status;
                // 持久化保存数据到文件
                await SaveDataAsync();
            }
        }
        finally
        {
            // 释放异步锁
            _lock.Release();
        }
    }

    // 定义公开异步方法 CheckAndUpdateExpiredAsync，检查并更新所有已过期的项
    public async Task<int> CheckAndUpdateExpiredAsync()
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 初始化过期计数器为 0
        var expiredCount = 0;

        // 等待获取异步锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 遍历所有状态为 Pending 且已过期的项
            foreach (var item in _cachedData!.Items.Where(x =>
                x.Status == WaitingListStatus.Pending && x.ExpiresAt <= DateTime.UtcNow))
            {
                // 将该项状态标记为已过期
                item.Status = WaitingListStatus.Expired;
                // 递增过期计数器
                expiredCount++;
            }

            // 如果有过期的项
            if (expiredCount > 0)
            {
                // 持久化保存数据到文件
                await SaveDataAsync();
                // 记录信息日志，提示更新了多少个过期商品
                _logger.LogInformation("更新了 {Count} 个过期商品", expiredCount);
            }
        }
        finally
        {
            // 释放异步锁
            _lock.Release();
        }

        // 返回过期项的数量
        return expiredCount;
    }

    // 定义公开异步方法 GenerateAnalysisAsync，为指定项生成分析内容
    public async Task<WaitingListItem?> GenerateAnalysisAsync(Guid id)
    {
        // 根据 ID 获取对应的冷静清单项
        var item = await GetByIdAsync(id);
        // 如果未找到该项，返回 null
        if (item == null) return null;

        // 获取用户设置
        var settings = await _settingsService.GetSettingsAsync();
        // 获取用户每月可支配收入
        var monthlyDisposable = await _settingsService.GetMonthlyDisposableIncomeAsync();

        // 1. 生成预算分析
        item.BudgetAnalysis = await _analysisService.AnalyzeBudgetAsync(
            // 传入商品名称
            item.Product,
            // 传入购买理由
            item.Reason,
            // 传入商品价格
            item.ProductPrice,
            // 传入每月收入
            settings.MonthlyIncome,
            // 传入每月支出
            settings.MonthlyExpenses,
            // 传入储蓄目标
            settings.SavingsTarget,
            // 传入每月可支配收入
            monthlyDisposable
        );

        // 2. 从商品库查找替代方案
        var catalogAlternatives = await _productCatalog.FindAlternativesAsync(item.Product);

        // 3. AI 增强替代方案分析
        if (catalogAlternatives.Any())
        {
            // 调用增强分析服务，生成替代方案的详细分析
            item.Alternatives = await _analysisService.EnhanceAlternativesAsync(
                // 传入原商品名称
                item.Product,
                // 传入购买理由
                item.Reason,
                // 传入商品库中找到的替代方案列表
                catalogAlternatives
            );
        }

        // 等待获取异步锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 持久化保存数据到文件
            await SaveDataAsync();
        }
        finally
        {
            // 释放异步锁
            _lock.Release();
        }

        // 记录信息日志，提示商品分析已完成
        _logger.LogInformation("商品 '{Product}' 分析完成", item.Product);
        // 返回分析完成的冷静清单项
        return item;
    }

    // 定义公开异步方法 GetPendingCountAsync，获取待处理项的数量
    public async Task<int> GetPendingCountAsync()
    {
        // 确保数据已从文件加载到内存中
        await EnsureDataLoadedAsync();
        // 统计状态为 Pending 的项的数量并返回
        return _cachedData!.Items.Count(x => x.Status == WaitingListStatus.Pending);
    }

    // 定义私有异步方法 EnsureDataLoadedAsync，确保数据已加载到内存
    private async Task EnsureDataLoadedAsync()
    {
        // 如果缓存数据已存在，直接返回
        if (_cachedData != null) return;

        // 等待获取异步锁，确保线程安全
        await _lock.WaitAsync();
        try
        {
            // 再次检查缓存数据是否已存在（双重检查锁定模式）
            if (_cachedData != null) return;

            // 如果数据文件不存在
            if (!File.Exists(_dataPath))
            {
                // 创建新的空数据对象
                _cachedData = new WaitingListData();
                // 保存空数据到文件，确保文件存在
                await SaveDataAsync();
                // 返回，结束加载
                return;
            }

            // 读取数据文件的全部内容
            var json = await File.ReadAllTextAsync(_dataPath);
            // 反序列化 JSON 内容为 WaitingListData 对象，若失败则创建新对象
            _cachedData = JsonSerializer.Deserialize<WaitingListData>(json) ?? new WaitingListData();

            // 检查并更新过期项
            var now = DateTime.UtcNow;
            // 遍历所有状态为 Pending 且已过期的项
            foreach (var item in _cachedData.Items.Where(x =>
                x.Status == WaitingListStatus.Pending && x.ExpiresAt <= now))
            {
                // 将该项状态标记为已过期
                item.Status = WaitingListStatus.Expired;
            }
        }
        catch (Exception ex)
        {
            // 记录错误日志，提示加载冷静清单失败
            _logger.LogError(ex, "加载冷静清单失败");
            // 出错时使用空数据对象作为降级方案
            _cachedData = new WaitingListData();
        }
        finally
        {
            // 释放异步锁
            _lock.Release();
        }
    }

    // 定义私有异步方法 SaveDataAsync，将内存中的数据保存到文件
    private async Task SaveDataAsync()
    {
        // 更新最后修改时间为当前 UTC 时间
        _cachedData!.LastUpdated = DateTime.UtcNow;
        // 将缓存数据序列化为格式化的 JSON 字符串
        var json = JsonSerializer.Serialize(_cachedData, new JsonSerializerOptions { WriteIndented = true });
        // 将 JSON 字符串异步写入到数据文件中
        await File.WriteAllTextAsync(_dataPath, json);
    }
}