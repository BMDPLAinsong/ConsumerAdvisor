// 引入日志服务命名空间
using Microsoft.Extensions.Logging;
// 引入语义内核命名空间，用于AI解析
using Microsoft.SemanticKernel;
// 引入JSON序列化命名空间
using System.Text.Json;
// 引入正则表达式命名空间
using System.Text.RegularExpressions;

// 定义服务所在的命名空间
namespace ConsumerAdvisor.Api.Services;

// 定义输入解析服务类，负责解析用户输入的商品和购买原因
public class InputParserService
{
    // 语义内核实例，用于AI解析（可为空）
    private readonly Kernel? _kernel;
    // 日志记录器实例
    private readonly ILogger<InputParserService> _logger;

    // 静态正则表达式列表，用于匹配用户购买意图的各种句式
    private static readonly List<Regex> PurchasePatterns = new()
    {
        // 匹配"我想买/购xxx，因为/由于/为了/想xxx"格式
        new Regex(@"我想(?:买|购)(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"我要买/购xxx，因为/由于/为了/想xxx"格式
        new Regex(@"我要(?:买|购)(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"打算买/购xxx，因为/由于/为了/想xxx"格式
        new Regex(@"打算(?:买|购)(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"准备买/购xxx，因为/由于/为了/想xxx"格式
        new Regex(@"准备(?:买|购)(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"想买xxx，因为/由于/为了/想xxx"格式
        new Regex(@"想买(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"要买xxx，因为/由于/为了/想xxx"格式
        new Regex(@"要买(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"购买xxx，因为/由于/为了/想xxx"格式
        new Regex(@"购买(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"想入手xxx，因为/由于/为了/想xxx"格式
        new Regex(@"想入手(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"入手xxx，因为/由于/为了/想xxx"格式
        new Regex(@"入手(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"看中xxx，因为/由于/为了/想xxx"格式
        new Regex(@"看中(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"看上xxx，因为/由于/为了/想xxx"格式
        new Regex(@"看上(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"考虑买/购xxx，因为/由于/为了/想xxx"格式
        new Regex(@"考虑(?:买|购)(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"纠结买/购xxx，因为/由于/为了/想xxx"格式
        new Regex(@"纠结(?:买|购)(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"想换xxx，因为/由于/为了/想xxx"格式
        new Regex(@"想换(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"换xxx，因为/由于/为了/想xxx"格式
        new Regex(@"换(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"需要xxx，因为/由于/为了/想xxx"格式
        new Regex(@"需要(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"想要xxx，因为/由于/为了/想xxx"格式
        new Regex(@"想要(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"缺xxx，因为/由于/为了/想xxx"格式
        new Regex(@"缺(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"应该买xxx，因为/由于/为了/想xxx"格式
        new Regex(@"应该买(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 匹配"该不该买xxx，因为/由于/为了/想xxx"格式
        new Regex(@"该不该买(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase),
        // 通用兜底正则，匹配任意文本加原因的格式
        new Regex(@"(.*?)[，,。\s]+(?:因为|由于|为了|想)(.*)", RegexOptions.IgnoreCase)
    };

    // 构造函数，初始化日志和内核实例
    public InputParserService(ILogger<InputParserService> logger, Kernel? kernel = null)
    {
        // 保存日志实例
        _logger = logger;
        // 保存内核实例（可选）
        _kernel = kernel;
    }

    // 解析用户输入的主方法，返回解析结果
    public ParsedInput Parse(string input)
    {
        // 检查输入是否为空或仅包含空白字符
        if (string.IsNullOrWhiteSpace(input))
            // 返回解析失败结果，提示输入不能为空
            return new ParsedInput { Success = false, ErrorMessage = "输入不能为空" };

        // 去除输入字符串的首尾空白
        var trimmedInput = input.Trim();

        // 遍历所有预设的正则表达式模式
        foreach (var pattern in PurchasePatterns)
        {
            // 使用当前正则模式匹配输入文本
            var match = pattern.Match(trimmedInput);
            // 检查匹配是否成功
            if (match.Success)
            {
                // 提取第一个捕获组（商品名称）并去除首尾空白
                var product = match.Groups[1].Value.Trim();
                // 提取第二个捕获组（购买原因）并去除首尾空白
                var reason = match.Groups[2].Value.Trim();

                // 验证商品和原因都非空
                if (!string.IsNullOrEmpty(product) && !string.IsNullOrEmpty(reason))
                {
                    // 记录日志：正则解析成功
                    _logger.LogInformation("正则解析成功: 商品={Product}, 原因={Reason}", product, reason);
                    // 返回解析成功的结果，标记使用正则解析方法
                    return new ParsedInput { Success = true, Product = product, Reason = reason, Method = "Regex" };
                }
            }
        }

        // 所有正则都未匹配，使用备用解析策略
        return ParseWithFallback(trimmedInput);
    }

    // 备用解析方法，通过分隔符和长度分割来解析
    private ParsedInput ParseWithFallback(string input)
    {
        // 定义可能的分隔符数组
        var separators = new[] { "，", ",", "。", ".", "因为", "由于", "为了", "想", "觉得", "所以" };
        // 遍历所有分隔符
        foreach (var separator in separators)
        {
            // 查找分隔符在输入中的位置（忽略大小写）
            var index = input.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            // 检查分隔符位置是否有效（不在开头且不在末尾）
            if (index > 0 && index < input.Length - 1)
            {
                // 提取分隔符前的部分作为商品名
                var product = input.Substring(0, index).Trim();
                // 提取分隔符后的部分作为原因
                var reason = input.Substring(index + separator.Length).Trim();

                // 验证商品和原因都非空
                if (!string.IsNullOrEmpty(product) && !string.IsNullOrEmpty(reason))
                {
                    // 清洗商品名称，去除前缀
                    product = CleanProductName(product);
                    // 清洗原因文本，去除前缀
                    reason = CleanReason(reason);

                    // 再次验证清洗后的商品和原因都非空
                    if (!string.IsNullOrEmpty(product) && !string.IsNullOrEmpty(reason))
                    {
                        // 记录日志：分隔符解析成功
                        _logger.LogInformation("分隔符解析成功: 商品={Product}, 原因={Reason}", product, reason);
                        // 返回解析成功的结果，标记使用分隔符解析方法
                        return new ParsedInput { Success = true, Product = product, Reason = reason, Method = "Separator" };
                    }
                }
            }
        }

        // 尝试按长度分割进行解析
        var splitResult = SplitByLength(input);
        // 检查长度分割解析是否成功
        if (splitResult.Success)
        {
            // 记录日志：长度分割解析成功
            _logger.LogInformation("长度分割解析成功: 商品={Product}, 原因={Reason}", splitResult.Product, splitResult.Reason);
            // 返回长度分割解析的结果
            return splitResult;
        }

        // 所有解析方式都失败，返回失败结果
        return new ParsedInput { Success = false, ErrorMessage = "无法解析输入，请按格式输入：我想买{商品}，因为{原因}" };
    }

    // 清洗商品名称的辅助方法，去除购买意图前缀
    private string CleanProductName(string product)
    {
        // 定义需要去除的前缀列表
        var prefixes = new[] { "我想", "我要", "打算", "准备", "想", "要", "买", "购", "入手", "看中", "看上", "考虑", "纠结", "换", "需要", "想要", "缺", "应该", "该", "不该" };
        // 遍历所有前缀
        foreach (var prefix in prefixes)
        {
            // 检查商品名是否以当前前缀开头（忽略大小写）
            if (product.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // 去除前缀并重新赋值
                product = product.Substring(prefix.Length).Trim();
                // 找到匹配的前缀后跳出循环
                break;
            }
        }
        // 返回清洗后的商品名，去除首尾标点和空格
        return product.Trim('，', ',', '。', '.', ' ');
    }

    // 清洗购买原因的辅助方法，去除原因前缀
    private string CleanReason(string reason)
    {
        // 定义需要去除的原因前缀列表
        var prefixes = new[] { "因为", "由于", "为了", "想", "觉得", "所以" };
        // 遍历所有前缀
        foreach (var prefix in prefixes)
        {
            // 检查原因是否以当前前缀开头（忽略大小写）
            if (reason.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // 去除前缀并重新赋值
                reason = reason.Substring(prefix.Length).Trim();
                // 找到匹配的前缀后跳出循环
                break;
            }
        }
        // 返回清洗后的原因文本，去除首尾标点和空格
        return reason.Trim('，', ',', '。', '.', ' ');
    }

    // 按长度分割的解析方法，尝试找到商品和原因的分界点
    private ParsedInput SplitByLength(string input)
    {
        // 去除输入首尾的标点符号和空格
        var trimmed = input.Trim('，', ',', '。', '.', ' ');
        // 检查输入长度是否过短（小于4字符无法分割）
        if (trimmed.Length < 4)
            // 返回解析失败
            return new ParsedInput { Success = false };

        // 查找第一个标点符号的位置
        var firstPeriod = trimmed.IndexOfAny(new[] { '，', ',', '。', '.' });
        // 如果找到标点符号
        if (firstPeriod > 0)
        {
            // 标点前部分作为商品名
            var product = trimmed.Substring(0, firstPeriod).Trim();
            // 标点后部分作为原因
            var reason = trimmed.Substring(firstPeriod + 1).Trim();
            // 验证商品和原因都非空
            if (!string.IsNullOrEmpty(product) && !string.IsNullOrEmpty(reason))
            {
                // 清洗并返回结果，标记使用分割解析方法
                return new ParsedInput { Success = true, Product = CleanProductName(product), Reason = CleanReason(reason), Method = "Split" };
            }
        }

        // 没有标点符号时，按长度对半分割
        var halfIndex = trimmed.Length / 2;
        // 前半部分作为商品名
        var p = trimmed.Substring(0, halfIndex).Trim();
        // 后半部分作为原因
        var r = trimmed.Substring(halfIndex).Trim();
        // 验证商品和原因都非空
        if (!string.IsNullOrEmpty(p) && !string.IsNullOrEmpty(r))
        {
            // 清洗并返回结果，标记使用分割解析方法
            return new ParsedInput { Success = true, Product = CleanProductName(p), Reason = CleanReason(r), Method = "Split" };
        }

        // 分割失败，返回解析失败结果
        return new ParsedInput { Success = false };
    }

    // 使用AI语义内核进行异步解析的方法
    public async Task<ParsedInput> ParseWithAIAsync(string input)
    {
        // 检查内核是否为空
        if (_kernel == null)
            // 内核不可用，回退到同步正则解析
            return Parse(input);

        // 构建AI提示词，要求提取商品和原因并输出JSON格式
        var prompt = $@"
请从以下用户输入中提取商品名称和购买原因。输出严格JSON格式：
{{""Product"":""商品名称"",""Reason"":""购买原因""}}

用户输入：{input}
";

        try
        {
            // 调用AI内核执行提示词
            var response = await _kernel.InvokePromptAsync(prompt);
            // 获取AI响应的字符串表示
            var raw = response.ToString();
            // 清理AI返回的文本，去除Markdown代码块标记
            var cleaned = Regex.Replace(raw, @"^```(?:json)?\s*|\s*```$", "", RegexOptions.Multiline).Trim();
            // 反序列化JSON为AI解析结果对象（忽略属性名大小写）
            var result = JsonSerializer.Deserialize<AiParseResult>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // 验证解析结果有效
            if (result != null && !string.IsNullOrEmpty(result.Product) && !string.IsNullOrEmpty(result.Reason))
            {
                // 记录日志：AI解析成功
                _logger.LogInformation("AI解析成功: 商品={Product}, 原因={Reason}", result.Product, result.Reason);
                // 返回AI解析成功的结果，标记使用AI解析方法
                return new ParsedInput { Success = true, Product = result.Product.Trim(), Reason = result.Reason.Trim(), Method = "AI" };
            }
        }
        catch (Exception ex)
        {
            // AI解析失败，记录警告日志
            _logger.LogWarning(ex, "AI解析失败，回退到正则");
        }

        // AI解析失败或结果无效，回退到正则解析
        return Parse(input);
    }

    // AI解析结果的内部类，用于JSON反序列化
    private class AiParseResult
    {
        // 商品名称属性，默认为空字符串
        public string Product { get; set; } = string.Empty;
        // 购买原因属性，默认为空字符串
        public string Reason { get; set; } = string.Empty;
    }
}

// 解析结果模型类，对外暴露
public class ParsedInput
{
    // 解析是否成功
    public bool Success { get; set; }
    // 解析出的商品名称
    public string Product { get; set; } = string.Empty;
    // 解析出的购买原因
    public string Reason { get; set; } = string.Empty;
    // 解析失败时的错误信息
    public string ErrorMessage { get; set; } = string.Empty;
    // 使用的解析方法（正则/分隔符/分割/AI）
    public string Method { get; set; } = string.Empty;
}