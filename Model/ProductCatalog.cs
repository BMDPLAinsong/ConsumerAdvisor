// 定义消费者顾问 API 模型的命名空间
namespace ConsumerAdvisor.Api.Models;

// 商品目录模型，包含所有商品分类
public class ProductCatalog
{
    // 商品分类列表
    public List<ProductCategory> Categories { get; set; } = new();
}

// 商品分类模型
public class ProductCategory
{
    // 分类名称
    public string Name { get; set; } = string.Empty;
    // 分类描述
    public string Description { get; set; } = string.Empty;
    // 该分类下的商品列表
    public List<Product> Products { get; set; } = new();
}

// 商品模型
public class Product
{
    // 商品名称
    public string Name { get; set; } = string.Empty;
    // 商品分类
    public string Category { get; set; } = string.Empty;
    // 典型价格
    public decimal? TypicalPrice { get; set; }
    // 该商品的替代方案列表
    public List<AlternativeProduct> Alternatives { get; set; } = new();
    // 商品标签，如"高频使用"、"性价比"等
    public List<string> Tags { get; set; } = new();
}