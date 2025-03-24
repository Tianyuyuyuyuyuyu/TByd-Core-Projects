using System.Text;
using NUnit.Framework;
using TByd.Core.Utils.Runtime;
using TByd.Core.Utils.Tests.Editor.Framework;
using Unity.PerformanceTesting;
// 假设StringUtils位于此命名空间

namespace TByd.Core.Utils.Tests.Editor.Performance
{
    /// <summary>
    /// StringUtils类的性能测试
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class StringUtilsPerformanceTests : PerformanceTestBase
    {
        private const int BenchmarkStringLength = 10000;
        private string _testString;
        private string _commaDelimitedString;
        
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            
            // 准备测试数据
            _testString = TestDataGenerator.GenerateString(BenchmarkStringLength);
            
            // 创建包含1000个逗号分隔项的字符串
            var sb = new StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                sb.Append(TestDataGenerator.GenerateString(10));
                if (i < 999) sb.Append(',');
            }
            _commaDelimitedString = sb.ToString();
        }
        
        /// <summary>
        /// 测试StringUtils.IsNullOrWhiteSpace的性能，与string.IsNullOrWhiteSpace比较
        /// </summary>
        [Test, Performance]
        public void IsNullOrWhiteSpace_Performance()
        {
            // 创建测试数据 - 10000个随机字符串，有10%概率为空或空白
            var strings = new string[10000];
            var random = new System.Random(42); // 固定种子以确保重复测试的一致性
            
            for (int i = 0; i < strings.Length; i++)
            {
                if (random.NextDouble() < 0.1) // 10%概率为空或空白
                {
                    strings[i] = random.Next(3) == 0 ? null : 
                                 random.Next(3) == 1 ? string.Empty : "   ";
                }
                else
                {
                    strings[i] = TestDataGenerator.GenerateString(random.Next(1, 20));
                }
            }
            
            // 比较性能
            ComparePerformance(
                // 基准实现 - 系统字符串方法
                () => {
                    for (int i = 0; i < strings.Length; i++)
                    {
                        _ = string.IsNullOrWhiteSpace(strings[i]);
                    }
                },
                
                // 优化实现 - 我们的StringUtils方法
                () => {
                    for (int i = 0; i < strings.Length; i++)
                    {
                        _ = StringUtils.IsNullOrWhiteSpace(strings[i]);
                    }
                },
                
                "IsNullOrWhiteSpace性能比较"
            );
        }
        
        /// <summary>
        /// 测试StringUtils.Split方法的性能，与string.Split比较
        /// </summary>
        [Test, Performance]
        public void Split_Performance()
        {
            ComparePerformance(
                // 基准实现 - 系统字符串分割
                () => {
                    string[] parts = _commaDelimitedString.Split(',');
                    // 使用结果以防止编译器优化掉操作
                    for (int i = 0; i < parts.Length; i++)
                    {
                        _ = parts[i].Length;
                    }
                },
                
                // 优化实现 - 我们的StringUtils分割器(使用Span实现的零分配版本)
                () => {
                    // 假设我们有一个高效的分割方法
                    foreach (var part in StringUtils.Split(_commaDelimitedString, ','))
                    {
                        _ = part.Length;
                    }
                },
                
                "Split性能比较",
                true, // 测量GC分配
                5    // 减少测量次数，因为这是一个相对较大的操作
            );
        }
        
        /// <summary>
        /// 测试StringUtils.GenerateRandom方法的性能
        /// </summary>
        [Test, Performance]
        public void GenerateRandom_Performance()
        {
            // 不同长度的随机字符串生成性能
            var lengths = new[] { 8, 16, 32, 64, 128 };
            
            foreach (int length in lengths)
            {
                // 测量StringUtils.GenerateRandom的性能
                MeasurePerformance(
                    () => {
                        for (int i = 0; i < 100; i++)
                        {
                            _ = StringUtils.GenerateRandom(length);
                        }
                    },
                    $"GenerateRandom(length={length})"
                );
            }
            
            // 测量包含特殊字符时的性能
            MeasurePerformance(
                () => {
                    for (int i = 0; i < 100; i++)
                    {
                        _ = StringUtils.GenerateRandom(32, includeSpecialChars: true);
                    }
                },
                "GenerateRandom(includeSpecialChars=true)"
            );
        }
        
        /// <summary>
        /// 测试StringUtils.Truncate方法的性能
        /// </summary>
        [Test, Performance]
        public void Truncate_Performance()
        {
            // 准备一批不同长度的字符串
            var strings = new string[100];
            for (int i = 0; i < strings.Length; i++)
            {
                strings[i] = TestDataGenerator.GenerateString(i * 20 + 50); // 50到2050长度的字符串
            }
            
            // 测试不同截断长度的性能
            var truncateLengths = new[] { 10, 50, 100, 500 };
            
            foreach (int truncateLength in truncateLengths)
            {
                // 测量StringUtils.Truncate的性能
                MeasurePerformance(
                    () => {
                        for (int i = 0; i < strings.Length; i++)
                        {
                            _ = StringUtils.Truncate(strings[i], truncateLength);
                        }
                    },
                    $"Truncate(maxLength={truncateLength})"
                );
            }
            
            // 测试自定义后缀的性能影响
            MeasurePerformance(
                () => {
                    for (int i = 0; i < strings.Length; i++)
                    {
                        _ = StringUtils.Truncate(strings[i], 100, "...查看更多");
                    }
                },
                "Truncate(自定义后缀)"
            );
        }
        
        /// <summary>
        /// 测试StringUtils.ToSlug方法的性能
        /// </summary>
        [Test, Performance]
        public void ToSlug_Performance()
        {
            // 准备测试数据 - 不同类型的字符串
            string[] testCases = {
                "Hello World! This is a Test",              // 英文
                "你好，世界！这是一个测试",                      // 中文
                "Hello 123 World! 你好，测试 #$%^ 特殊字符",    // 混合
                TestDataGenerator.GenerateString(500),      // 长字符串
                TestDataGenerator.GenerateString(1000),     // 更长的字符串
            };
            
            foreach (string testCase in testCases)
            {
                string testName = testCase.Length <= 20 ? 
                    $"ToSlug(\"{testCase}\")" : 
                    $"ToSlug(长度={testCase.Length}的字符串)";
                
                // 测量StringUtils.ToSlug的性能
                MeasurePerformance(
                    () => {
                        for (int i = 0; i < 100; i++)
                        {
                            _ = StringUtils.ToSlug(testCase);
                        }
                    },
                    testName
                );
            }
        }
        
        /// <summary>
        /// 测试中文字符串处理的性能
        /// </summary>
        [Test, Performance]
        public void ChineseString_Processing_Performance()
        {
            // 生成包含中文的测试字符串
            string chineseString = TestDataGenerator.GenerateChineseString(1000);
            
            // 测量中文字符串的Truncate性能
            MeasurePerformance(
                () => {
                    for (int i = 0; i < 100; i++)
                    {
                        _ = StringUtils.Truncate(chineseString, 100);
                    }
                },
                "Truncate(中文字符串)"
            );
            
            // 测量中文字符串的ToSlug性能
            MeasurePerformance(
                () => {
                    for (int i = 0; i < 20; i++) // 减少次数，因为这是个重操作
                    {
                        _ = StringUtils.ToSlug(chineseString);
                    }
                },
                "ToSlug(中文字符串)"
            );
        }
    }
} 