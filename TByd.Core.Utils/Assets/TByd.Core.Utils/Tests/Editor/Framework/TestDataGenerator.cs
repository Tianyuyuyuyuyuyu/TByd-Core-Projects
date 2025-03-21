using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TByd.Core.Utils.Editor.Tests.Framework
{
    /// <summary>
    /// 测试数据生成器，用于生成各种类型的测试数据
    /// </summary>
    public static class TestDataGenerator
    {
        private static readonly System.Random Random = new System.Random();
        
        /// <summary>
        /// 生成指定长度的随机字符串
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <param name="includeSpecialChars">是否包含特殊字符</param>
        /// <returns>随机字符串</returns>
        public static string GenerateString(int length, bool includeSpecialChars = false)
        {
            const string alphanumeric = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            const string special = "!@#$%^&*()_-+=<>?";
            string chars = includeSpecialChars ? alphanumeric + special : alphanumeric;
            
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[Random.Next(chars.Length)]);
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 生成随机整数
        /// </summary>
        /// <param name="min">最小值（含）</param>
        /// <param name="max">最大值（不含）</param>
        /// <returns>随机整数</returns>
        public static int GenerateInt(int min = 0, int max = 100)
        {
            return Random.Next(min, max);
        }
        
        /// <summary>
        /// 生成随机浮点数
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns>随机浮点数</returns>
        public static float GenerateFloat(float min = 0f, float max = 1f)
        {
            return (float)(Random.NextDouble() * (max - min) + min);
        }
        
        /// <summary>
        /// 生成随机布尔值
        /// </summary>
        /// <param name="trueChance">true的概率（0-1）</param>
        /// <returns>随机布尔值</returns>
        public static bool GenerateBool(float trueChance = 0.5f)
        {
            return Random.NextDouble() < trueChance;
        }
        
        /// <summary>
        /// 生成随机日期时间
        /// </summary>
        /// <param name="minYear">最小年份</param>
        /// <param name="maxYear">最大年份</param>
        /// <returns>随机日期时间</returns>
        public static DateTime GenerateDateTime(int minYear = 2000, int maxYear = 2030)
        {
            int year = Random.Next(minYear, maxYear + 1);
            int month = Random.Next(1, 13);
            int day = Random.Next(1, DateTime.DaysInMonth(year, month) + 1);
            int hour = Random.Next(0, 24);
            int minute = Random.Next(0, 60);
            int second = Random.Next(0, 60);
            
            return new DateTime(year, month, day, hour, minute, second);
        }
        
        /// <summary>
        /// 生成随机Vector2
        /// </summary>
        /// <param name="min">各分量最小值</param>
        /// <param name="max">各分量最大值</param>
        /// <returns>随机Vector2</returns>
        public static Vector2 GenerateVector2(float min = -10f, float max = 10f)
        {
            return new Vector2(
                GenerateFloat(min, max),
                GenerateFloat(min, max)
            );
        }
        
        /// <summary>
        /// 生成随机Vector3
        /// </summary>
        /// <param name="min">各分量最小值</param>
        /// <param name="max">各分量最大值</param>
        /// <returns>随机Vector3</returns>
        public static Vector3 GenerateVector3(float min = -10f, float max = 10f)
        {
            return new Vector3(
                GenerateFloat(min, max),
                GenerateFloat(min, max),
                GenerateFloat(min, max)
            );
        }
        
        /// <summary>
        /// 生成随机Color
        /// </summary>
        /// <param name="randomAlpha">是否生成随机透明度</param>
        /// <returns>随机Color</returns>
        public static Color GenerateColor(bool randomAlpha = false)
        {
            return new Color(
                GenerateFloat(0f, 1f),
                GenerateFloat(0f, 1f),
                GenerateFloat(0f, 1f),
                randomAlpha ? GenerateFloat(0f, 1f) : 1f
            );
        }
        
        /// <summary>
        /// 生成随机数组
        /// </summary>
        /// <typeparam name="T">数组元素类型</typeparam>
        /// <param name="count">数组长度</param>
        /// <param name="generator">元素生成函数</param>
        /// <returns>随机数组</returns>
        public static T[] GenerateArray<T>(int count, Func<int, T> generator)
        {
            var array = new T[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = generator(i);
            }
            return array;
        }
        
        /// <summary>
        /// 生成随机列表
        /// </summary>
        /// <typeparam name="T">列表元素类型</typeparam>
        /// <param name="count">列表长度</param>
        /// <param name="generator">元素生成函数</param>
        /// <returns>随机列表</returns>
        public static List<T> GenerateList<T>(int count, Func<int, T> generator)
        {
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(generator(i));
            }
            return list;
        }
        
        /// <summary>
        /// 生成随机字典
        /// </summary>
        /// <typeparam name="TKey">键类型</typeparam>
        /// <typeparam name="TValue">值类型</typeparam>
        /// <param name="count">字典元素数量</param>
        /// <param name="keyGenerator">键生成函数</param>
        /// <param name="valueGenerator">值生成函数</param>
        /// <returns>随机字典</returns>
        public static Dictionary<TKey, TValue> GenerateDictionary<TKey, TValue>(
            int count, Func<int, TKey> keyGenerator, Func<int, TValue> valueGenerator)
        {
            var dict = new Dictionary<TKey, TValue>(count);
            for (int i = 0; i < count; i++)
            {
                dict[keyGenerator(i)] = valueGenerator(i);
            }
            return dict;
        }
        
        /// <summary>
        /// 生成随机中文字符串
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <returns>随机中文字符串</returns>
        public static string GenerateChineseString(int length)
        {
            var sb = new StringBuilder(length);
            // 中文字符范围（常用汉字范围）
            for (int i = 0; i < length; i++)
            {
                // 生成随机常用汉字（0x4E00-0x9FA5）
                char ch = (char)Random.Next(0x4E00, 0x9FA5 + 1);
                sb.Append(ch);
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// 生成一个大文本块，用于测试文本处理性能
        /// </summary>
        /// <param name="paragraphCount">段落数</param>
        /// <param name="sentencesPerParagraph">每段句子数</param>
        /// <param name="averageWordsPerSentence">每句平均单词数</param>
        /// <returns>生成的大文本块</returns>
        public static string GenerateLargeText(int paragraphCount = 10, 
                                               int sentencesPerParagraph = 5, 
                                               int averageWordsPerSentence = 10)
        {
            string[] words = {
                "测试", "开发", "Unity", "游戏", "性能", "工具", "系统", "模块", "数据", "代码",
                "对象", "类", "接口", "方法", "属性", "变量", "函数", "参数", "返回值", "算法",
                "设计", "实现", "优化", "调试", "发布", "维护", "版本", "更新", "修复", "改进"
            };
            
            var sb = new StringBuilder();
            
            for (int p = 0; p < paragraphCount; p++)
            {
                for (int s = 0; s < sentencesPerParagraph; s++)
                {
                    int wordCount = (int)(averageWordsPerSentence * (0.7 + Random.NextDouble() * 0.6));
                    
                    for (int w = 0; w < wordCount; w++)
                    {
                        sb.Append(words[Random.Next(words.Length)]);
                        if (w < wordCount - 1)
                            sb.Append(Random.Next(10) < 8 ? " " : "");
                    }
                    
                    sb.Append(Random.Next(4) == 0 ? "！" : Random.Next(4) == 1 ? "？" : "。");
                    if (s < sentencesPerParagraph - 1)
                        sb.Append(" ");
                }
                
                if (p < paragraphCount - 1)
                    sb.AppendLine().AppendLine();
            }
            
            return sb.ToString();
        }
    }
} 