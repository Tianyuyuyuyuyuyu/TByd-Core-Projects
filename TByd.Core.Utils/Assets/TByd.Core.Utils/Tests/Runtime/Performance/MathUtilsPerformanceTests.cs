using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TByd.Core.Utils.Runtime;
using System.Diagnostics;

namespace TByd.Core.Utils.Tests.Runtime.Performance
{
    /// <summary>
    /// MathUtils性能测试类，使用PerformanceTestFramework进行测试
    /// </summary>
    public class MathUtilsPerformanceTests
    {
        private PerformanceTestFramework _testFramework;
        private Vector3 _startPos;
        private Vector3 _targetPos;
        private Vector3 _velocity;
        private float _testValue;

        [SetUp]
        public void Setup()
        {
            _testFramework = new PerformanceTestFramework();
            _startPos = new Vector3(1, 2, 3);
            _targetPos = new Vector3(4, 5, 6);
            _velocity = Vector3.zero;
            _testValue = 0.5f;
        }

        [Test]
        public void Test_SmoothDamp_Performance()
        {
            _testFramework.RunTest(
                "MathUtils.SmoothDamp",
                () => 
                {
                    Vector3 vel = _velocity;
                    MathUtils.SmoothDamp(_startPos, _targetPos, ref vel, 0.1f);
                },
                "Vector3.SmoothDamp",
                () => 
                {
                    Vector3 vel = _velocity;
                    Vector3.SmoothDamp(_startPos, _targetPos, ref vel, 0.1f, Mathf.Infinity, Time.deltaTime);
                },
                10000
            );
            
            // 测试不同的阻尼值
            _testFramework.RunTest(
                "MathUtils.SmoothDamp (低阻尼)",
                () => 
                {
                    Vector3 vel = _velocity;
                    MathUtils.SmoothDamp(_startPos, _targetPos, ref vel, 0.01f);
                },
                "Vector3.SmoothDamp (低阻尼)",
                () => 
                {
                    Vector3 vel = _velocity;
                    Vector3.SmoothDamp(_startPos, _targetPos, ref vel, 0.01f, Mathf.Infinity, Time.deltaTime);
                },
                10000
            );
            
            _testFramework.RunTest(
                "MathUtils.SmoothDamp (高阻尼)",
                () => 
                {
                    Vector3 vel = _velocity;
                    MathUtils.SmoothDamp(_startPos, _targetPos, ref vel, 1.0f);
                },
                "Vector3.SmoothDamp (高阻尼)",
                () => 
                {
                    Vector3 vel = _velocity;
                    Vector3.SmoothDamp(_startPos, _targetPos, ref vel, 1.0f, Mathf.Infinity, Time.deltaTime);
                },
                10000
            );
        }

        [Test]
        public void Test_Remap_Performance()
        {
            _testFramework.RunTest(
                "MathUtils.Remap",
                () => MathUtils.Remap(_testValue, 0, 1, 0, 100),
                "手动实现Remap",
                () => 
                {
                    float normalizedValue = (_testValue - 0) / (1 - 0);
                    return Mathf.Lerp(0, 100, normalizedValue);
                },
                10000
            );
            
            // 测试不同范围的重映射
            _testFramework.RunTest(
                "MathUtils.Remap (大范围)",
                () => MathUtils.Remap(_testValue, 0, 1, 0, 10000),
                "手动实现Remap (大范围)",
                () => 
                {
                    float normalizedValue = (_testValue - 0) / (1 - 0);
                    return Mathf.Lerp(0, 10000, normalizedValue);
                },
                10000
            );
            
            _testFramework.RunTest(
                "MathUtils.Remap (负范围)",
                () => MathUtils.Remap(_testValue, -1, 1, -100, 100),
                "手动实现Remap (负范围)",
                () => 
                {
                    float normalizedValue = (_testValue - (-1)) / (1 - (-1));
                    return Mathf.Lerp(-100, 100, normalizedValue);
                },
                10000
            );
        }

        [Test]
        public void Test_DirectionToRotation_Performance()
        {
            Vector3 direction = _targetPos - _startPos;
            
            _testFramework.RunTest(
                "MathUtils.DirectionToRotation",
                () => MathUtils.DirectionToRotation(direction),
                "Quaternion.LookRotation",
                () => Quaternion.LookRotation(direction),
                10000
            );
            
            // 测试不同方向
            Vector3 upDirection = Vector3.up;
            _testFramework.RunTest(
                "MathUtils.DirectionToRotation (上方向)",
                () => MathUtils.DirectionToRotation(upDirection),
                "Quaternion.LookRotation (上方向)",
                () => Quaternion.LookRotation(upDirection),
                10000
            );
            
            Vector3 randomDirection = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            ).normalized;
            
            _testFramework.RunTest(
                "MathUtils.DirectionToRotation (随机方向)",
                () => MathUtils.DirectionToRotation(randomDirection),
                "Quaternion.LookRotation (随机方向)",
                () => Quaternion.LookRotation(randomDirection),
                10000
            );
        }

        [Test]
        public void Test_Clamp_Performance()
        {
            float value = 150f;
            
            _testFramework.RunTest(
                "MathUtils.Clamp",
                () => MathUtils.Clamp(value, 0, 100),
                "Mathf.Clamp",
                () => Mathf.Clamp(value, 0, 100),
                10000
            );
            
            // 测试不同范围的Clamp
            _testFramework.RunTest(
                "MathUtils.Clamp (负范围)",
                () => MathUtils.Clamp(value, -100, -50),
                "Mathf.Clamp (负范围)",
                () => Mathf.Clamp(value, -100, -50),
                10000
            );
            
            // 测试Vector3的Clamp
            Vector3 vectorValue = new Vector3(150, 200, -50);
            Vector3 min = new Vector3(-10, -10, -10);
            Vector3 max = new Vector3(10, 10, 10);
            
            _testFramework.RunTest(
                "MathUtils.Clamp (Vector3)",
                () => MathUtils.Clamp(vectorValue, min, max),
                "手动实现Vector3 Clamp",
                () => new Vector3(
                    Mathf.Clamp(vectorValue.x, min.x, max.x),
                    Mathf.Clamp(vectorValue.y, min.y, max.y),
                    Mathf.Clamp(vectorValue.z, min.z, max.z)
                ),
                10000
            );
        }

        [Test]
        public void Test_Lerp_Performance()
        {
            _testFramework.RunTest(
                "MathUtils.Lerp",
                () => MathUtils.Lerp(_startPos, _targetPos, 0.5f),
                "Vector3.Lerp",
                () => Vector3.Lerp(_startPos, _targetPos, 0.5f),
                10000
            );
            
            // 测试不同的插值参数
            _testFramework.RunTest(
                "MathUtils.Lerp (t=0)",
                () => MathUtils.Lerp(_startPos, _targetPos, 0f),
                "Vector3.Lerp (t=0)",
                () => Vector3.Lerp(_startPos, _targetPos, 0f),
                10000
            );
            
            _testFramework.RunTest(
                "MathUtils.Lerp (t=1)",
                () => MathUtils.Lerp(_startPos, _targetPos, 1f),
                "Vector3.Lerp (t=1)",
                () => Vector3.Lerp(_startPos, _targetPos, 1f),
                10000
            );
            
            // 测试不受限制的插值参数
            _testFramework.RunTest(
                "MathUtils.LerpUnclamped",
                () => MathUtils.LerpUnclamped(_startPos, _targetPos, 1.5f),
                "Vector3.LerpUnclamped",
                () => Vector3.LerpUnclamped(_startPos, _targetPos, 1.5f),
                10000
            );
        }

        [Test]
        public void Test_CalculateDistance_Performance()
        {
            _testFramework.RunTest(
                "MathUtils.CalculateDistance",
                () => MathUtils.CalculateDistance(_startPos, _targetPos),
                "Vector3.Distance",
                () => Vector3.Distance(_startPos, _targetPos),
                10000
            );
            
            // 测试平方距离计算
            _testFramework.RunTest(
                "MathUtils.CalculateDistanceSquared",
                () => MathUtils.CalculateDistanceSquared(_startPos, _targetPos),
                "手动计算平方距离",
                () => (_targetPos - _startPos).sqrMagnitude,
                10000
            );
        }

        [Test]
        public void Test_Memory_Allocation()
        {
            // 测试内存分配
            _testFramework.RunMemoryTest(
                "MathUtils.SmoothDamp 内存分配",
                () => 
                {
                    Vector3 vel = _velocity;
                    for (int i = 0; i < 1000; i++)
                    {
                        MathUtils.SmoothDamp(_startPos, _targetPos, ref vel, 0.1f);
                    }
                },
                "Vector3.SmoothDamp 内存分配",
                () => 
                {
                    Vector3 vel = _velocity;
                    for (int i = 0; i < 1000; i++)
                    {
                        Vector3.SmoothDamp(_startPos, _targetPos, ref vel, 0.1f, Mathf.Infinity, Time.deltaTime);
                    }
                }
            );
            
            _testFramework.RunMemoryTest(
                "MathUtils.DirectionToRotation 内存分配",
                () => 
                {
                    Vector3 direction = _targetPos - _startPos;
                    for (int i = 0; i < 1000; i++)
                    {
                        MathUtils.DirectionToRotation(direction);
                    }
                },
                "Quaternion.LookRotation 内存分配",
                () => 
                {
                    Vector3 direction = _targetPos - _startPos;
                    for (int i = 0; i < 1000; i++)
                    {
                        Quaternion.LookRotation(direction);
                    }
                }
            );
        }

        [Test]
        public void GeneratePerformanceReport()
        {
            // 运行所有测试并生成报告
            Test_SmoothDamp_Performance();
            Test_Remap_Performance();
            Test_DirectionToRotation_Performance();
            Test_Clamp_Performance();
            Test_Lerp_Performance();
            Test_CalculateDistance_Performance();
            Test_Memory_Allocation();
            
            string report = _testFramework.GenerateReport("MathUtils性能测试报告");
            UnityEngine.Debug.Log(report);
            
            // 可以将报告保存到文件
            string reportPath = Application.temporaryCachePath + "/MathUtilsPerformanceReport.md";
            System.IO.File.WriteAllText(reportPath, report);
            UnityEngine.Debug.Log($"性能测试报告已保存到: {reportPath}");
        }
    }
} 