using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TByd.Core.Utils.Editor.Tests.Framework
{
    /// <summary>
    /// 通用测试工具类，提供测试辅助功能
    /// </summary>
    public static class TestUtils
    {
        /// <summary>
        /// 生成指定长度的随机字符串用于测试
        /// </summary>
        /// <param name="length">字符串长度</param>
        /// <returns>随机字符串</returns>
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new System.Random();
            var sb = new StringBuilder(length);
            
            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[random.Next(chars.Length)]);
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 创建用于测试的随机数组
        /// </summary>
        /// <typeparam name="T">数组元素类型</typeparam>
        /// <param name="length">数组长度</param>
        /// <param name="generator">元素生成函数</param>
        /// <returns>随机数组</returns>
        public static T[] CreateRandomArray<T>(int length, Func<int, T> generator)
        {
            var array = new T[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = generator(i);
            }
            return array;
        }
        
        /// <summary>
        /// 创建用于测试的随机列表
        /// </summary>
        /// <typeparam name="T">列表元素类型</typeparam>
        /// <param name="count">列表元素数量</param>
        /// <param name="generator">元素生成函数</param>
        /// <returns>随机列表</returns>
        public static List<T> CreateRandomList<T>(int count, Func<int, T> generator)
        {
            var list = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(generator(i));
            }
            return list;
        }
        
        /// <summary>
        /// 通过反射获取私有字段值
        /// </summary>
        /// <typeparam name="T">字段类型</typeparam>
        /// <param name="obj">目标对象</param>
        /// <param name="fieldName">字段名称</param>
        /// <returns>字段值</returns>
        public static T GetPrivateField<T>(object obj, string fieldName)
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance | 
                BindingFlags.Public | BindingFlags.Static);
                
            if (field == null)
            {
                throw new ArgumentException($"找不到字段: {fieldName}");
            }
            
            return (T)field.GetValue(obj);
        }
        
        /// <summary>
        /// 通过反射设置私有字段值
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <param name="fieldName">字段名称</param>
        /// <param name="value">新值</param>
        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance | 
                BindingFlags.Public | BindingFlags.Static);
                
            if (field == null)
            {
                throw new ArgumentException($"找不到字段: {fieldName}");
            }
            
            field.SetValue(obj, value);
        }
        
        /// <summary>
        /// 通过反射调用私有方法
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="obj">目标对象</param>
        /// <param name="methodName">方法名称</param>
        /// <param name="parameters">方法参数</param>
        /// <returns>方法返回值</returns>
        public static T InvokePrivateMethod<T>(object obj, string methodName, params object[] parameters)
        {
            var type = obj.GetType();
            var method = type.GetMethod(methodName, 
                BindingFlags.NonPublic | BindingFlags.Instance | 
                BindingFlags.Public | BindingFlags.Static);
                
            if (method == null)
            {
                throw new ArgumentException($"找不到方法: {methodName}");
            }
            
            return (T)method.Invoke(obj, parameters);
        }
        
        /// <summary>
        /// 断言两个浮点数近似相等
        /// </summary>
        /// <param name="expected">期望值</param>
        /// <param name="actual">实际值</param>
        /// <param name="epsilon">容差</param>
        public static void AssertApproximatelyEqual(float expected, float actual, float epsilon = 0.0001f)
        {
            Assert.That(Math.Abs(expected - actual), Is.LessThan(epsilon),
                $"预期值: {expected}, 实际值: {actual}, 差值: {Math.Abs(expected - actual)}, 容差: {epsilon}");
        }
        
        /// <summary>
        /// 断言两个Vector3近似相等
        /// </summary>
        /// <param name="expected">期望值</param>
        /// <param name="actual">实际值</param>
        /// <param name="epsilon">容差</param>
        public static void AssertApproximatelyEqual(Vector3 expected, Vector3 actual, float epsilon = 0.0001f)
        {
            AssertApproximatelyEqual(expected.x, actual.x, epsilon);
            AssertApproximatelyEqual(expected.y, actual.y, epsilon);
            AssertApproximatelyEqual(expected.z, actual.z, epsilon);
        }
        
        /// <summary>
        /// 创建临时GameObject用于测试，测试结束后会自动销毁
        /// </summary>
        /// <param name="name">GameObject名称</param>
        /// <returns>创建的GameObject</returns>
        public static GameObject CreateTestGameObject(string name = "TestGameObject")
        {
            var go = new GameObject(name);
            
            // 使用TearDown自动销毁测试对象
            TestContext.CurrentContext.Test.Properties.Add("TempGameObject", go);
            
            return go;
        }
    }
} 