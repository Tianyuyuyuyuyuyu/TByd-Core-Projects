using System;
using UnityEngine;
using NUnit.Framework;

namespace TByd.Core.Utils.Editor.Tests.Framework
{
    /// <summary>
    /// GC分配测试工具，用于测量方法的内存分配情况
    /// </summary>
    public static class GCAllocationTester
    {
        /// <summary>
        /// 测量操作的GC内存分配量
        /// </summary>
        /// <param name="action">待测量的操作</param>
        /// <returns>分配的内存字节数</returns>
        public static long MeasureAllocation(Action action)
        {
            // 强制进行GC收集
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // 记录初始内存使用量
            long startMemory = GC.GetTotalMemory(true);
            
            // 执行操作
            action();
            
            // 记录操作后的内存使用量
            long endMemory = GC.GetTotalMemory(false);
            
            // 返回差值即为分配的内存量
            return endMemory - startMemory;
        }
        
        /// <summary>
        /// 断言方法不产生任何GC分配
        /// </summary>
        /// <param name="action">待测试的方法</param>
        /// <param name="allowedBytes">允许的少量分配（默认为0）</param>
        public static void AssertNoAllocation(Action action, long allowedBytes = 0)
        {
            long allocated = MeasureAllocation(action);
            
            Assert.That(allocated, Is.LessThanOrEqualTo(allowedBytes),
                $"预期无GC分配，但实际分配了 {allocated} 字节");
        }
        
        /// <summary>
        /// 断言方法的GC分配量小于指定值
        /// </summary>
        /// <param name="action">待测试的方法</param>
        /// <param name="maxAllowedBytes">最大允许分配字节数</param>
        public static void AssertAllocationLessThan(Action action, long maxAllowedBytes)
        {
            long allocated = MeasureAllocation(action);
            
            Assert.That(allocated, Is.LessThan(maxAllowedBytes),
                $"GC分配 {allocated} 字节超过了允许的最大值 {maxAllowedBytes} 字节");
        }
        
        /// <summary>
        /// 运行性能比较，比较两个实现的内存分配情况
        /// </summary>
        /// <param name="baseline">基准实现</param>
        /// <param name="optimized">优化实现</param>
        /// <param name="expectedImprovement">期望的改进比例（0-1之间）</param>
        public static void CompareAllocations(Action baseline, Action optimized, float expectedImprovement)
        {
            // 预热
            baseline();
            optimized();
            
            // 测量基准版本
            long baselineAllocation = MeasureAllocation(baseline);
            
            // 测量优化版本
            long optimizedAllocation = MeasureAllocation(optimized);
            
            // 计算改进比例
            float actualImprovement = 1.0f;
            if (baselineAllocation > 0)
            {
                actualImprovement = 1.0f - ((float)optimizedAllocation / baselineAllocation);
            }
            
            Debug.Log($"内存分配比较：基准版本 {baselineAllocation} 字节，优化版本 {optimizedAllocation} 字节");
            Debug.Log($"改进比例：{actualImprovement * 100}%，期望 {expectedImprovement * 100}%");
            
            Assert.That(actualImprovement, Is.GreaterThanOrEqualTo(expectedImprovement),
                $"内存分配优化不足，实际改进比例 {actualImprovement * 100}%，期望至少 {expectedImprovement * 100}%");
        }
        
        /// <summary>
        /// 运行每K项元素的GC分配测试
        /// </summary>
        /// <param name="itemCount">测试项数量</param>
        /// <param name="action">每K项的操作</param>
        /// <param name="maxBytesPerK">每K项最大允许的字节数</param>
        public static void AssertAllocationPerK(int itemCount, Action action, long maxBytesPerK)
        {
            long allocated = MeasureAllocation(action);
            
            // 计算每千项的分配量
            float allocationPerK = (float)allocated / (itemCount / 1000.0f);
            
            Debug.Log($"每K项GC分配：{allocationPerK:F2} 字节，总分配 {allocated} 字节，项数 {itemCount}");
            
            Assert.That(allocationPerK, Is.LessThanOrEqualTo(maxBytesPerK),
                $"每K项GC分配 {allocationPerK:F2} 字节超过了允许的 {maxBytesPerK} 字节");
        }
    }
} 