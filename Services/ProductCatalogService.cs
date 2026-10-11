// 引入消费者顾问API的模型命名空间
using ConsumerAdvisor.Api.Models;

// 定义消费者顾问API的服务命名空间
namespace ConsumerAdvisor.Api.Services;

// 产品目录服务类，用于管理和查询产品替代方案
public class ProductCatalogService
{
    // 私有只读字段，存储产品目录实例
    private readonly ProductCatalog _catalog;

    // 构造函数，初始化产品目录服务
    public ProductCatalogService()
    {
        // 调用初始化方法创建产品目录数据
        _catalog = InitializeCatalog();
    }

    // 异步查找产品替代方案的方法，接收产品名称作为参数，返回替代产品列表
    public async Task<List<AlternativeProduct>> FindAlternativesAsync(string productName)
    {
        // 异步方法，便于未来扩展为数据库查询
        await Task.CompletedTask;

        // 在目录中查找指定名称的产品
        var product = FindProduct(productName);
        // 检查产品是否存在且有替代方案
        if (product != null && product.Alternatives.Any())
        {
            // 返回找到的替代产品列表，并为每个替代产品标注来源为"商品库"
            return product.Alternatives.Select(a => new AlternativeProduct
            {
                // 替代产品名称
                Name = a.Name,
                // 替代产品价格
                Price = a.Price,
                // 替代产品优点列表
                Pros = a.Pros,
                // 替代产品缺点列表
                Cons = a.Cons,
                // 替代产品适用人群说明
                Suitability = a.Suitability,
                // 标注数据来源为商品库
                Source = "商品库"
            }).ToList();
        }

        // 模糊匹配：从所有分类中查找相似产品的替代方案
        var similarProducts = _catalog.Categories
            // 从每个分类中提取所有产品
            .SelectMany(c => c.Products)
            // 筛选出与搜索名称相似的产品
            .Where(p => IsSimilarProduct(p.Name, productName))
            // 最多取3个相似产品
            .Take(3)
            // 从相似产品中提取所有替代方案
            .SelectMany(p => p.Alternatives)
            // 最多取5个替代方案
            .Take(5)
            // 转换为列表
            .ToList();

        // 返回相似产品的替代方案列表，并标注来源
        return similarProducts.Select(a => new AlternativeProduct
        {
            // 替代产品名称
            Name = a.Name,
            // 替代产品价格
            Price = a.Price,
            // 替代产品优点列表
            Pros = a.Pros,
            // 替代产品缺点列表
            Cons = a.Cons,
            // 替代产品适用人群说明
            Suitability = a.Suitability,
            // 标注数据来源为商品库
            Source = "商品库"
        }).ToList();
    }

    // 私有方法，根据产品名称精确查找产品，返回产品对象或null
    private Product? FindProduct(string name)
    {
        // 从所有分类中提取产品，查找名称包含指定关键字的产品
        return _catalog.Categories
            // 从每个分类中提取所有产品
            .SelectMany(c => c.Products)
            // 查找第一个名称包含关键字或关键字包含产品名称的产品（忽略大小写）
            .FirstOrDefault(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                                name.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
    }

    // 私有方法，判断两个产品名称是否相似，返回布尔值
    private bool IsSimilarProduct(string productName, string searchName)
    {
        // 定义产品关键词数组，用于判断产品相似度
        var keywords = new[] { "手机", "电脑", "相机", "耳机", "音响", "手表", "平板", "笔记本", "吹风机", "吸尘器" };
        // 判断是否满足相似条件：有共同关键词、或一方包含另一方
        return keywords.Any(k =>
            // 两个产品名称都包含同一个关键词
            (productName.Contains(k) && searchName.Contains(k)) ||
            // 产品名称包含搜索名称
            productName.Contains(searchName) ||
            // 搜索名称包含产品名称
            searchName.Contains(productName));
    }

    // 私有方法，初始化产品目录数据，返回产品目录对象
    private ProductCatalog InitializeCatalog()
    {
        // 创建并返回产品目录实例，包含所有分类和产品数据
        return new ProductCatalog
        {
            // 产品分类列表
            Categories = new List<ProductCategory>
            {
                // 第一个分类：数码产品
                new ProductCategory
                {
                    // 分类名称
                    Name = "数码产品",
                    // 分类描述
                    Description = "手机、电脑、相机等电子产品",
                    // 该分类下的产品列表
                    Products = new List<Product>
                    {
                        // 第一个产品：iPhone
                        new Product
                        {
                            // 产品名称
                            Name = "iPhone",
                            // 产品分类
                            Category = "手机",
                            // 典型价格
                            TypicalPrice = 6000,
                            // 产品标签列表
                            Tags = new List<string> { "手机", "苹果", "高端" },
                            // 替代产品列表
                            Alternatives = new List<AlternativeProduct>
                            {
                                // 第一个替代产品：小米旗舰手机
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "小米旗舰手机",
                                    // 替代产品价格
                                    Price = 3000,
                                    // 优点列表
                                    Pros = new List<string> { "性价比高", "系统开放", "充电快" },
                                    // 缺点列表
                                    Cons = new List<string> { "品牌溢价较低" },
                                    // 适用人群
                                    Suitability = "预算有限但需要高性能手机的用户"
                                },
                                // 第二个替代产品：一加手机
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "一加手机",
                                    // 替代产品价格
                                    Price = 3500,
                                    // 优点列表
                                    Pros = new List<string> { "流畅度高", "配置强", "系统简洁" },
                                    // 缺点列表
                                    Cons = new List<string> { "线下店少" },
                                    // 适用人群
                                    Suitability = "追求性能和简洁系统的用户"
                                },
                                // 第三个替代产品：二手旗舰手机
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "二手旗舰手机",
                                    // 替代产品价格
                                    Price = 2500,
                                    // 优点列表
                                    Pros = new List<string> { "价格优惠", "配置高端" },
                                    // 缺点列表
                                    Cons = new List<string> { "无官方保修", "电池损耗" },
                                    // 适用人群
                                    Suitability = "预算有限但追求品质的用户"
                                }
                            }
                        },
                        // 第二个产品：MacBook Pro
                        new Product
                        {
                            // 产品名称
                            Name = "MacBook Pro",
                            // 产品分类
                            Category = "笔记本电脑",
                            // 典型价格
                            TypicalPrice = 15000,
                            // 产品标签列表
                            Tags = new List<string> { "笔记本", "苹果", "专业" },
                            // 替代产品列表
                            Alternatives = new List<AlternativeProduct>
                            {
                                // 第一个替代产品：ThinkPad X1 Carbon
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "ThinkPad X1 Carbon",
                                    // 替代产品价格
                                    Price = 10000,
                                    // 优点列表
                                    Pros = new List<string> { "商务专业", "键盘舒适", "接口丰富" },
                                    // 缺点列表
                                    Cons = new List<string> { "显卡较弱" },
                                    // 适用人群
                                    Suitability = "商务办公用户"
                                },
                                // 第二个替代产品：华为MateBook
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "华为MateBook",
                                    // 替代产品价格
                                    Price = 6000,
                                    // 优点列表
                                    Pros = new List<string> { "性价比高", "多屏协同", "轻薄" },
                                    // 缺点列表
                                    Cons = new List<string> { "生态系统局限" },
                                    // 适用人群
                                    Suitability = "华为手机用户"
                                },
                                // 第三个替代产品：ThinkBook 14+
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "ThinkBook 14+",
                                    // 替代产品价格
                                    Price = 5000,
                                    // 优点列表
                                    Pros = new List<string> { "性能均衡", "扩展性好", "性价比高" },
                                    // 缺点列表
                                    Cons = new List<string> { "重量略重" },
                                    // 适用人群
                                    Suitability = "学生和办公用户"
                                }
                            }
                        },
                        // 第三个产品：戴森吹风机
                        new Product
                        {
                            // 产品名称
                            Name = "戴森吹风机",
                            // 产品分类
                            Category = "个护电器",
                            // 典型价格
                            TypicalPrice = 3000,
                            // 产品标签列表
                            Tags = new List<string> { "吹风机", "戴森", "高端" },
                            // 替代产品列表
                            Alternatives = new List<AlternativeProduct>
                            {
                                // 第一个替代产品：徕芬吹风机
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "徕芬吹风机",
                                    // 替代产品价格
                                    Price = 600,
                                    // 优点列表
                                    Pros = new List<string> { "高速吹干", "价格实惠", "风量大" },
                                    // 缺点列表
                                    Cons = new List<string> { "品牌知名度低" },
                                    // 适用人群
                                    Suitability = "追求性价比的用户"
                                },
                                // 第二个替代产品：松下吹风机
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "松下吹风机",
                                    // 替代产品价格
                                    Price = 800,
                                    // 优点列表
                                    Pros = new List<string> { "护发技术", "品质可靠" },
                                    // 缺点列表
                                    Cons = new List<string> { "体积较大" },
                                    // 适用人群
                                    Suitability = "注重护发效果的用户"
                                }
                            }
                        }
                    }
                },
                // 第二个分类：家居用品
                new ProductCategory
                {
                    // 分类名称
                    Name = "家居用品",
                    // 分类描述
                    Description = "家具、家电、厨具等",
                    // 该分类下的产品列表
                    Products = new List<Product>
                    {
                        // 第一个产品：戴森吸尘器
                        new Product
                        {
                            // 产品名称
                            Name = "戴森吸尘器",
                            // 产品分类
                            Category = "清洁电器",
                            // 典型价格
                            TypicalPrice = 5000,
                            // 产品标签列表
                            Tags = new List<string> { "吸尘器", "戴森", "高端" },
                            // 替代产品列表
                            Alternatives = new List<AlternativeProduct>
                            {
                                // 第一个替代产品：小米无线吸尘器
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "小米无线吸尘器",
                                    // 替代产品价格
                                    Price = 1500,
                                    // 优点列表
                                    Pros = new List<string> { "性价比高", "功能齐全", "智能控制" },
                                    // 缺点列表
                                    Cons = new List<string> { "吸力略弱" },
                                    // 适用人群
                                    Suitability = "普通家庭清洁需求"
                                },
                                // 第二个替代产品：石头扫地机器人
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "石头扫地机器人",
                                    // 替代产品价格
                                    Price = 2500,
                                    // 优点列表
                                    Pros = new List<string> { "自动清扫", "智能规划", "省时省力" },
                                    // 缺点列表
                                    Cons = new List<string> { "无法处理大颗粒" },
                                    // 适用人群
                                    Suitability = "忙碌的上班族"
                                }
                            }
                        },
                        // 第二个产品：SK-II护肤品套装
                        new Product
                        {
                            // 产品名称
                            Name = "SK-II护肤品套装",
                            // 产品分类
                            Category = "护肤美妆",
                            // 典型价格
                            TypicalPrice = 2000,
                            // 产品标签列表
                            Tags = new List<string> { "护肤品", "高端", "护肤" },
                            // 替代产品列表
                            Alternatives = new List<AlternativeProduct>
                            {
                                // 第一个替代产品：平价护肤套装
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "平价护肤套装",
                                    // 替代产品价格
                                    Price = 500,
                                    // 优点列表
                                    Pros = new List<string> { "价格实惠", "基础护肤够用" },
                                    // 缺点列表
                                    Cons = new List<string> { "功效较弱" },
                                    // 适用人群
                                    Suitability = "学生和预算有限人群"
                                },
                                // 第二个替代产品：国货护肤品牌
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "国货护肤品牌",
                                    // 替代产品价格
                                    Price = 800,
                                    // 优点列表
                                    Pros = new List<string> { "性价比高", "适合国人肤质" },
                                    // 缺点列表
                                    Cons = new List<string> { "知名度较低" },
                                    // 适用人群
                                    Suitability = "支持国货的用户"
                                }
                            }
                        }
                    }
                },
                // 第三个分类：运动健身
                new ProductCategory
                {
                    // 分类名称
                    Name = "运动健身",
                    // 分类描述
                    Description = "运动器材、健身装备",
                    // 该分类下的产品列表
                    Products = new List<Product>
                    {
                        // 第一个产品：高端跑步机
                        new Product
                        {
                            // 产品名称
                            Name = "高端跑步机",
                            // 产品分类
                            Category = "健身器材",
                            // 典型价格
                            TypicalPrice = 10000,
                            // 产品标签列表
                            Tags = new List<string> { "跑步机", "健身", "高端" },
                            // 替代产品列表
                            Alternatives = new List<AlternativeProduct>
                            {
                                // 第一个替代产品：Keep跑步机
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "Keep跑步机",
                                    // 替代产品价格
                                    Price = 3000,
                                    // 优点列表
                                    Pros = new List<string> { "课程丰富", "APP联动", "价格适中" },
                                    // 缺点列表
                                    Cons = new List<string> { "承重有限" },
                                    // 适用人群
                                    Suitability = "健身初学者"
                                },
                                // 第二个替代产品：户外跑步
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "户外跑步",
                                    // 替代产品价格
                                    Price = 0,
                                    // 优点列表
                                    Pros = new List<string> { "免费", "锻炼心肺", "环境好" },
                                    // 缺点列表
                                    Cons = new List<string> { "受天气影响" },
                                    // 适用人群
                                    Suitability = "所有人群"
                                }
                            }
                        },
                        // 第二个产品：健身私教课
                        new Product
                        {
                            // 产品名称
                            Name = "健身私教课",
                            // 产品分类
                            Category = "健身服务",
                            // 典型价格
                            TypicalPrice = 500,
                            // 产品标签列表
                            Tags = new List<string> { "私教", "健身", "服务" },
                            // 替代产品列表
                            Alternatives = new List<AlternativeProduct>
                            {
                                // 第一个替代产品：健身APP会员
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "健身APP会员",
                                    // 替代产品价格
                                    Price = 30,
                                    // 优点列表
                                    Pros = new List<string> { "价格低", "随时可练" },
                                    // 缺点列表
                                    Cons = new List<string> { "无专人指导" },
                                    // 适用人群
                                    Suitability = "自律性强的用户"
                                },
                                // 第二个替代产品：健身房月卡
                                new AlternativeProduct
                                {
                                    // 替代产品名称
                                    Name = "健身房月卡",
                                    // 替代产品价格
                                    Price = 200,
                                    // 优点列表
                                    Pros = new List<string> { "器械齐全", "氛围好" },
                                    // 缺点列表
                                    Cons = new List<string> { "需到场" },
                                    // 适用人群
                                    Suitability = "喜欢健身房氛围的用户"
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}