using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TByd.Core.Utils
{
    /// <summary>
    /// 提供高性能、跨平台的IO操作工具类
    /// </summary>
    /// <remarks>
    /// IOUtils提供了一系列处理文件和目录的方法，包括文件读写、路径处理、
    /// 文件监控、异步IO操作、文件类型检测和文件哈希计算等功能。
    /// 所有方法都经过优化，尽量减少GC分配和性能开销，并确保跨平台兼容性。
    /// 
    /// <para>性能优化：</para>
    /// <list type="bullet">
    ///   <item>使用ArrayPool减少内存分配</item>
    ///   <item>缓存常用的编码器和缓冲区</item>
    ///   <item>使用Span&lt;T&gt;进行高效的内存操作</item>
    ///   <item>优化文件读写的缓冲策略</item>
    /// </list>
    /// </remarks>
    public static class IOUtils
    {
        /// <summary>
        /// 默认的文件读写缓冲区大小（64KB）
        /// </summary>
        private const int DefaultBufferSize = 65536; // 64KB

        /// <summary>
        /// 缓存的UTF8编码器实例
        /// </summary>
        private static readonly UTF8Encoding CachedUtf8Encoding = new UTF8Encoding(false);

        /// <summary>
        /// 缓存的MD5哈希计算器
        /// </summary>
        private static readonly MD5 CachedMd5 = MD5.Create();

        /// <summary>
        /// 缓存的SHA1哈希计算器
        /// </summary>
        private static readonly SHA1 CachedSha1 = SHA1.Create();

        /// <summary>
        /// 缓存的SHA256哈希计算器
        /// </summary>
        private static readonly SHA256 CachedSha256 = SHA256.Create();

        /// <summary>
        /// 最大栈分配大小
        /// </summary>
        private const int MaxStackAllocSize = 256;

        #region 文件路径处理

        /// <summary>
        /// 获取规范化的文件路径（标准化分隔符，移除多余的分隔符和相对路径符号）
        /// </summary>
        /// <param name="path">需要规范化的路径</param>
        /// <returns>规范化后的路径</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 使用Stack进行路径段处理
        /// - 预先处理路径分隔符
        /// - 处理'..'和'.'符号
        /// </remarks>
        public static string NormalizePath(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length == 0) return string.Empty;

            // 标准化分隔符
            path = path.Replace('\\', '/');

            // 移除重复的分隔符
            while (path.IndexOf("//") >= 0)
            {
                path = path.Replace("//", "/");
            }

            // 处理 . 和 .. 符号
            string[] parts = path.Split('/');
            var stack = new Stack<string>();

            foreach (var part in parts)
            {
                if (part == ".")
                {
                    // 当前目录，跳过
                    continue;
                }
                else if (part == "..")
                {
                    // 上级目录，弹出栈顶元素（如果栈非空）
                    if (stack.Count > 0)
                    {
                        stack.Pop();
                    }
                    // 如果栈为空，可能是相对于根目录的上级目录，保留..符号
                    else if (path.StartsWith(".."))
                    {
                        stack.Push("..");
                    }
                }
                else if (!string.IsNullOrEmpty(part))
                {
                    // 常规路径段，入栈
                    stack.Push(part);
                }
            }

            // 重新构建规范化路径
            var result = new StringBuilder();
            var reversedStack = stack.Reverse();
            
            foreach (var part in reversedStack)
            {
                if (result.Length > 0)
                {
                    result.Append('/');
                }
                result.Append(part);
            }

            // 保留开始的斜杠（如果原始路径有的话）
            if (path.StartsWith("/") && result.Length > 0)
            {
                result.Insert(0, '/');
            }

            // 保留末尾的斜杠（如果原始路径有的话）
            if (path.EndsWith("/") && result.Length > 0)
            {
                result.Append('/');
            }

            return result.ToString();
        }

        /// <summary>
        /// 组合多个路径部分，确保分隔符正确
        /// </summary>
        /// <param name="parts">路径部分</param>
        /// <returns>组合后的路径</returns>
        /// <exception cref="ArgumentNullException">parts为null时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 过滤空路径部分
        /// - 标准化斜杠
        /// - 确保部分间只有一个斜杠
        /// </remarks>
        public static string CombinePath(params string[] parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (parts.Length == 0) return string.Empty;

            // 过滤掉空部分并标准化分隔符
            var filteredParts = new List<string>();
            foreach (var part in parts)
            {
                if (!string.IsNullOrEmpty(part))
                {
                    // 标准化分隔符
                    string cleanPart = part.Replace('\\', '/');
                    
                    // 移除开头的斜杠（除非是第一个部分）
                    if (filteredParts.Count > 0 && cleanPart.StartsWith("/"))
                    {
                        cleanPart = cleanPart.TrimStart('/');
                    }
                    
                    // 移除结尾的斜杠
                    if (cleanPart.EndsWith("/"))
                    {
                        cleanPart = cleanPart.TrimEnd('/');
                    }
                    
                    if (!string.IsNullOrEmpty(cleanPart))
                    {
                        filteredParts.Add(cleanPart);
                    }
                }
            }

            // 特殊情况处理
            if (filteredParts.Count == 0)
            {
                return string.Empty;
            }
            
            if (filteredParts.Count == 1)
            {
                return filteredParts[0];
            }
            
            // 组合路径部分
            var result = new StringBuilder();
            for (int i = 0; i < filteredParts.Count; i++)
            {
                if (i > 0)
                {
                    result.Append('/');
                }
                result.Append(filteredParts[i]);
            }
            
            return result.ToString();
        }

        /// <summary>
        /// 获取相对路径
        /// </summary>
        /// <param name="basePath">基础路径</param>
        /// <param name="targetPath">目标路径</param>
        /// <returns>从基础路径到目标路径的相对路径</returns>
        /// <exception cref="ArgumentNullException">basePath或targetPath为null时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 使用Span避免字符串分配
        /// - 预分配合适大小的缓冲区
        /// - 优化路径比较逻辑
        /// </remarks>
        public static string GetRelativePath(string basePath, string targetPath)
        {
            if (basePath == null) throw new ArgumentNullException(nameof(basePath));
            if (targetPath == null) throw new ArgumentNullException(nameof(targetPath));

            // 规范化路径
            basePath = NormalizePath(basePath);
            targetPath = NormalizePath(targetPath);

            // 分割路径
            var baseParts = basePath.Split('/');
            var targetParts = targetPath.Split('/');

            // 找到共同前缀
            int commonLength = 0;
            int minLength = Math.Min(baseParts.Length, targetParts.Length);
            
            while (commonLength < minLength && 
                   string.Equals(baseParts[commonLength], targetParts[commonLength], StringComparison.OrdinalIgnoreCase))
            {
                commonLength++;
            }

            // 计算上级目录数量
            int upCount = baseParts.Length - commonLength;

            // 计算结果长度
            int resultLength = upCount * 3; // "../" for each level up
            for (int i = commonLength; i < targetParts.Length; i++)
            {
                resultLength += targetParts[i].Length + 1; // +1 for separator
            }

            // 对于短路径，使用栈分配
            if (resultLength <= MaxStackAllocSize)
            {
                Span<char> buffer = stackalloc char[resultLength];
                int length = 0;

                // 添加上级目录
                for (int i = 0; i < upCount; i++)
                {
                    buffer[length++] = '.';
                    buffer[length++] = '.';
                    buffer[length++] = '/';
                }

                // 添加目标路径部分
                for (int i = commonLength; i < targetParts.Length; i++)
                {
                    if (i > commonLength)
                    {
                        buffer[length++] = '/';
                    }

                    targetParts[i].AsSpan().CopyTo(buffer.Slice(length));
                    length += targetParts[i].Length;
                }

                return new string(buffer.Slice(0, length));
            }

            // 对于长路径，使用StringBuilder
            var sb = new StringBuilder(resultLength);

            // 添加上级目录
            for (int i = 0; i < upCount; i++)
            {
                sb.Append("../");
            }

            // 添加目标路径部分
            for (int i = commonLength; i < targetParts.Length; i++)
            {
                if (i > commonLength)
                {
                    sb.Append('/');
                }
                sb.Append(targetParts[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取文件扩展名（不包含点）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>扩展名（小写，不包含点）。如果没有扩展名，则返回空字符串</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static string GetExtension(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            int lastDotIndex = path.LastIndexOf('.');
            int lastSeparatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));

            if (lastDotIndex > lastSeparatorIndex && lastDotIndex < path.Length - 1)
            {
                return path.Substring(lastDotIndex + 1).ToLowerInvariant();
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取不带扩展名的文件名
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>不带扩展名的文件名</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static string GetFileNameWithoutExtension(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            int lastSeparatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            int lastDotIndex = path.LastIndexOf('.');

            if (lastDotIndex > lastSeparatorIndex)
            {
                return path.Substring(lastSeparatorIndex + 1, lastDotIndex - lastSeparatorIndex - 1);
            }
            else
            {
                return path.Substring(lastSeparatorIndex + 1);
            }
        }

        /// <summary>
        /// 获取文件名（包含扩展名）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件名（包含扩展名）</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 使用Span避免字符串分配
        /// - 优化路径分隔符检测
        /// </remarks>
        public static string GetFileName(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            int lastSeparatorIndex = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            
            if (lastSeparatorIndex >= 0 && lastSeparatorIndex < path.Length - 1)
            {
                return path.Substring(lastSeparatorIndex + 1);
            }
            
            return path;
        }

        /// <summary>
        /// 获取文件所在的目录路径
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>目录路径</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static string GetDirectoryPath(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length == 0) return string.Empty;

            // 检查路径末尾是否有斜杠
            bool endsWithSeparator = (path.Length > 1) && (path[path.Length - 1] == '/' || path[path.Length - 1] == '\\');
            
            // 如果路径只有一个斜杠结尾，如"path/"，直接返回去掉斜杠的部分
            if (endsWithSeparator)
            {
                // 查找倒数第二个斜杠
                int secondLastSeparatorIndex = -1;
                for (int i = path.Length - 2; i >= 0; i--)
                {
                    if (path[i] == '/' || path[i] == '\\')
                    {
                        secondLastSeparatorIndex = i;
                        break;
                    }
                }
                
                if (secondLastSeparatorIndex >= 0)
                {
                    // 如果有两个斜杠，截取到倒数第二个斜杠
                    return path.Substring(0, secondLastSeparatorIndex);
                }
                else
                {
                    // 如果只有一个结尾斜杠，例如"path/"，返回"path"
                    return path.Substring(0, path.Length - 1);
                }
            }

            // 快速路径：如果不包含分隔符，则返回空字符串
            bool hasSeparator = false;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '/' || path[i] == '\\')
                {
                    hasSeparator = true;
                    break;
                }
            }
            
            if (!hasSeparator) return string.Empty;

            // 查找最后一个斜杠
            int lastSeparatorIndex = -1;
            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i] == '/' || path[i] == '\\')
                {
                    lastSeparatorIndex = i;
                    break;
                }
            }

            if (lastSeparatorIndex >= 0)
            {
                // 有斜杠，截取到最后一个斜杠之前
                return path.Substring(0, lastSeparatorIndex);
            }
            
            // 没有找到分隔符，返回空字符串
            return string.Empty;
        }

        #endregion

        #region 文件操作

        /// <summary>
        /// 安全地读取文件的所有文本内容
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <returns>文件的文本内容</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 使用FileShare.Read提高并发性能
        /// - 使用BufferedStream提高读取性能
        /// - 预分配合适大小的缓冲区
        /// </remarks>
        public static string ReadAllText(string path, Encoding encoding = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);

            encoding = encoding ?? CachedUtf8Encoding;
            
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize))
                using (var bufferedStream = new BufferedStream(fileStream, DefaultBufferSize))
                using (var reader = new StreamReader(bufferedStream, encoding))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"读取文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"读取文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 安全地读取文件的所有行
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <returns>文件的所有行</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static string[] ReadAllLines(string path, Encoding encoding = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);

            encoding = encoding ?? Encoding.UTF8;
            
            try
            {
                return File.ReadAllLines(path, encoding);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"读取文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"读取文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 安全地写入文本到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="content">要写入的内容</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <param name="append">是否追加到文件末尾，而不是覆盖</param>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="IOException">写入文件时发生IO错误时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 采用直接写入方式提升性能
        /// - 避免多层流嵌套的开销
        /// - 对小文本使用快速路径
        /// </remarks>
        public static void WriteAllText(string path, string content, Encoding encoding = null, bool append = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (content == null) content = string.Empty;
            
            encoding = encoding ?? CachedUtf8Encoding;
            
            try
            {
                // 确保目录存在
                string directory = GetDirectoryPath(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 使用更简单直接的写入方式，避免多层流嵌套
                if (append)
                {
                    // 追加模式
                    File.AppendAllText(path, content, encoding);
                }
                else
                {
                    // 创建/覆盖模式
                    File.WriteAllText(path, content, encoding);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"写入文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"写入文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 安全地写入多行文本到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="lines">要写入的行</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <param name="append">是否追加到文件末尾，而不是覆盖</param>
        /// <exception cref="ArgumentNullException">path或lines为null时抛出</exception>
        /// <exception cref="IOException">写入文件时发生IO错误时抛出</exception>
        public static void WriteAllLines(string path, IEnumerable<string> lines, Encoding encoding = null, bool append = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            
            encoding = encoding ?? Encoding.UTF8;
            
            try
            {
                // 确保目录存在
                string directory = GetDirectoryPath(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (append && File.Exists(path))
                {
                    File.AppendAllLines(path, lines, encoding);
                }
                else
                {
                    File.WriteAllLines(path, lines, encoding);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"写入文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"写入文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 安全地读取文件的所有字节
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件的字节数组</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 使用FileShare.Read提高并发性能
        /// - 使用BufferedStream提高读取性能
        /// - 预分配合适大小的缓冲区
        /// </remarks>
        public static byte[] ReadAllBytes(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);

            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultBufferSize))
                {
                    if (fileStream.Length > int.MaxValue)
                        throw new IOException("文件太大，无法一次性读取到内存");

                    int length = (int)fileStream.Length;
                    byte[] buffer = new byte[length];
                    int totalBytesRead = 0;
                    
                    while (totalBytesRead < length)
                    {
                        int bytesRead = fileStream.Read(buffer, totalBytesRead, length - totalBytesRead);
                        if (bytesRead == 0) break;
                        totalBytesRead += bytesRead;
                    }

                    return buffer;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"读取文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"读取文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 安全地写入字节数组到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="bytes">要写入的字节数组</param>
        /// <param name="append">是否追加到文件末尾，而不是覆盖</param>
        /// <exception cref="ArgumentNullException">path或bytes为null时抛出</exception>
        /// <exception cref="IOException">写入文件时发生IO错误时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 采用直接写入方式提升性能
        /// - 避免多层流嵌套的开销
        /// - 对小文件使用FileStream.Write
        /// </remarks>
        public static void WriteAllBytes(string path, byte[] bytes, bool append = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length == 0 && !append) 
            {
                // 优化空数组写入
                File.WriteAllBytes(path, Array.Empty<byte>());
                return;
            }

            try
            {
                // 确保目录存在
                string directory = GetDirectoryPath(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (append)
                {
                    // 追加模式需要手动处理
                    using (var fileStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None))
                    {
                        fileStream.Write(bytes, 0, bytes.Length);
                    }
                }
                else
                {
                    // 创建/覆盖模式直接使用File.WriteAllBytes
                    File.WriteAllBytes(path, bytes);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"写入文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"写入文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 安全地复制文件
        /// </summary>
        /// <param name="sourcePath">源文件路径</param>
        /// <param name="destPath">目标文件路径</param>
        /// <param name="overwrite">是否覆盖已存在的文件</param>
        /// <returns>是否成功复制</returns>
        /// <exception cref="ArgumentNullException">sourcePath或destPath为null时抛出</exception>
        /// <exception cref="FileNotFoundException">源文件不存在时抛出</exception>
        /// <exception cref="IOException">复制文件时发生IO错误时抛出</exception>
        public static bool CopyFile(string sourcePath, string destPath, bool overwrite = true)
        {
            if (sourcePath == null) throw new ArgumentNullException(nameof(sourcePath));
            if (destPath == null) throw new ArgumentNullException(nameof(destPath));
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("源文件不存在", sourcePath);
            
            try
            {
                // 确保目标目录存在
                string directory = GetDirectoryPath(destPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(sourcePath, destPath, overwrite);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"复制文件失败: {sourcePath} -> {destPath}, 错误: {ex.Message}");
                if (!overwrite && File.Exists(destPath))
                {
                    return false; // 目标文件已存在且不允许覆盖
                }
                throw new IOException($"复制文件失败: {sourcePath} -> {destPath}", ex);
            }
        }

        /// <summary>
        /// 安全地移动文件
        /// </summary>
        /// <param name="sourcePath">源文件路径</param>
        /// <param name="destPath">目标文件路径</param>
        /// <param name="overwrite">是否覆盖已存在的文件</param>
        /// <returns>是否成功移动</returns>
        /// <exception cref="ArgumentNullException">sourcePath或destPath为null时抛出</exception>
        /// <exception cref="FileNotFoundException">源文件不存在时抛出</exception>
        /// <exception cref="IOException">移动文件时发生IO错误时抛出</exception>
        public static bool MoveFile(string sourcePath, string destPath, bool overwrite = true)
        {
            if (sourcePath == null) throw new ArgumentNullException(nameof(sourcePath));
            if (destPath == null) throw new ArgumentNullException(nameof(destPath));
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("源文件不存在", sourcePath);
            
            try
            {
                // 如果目标文件已存在且需要覆盖
                if (File.Exists(destPath))
                {
                    if (overwrite)
                    {
                        File.Delete(destPath);
                    }
                    else
                    {
                        return false;
                    }
                }

                // 确保目标目录存在
                string directory = GetDirectoryPath(destPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(sourcePath, destPath);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"移动文件失败: {sourcePath} -> {destPath}, 错误: {ex.Message}");
                throw new IOException($"移动文件失败: {sourcePath} -> {destPath}", ex);
            }
        }

        /// <summary>
        /// 安全地删除文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>是否成功删除</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static bool DeleteFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"删除文件失败: {path}, 错误: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 目录操作

        /// <summary>
        /// 安全地创建目录
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <returns>是否成功创建，如果目录已存在则返回true</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="IOException">创建目录时发生IO错误时抛出</exception>
        public static bool CreateDirectory(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"创建目录失败: {path}, 错误: {ex.Message}");
                throw new IOException($"创建目录失败: {path}", ex);
            }
        }

        /// <summary>
        /// 安全地复制目录及其所有内容
        /// </summary>
        /// <param name="sourcePath">源目录路径</param>
        /// <param name="destPath">目标目录路径</param>
        /// <param name="overwrite">是否覆盖已存在的文件</param>
        /// <returns>是否成功复制</returns>
        /// <exception cref="ArgumentNullException">sourcePath或destPath为null时抛出</exception>
        /// <exception cref="DirectoryNotFoundException">源目录不存在时抛出</exception>
        /// <exception cref="IOException">复制目录时发生IO错误时抛出</exception>
        public static bool CopyDirectory(string sourcePath, string destPath, bool overwrite = true)
        {
            if (sourcePath == null) throw new ArgumentNullException(nameof(sourcePath));
            if (destPath == null) throw new ArgumentNullException(nameof(destPath));
            if (!Directory.Exists(sourcePath)) throw new DirectoryNotFoundException("源目录不存在: " + sourcePath);
            
            try
            {
                // 创建目标目录
                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                }

                // 复制所有文件
                foreach (string filePath in Directory.GetFiles(sourcePath))
                {
                    string fileName = Path.GetFileName(filePath);
                    string destFilePath = Path.Combine(destPath, fileName);
                    CopyFile(filePath, destFilePath, overwrite);
                }

                // 递归复制子目录
                foreach (string dirPath in Directory.GetDirectories(sourcePath))
                {
                    string dirName = Path.GetFileName(dirPath);
                    string destDirPath = Path.Combine(destPath, dirName);
                    CopyDirectory(dirPath, destDirPath, overwrite);
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"复制目录失败: {sourcePath} -> {destPath}, 错误: {ex.Message}");
                throw new IOException($"复制目录失败: {sourcePath} -> {destPath}", ex);
            }
        }

        /// <summary>
        /// 安全地删除目录及其所有内容
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <param name="recursive">是否递归删除子目录和文件</param>
        /// <returns>是否成功删除</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static bool DeleteDirectory(string path, bool recursive = true)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive);
                    return true;
                }
                return false;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"删除目录失败: {path}, 错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取目录中的所有文件（可选递归子目录）
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <param name="searchPattern">搜索模式，例如 "*.txt"</param>
        /// <param name="recursive">是否递归搜索子目录</param>
        /// <returns>文件路径列表</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="DirectoryNotFoundException">目录不存在时抛出</exception>
        public static string[] GetFiles(string path, string searchPattern = "*", bool recursive = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("目录不存在: " + path);
            
            try
            {
                return Directory.GetFiles(path, searchPattern, 
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"获取文件列表失败: {path}, 错误: {ex.Message}");
                throw new IOException($"获取文件列表失败: {path}", ex);
            }
        }

        /// <summary>
        /// 获取目录中的所有子目录（可选递归）
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <param name="recursive">是否递归搜索子目录</param>
        /// <returns>子目录路径列表</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="DirectoryNotFoundException">目录不存在时抛出</exception>
        public static string[] GetDirectories(string path, bool recursive = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("目录不存在: " + path);
            
            try
            {
                return Directory.GetDirectories(path, "*", 
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"获取子目录列表失败: {path}, 错误: {ex.Message}");
                throw new IOException($"获取子目录列表失败: {path}", ex);
            }
        }

        #endregion

        #region 文件信息

        /// <summary>
        /// 获取文件大小（字节）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件大小（字节）</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        public static long GetFileSize(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("文件不存在", path);
            
            try
            {
                var fileInfo = new FileInfo(path);
                return fileInfo.Length;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"获取文件大小失败: {path}, 错误: {ex.Message}");
                throw new IOException($"获取文件大小失败: {path}", ex);
            }
        }

        /// <summary>
        /// 获取文件的最后修改时间
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>最后修改时间</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        public static DateTime GetLastModifiedTime(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("文件不存在", path);
            
            try
            {
                return File.GetLastWriteTime(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"获取文件修改时间失败: {path}, 错误: {ex.Message}");
                throw new IOException($"获取文件修改时间失败: {path}", ex);
            }
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件是否存在</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static bool FileExists(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            return File.Exists(path);
        }

        /// <summary>
        /// 检查目录是否存在
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <returns>目录是否存在</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static bool DirectoryExists(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            return Directory.Exists(path);
        }

        #endregion

        #region 文件类型检测

        /// <summary>
        /// 检查文件是否为图片文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>是否为图片文件</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static bool IsImageFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            
            string extension = GetExtension(path).ToLowerInvariant();
            string[] imageExtensions = { "jpg", "jpeg", "png", "gif", "bmp", "tiff", "tif", "webp" };
            
            return Array.IndexOf(imageExtensions, extension) >= 0;
        }

        /// <summary>
        /// 检查文件是否为文本文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>是否为文本文件</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        public static bool IsTextFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("文件不存在", path);
            
            string extension = GetExtension(path).ToLowerInvariant();
            string[] textExtensions = { "txt", "csv", "json", "xml", "html", "htm", "css", "js", "cs", "c", "cpp", "h", "py", "md", "log" };
            
            if (Array.IndexOf(textExtensions, extension) >= 0)
            {
                return true;
            }
            
            // 如果扩展名不在列表中，尝试检查文件内容
            try
            {
                // 读取文件的前几千字节检查是否有二进制字符
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] buffer = new byte[4096]; // 4KB足够判断大部分情况
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    
                    // 检查是否有二进制字符
                    for (int i = 0; i < bytesRead; i++)
                    {
                        // 0是NULL，小于32且不是制表符、换行符等的都是控制字符
                        if (buffer[i] == 0 || (buffer[i] < 32 && buffer[i] != 9 && buffer[i] != 10 && buffer[i] != 13))
                        {
                            return false; // 发现二进制字符，不是文本文件
                        }
                    }
                    
                    return true; // 没有发现二进制字符，可能是文本文件
                }
            }
            catch
            {
                return false; // 读取失败，保守判断为非文本文件
            }
        }

        /// <summary>
        /// 检查文件是否为音频文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>是否为音频文件</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static bool IsAudioFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            
            string extension = GetExtension(path).ToLowerInvariant();
            string[] audioExtensions = { "mp3", "wav", "ogg", "flac", "aac", "m4a", "wma" };
            
            return Array.IndexOf(audioExtensions, extension) >= 0;
        }

        /// <summary>
        /// 检查文件是否为视频文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>是否为视频文件</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        public static bool IsVideoFile(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            
            string extension = GetExtension(path).ToLowerInvariant();
            string[] videoExtensions = { "mp4", "avi", "mov", "wmv", "flv", "mkv", "webm" };
            
            return Array.IndexOf(videoExtensions, extension) >= 0;
        }

        #endregion

        #region 文件哈希计算

        /// <summary>
        /// 哈希计算的默认缓冲区大小
        /// </summary>
        private const int HashBufferSize = 81920; // 80KB

        /// <summary>
        /// 计算文件的MD5哈希值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>MD5哈希值的十六进制字符串表示</returns>
        /// <remarks>
        /// 性能说明：此方法使用缓存的MD5实例和80KB的缓冲区进行优化，适用于大文件。
        /// 内存分配：仅分配结果字符串和一个缓冲区。
        /// </remarks>
        public static string CalculateMD5(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);
            
            try
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize); // 80KB
                try
                {
                    using (var fileStream = new FileStream(
                        path, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read, 
                        DefaultBufferSize))
                    using (var bufferedStream = new BufferedStream(fileStream, DefaultBufferSize))
                    {
                        byte[] hash;
                        lock (CachedMd5) // 确保线程安全
                        {
                            CachedMd5.Initialize();
                            int bytesRead;
                            while ((bytesRead = bufferedStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                CachedMd5.TransformBlock(buffer, 0, bytesRead, null, 0);
                            }
                            CachedMd5.TransformFinalBlock(buffer, 0, 0);
                            hash = CachedMd5.Hash;
                        }
                        
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"计算MD5哈希值失败: {path}, 错误: {ex.Message}");
                throw new IOException($"计算MD5哈希值失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步计算文件的MD5哈希值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，包含MD5哈希值的十六进制字符串</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static async Task<string> CalculateMD5Async(
            string path, 
            IProgress<float> progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);
            
            try
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize); // 80KB
                try
                {
                    using (var fileStream = new FileStream(
                        path, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read, 
                        DefaultBufferSize, 
                        FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        var length = fileStream.Length;
                        long totalBytesRead = 0;
                        
                        // 创建一个新的MD5实例，避免线程安全问题
                        using (var md5 = MD5.Create())
                        {
                            int bytesRead;
                            while ((bytesRead = await fileStream.ReadAsync(
                                buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                                
                                totalBytesRead += bytesRead;
                                progress?.Report(totalBytesRead / (float)length);
                            }
                            
                            md5.TransformFinalBlock(buffer, 0, 0);
                            var hash = md5.Hash;
                            
                            progress?.Report(1f);
                            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"计算MD5哈希值操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步计算MD5哈希值失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步计算MD5哈希值失败: {path}", ex);
            }
        }

        /// <summary>
        /// 计算文件的SHA1哈希值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>SHA1哈希值的十六进制字符串</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static string CalculateSHA1(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);
            
            try
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize); // 80KB
                try
                {
                    using (var fileStream = new FileStream(
                        path, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read, 
                        DefaultBufferSize, 
                        FileOptions.Asynchronous | FileOptions.SequentialScan))
                    using (var bufferedStream = new BufferedStream(fileStream, DefaultBufferSize))
                    {
                        byte[] hash;
                        lock (CachedSha1) // 确保线程安全
                        {
                            CachedSha1.Initialize();
                            int bytesRead;
                            while ((bytesRead = bufferedStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                CachedSha1.TransformBlock(buffer, 0, bytesRead, null, 0);
                            }
                            CachedSha1.TransformFinalBlock(buffer, 0, 0);
                            hash = CachedSha1.Hash;
                        }
                        
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"计算SHA1哈希值失败: {path}, 错误: {ex.Message}");
                throw new IOException($"计算SHA1哈希值失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步计算文件的SHA1哈希值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，包含SHA1哈希值的十六进制字符串</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static async Task<string> CalculateSHA1Async(
            string path, 
            IProgress<float> progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);
            
            try
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize); // 80KB
                try
                {
                    using (var fileStream = new FileStream(
                        path, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read, 
                        DefaultBufferSize, 
                        FileOptions.Asynchronous | FileOptions.SequentialScan))
                    using (var bufferedStream = new BufferedStream(fileStream, DefaultBufferSize))
                    {
                        var length = fileStream.Length;
                        long totalBytesRead = 0;
                        
                        // 创建一个新的SHA1实例，避免线程安全问题
                        using (var sha1 = SHA1.Create())
                        {
                            int bytesRead;
                            while ((bytesRead = await fileStream.ReadAsync(
                                buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
                                
                                totalBytesRead += bytesRead;
                                progress?.Report(totalBytesRead / (float)length);
                            }
                            
                            sha1.TransformFinalBlock(buffer, 0, 0);
                            var hash = sha1.Hash;
                            
                            progress?.Report(1f);
                            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"计算SHA1哈希值操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步计算SHA1哈希值失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步计算SHA1哈希值失败: {path}", ex);
            }
        }

        /// <summary>
        /// 计算文件的SHA256哈希值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>SHA256哈希值的十六进制字符串</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static string CalculateSHA256(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);
            
            try
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize); // 80KB
                try
                {
                    using (var fileStream = new FileStream(
                        path, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read, 
                        DefaultBufferSize, 
                        FileOptions.Asynchronous | FileOptions.SequentialScan))
                    using (var bufferedStream = new BufferedStream(fileStream, DefaultBufferSize))
                    {
                        byte[] hash;
                        lock (CachedSha256) // 确保线程安全
                        {
                            CachedSha256.Initialize();
                            int bytesRead;
                            while ((bytesRead = bufferedStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                CachedSha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                            }
                            CachedSha256.TransformFinalBlock(buffer, 0, 0);
                            hash = CachedSha256.Hash;
                        }
                        
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"计算SHA256哈希值失败: {path}, 错误: {ex.Message}");
                throw new IOException($"计算SHA256哈希值失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步计算文件的SHA256哈希值
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，包含SHA256哈希值的十六进制字符串</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static async Task<string> CalculateSHA256Async(
            string path, 
            IProgress<float> progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);
            
            try
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(HashBufferSize); // 80KB
                try
                {
                    using (var fileStream = new FileStream(
                        path, 
                        FileMode.Open, 
                        FileAccess.Read, 
                        FileShare.Read, 
                        DefaultBufferSize, 
                        FileOptions.Asynchronous | FileOptions.SequentialScan))
                    using (var bufferedStream = new BufferedStream(fileStream, DefaultBufferSize))
                    {
                        var length = fileStream.Length;
                        long totalBytesRead = 0;
                        
                        // 创建一个新的SHA256实例，避免线程安全问题
                        using (var sha256 = SHA256.Create())
                        {
                            int bytesRead;
                            while ((bytesRead = await fileStream.ReadAsync(
                                buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                                
                                totalBytesRead += bytesRead;
                                progress?.Report(totalBytesRead / (float)length);
                            }
                            
                            sha256.TransformFinalBlock(buffer, 0, 0);
                            var hash = sha256.Hash;
                            
                            progress?.Report(1f);
                            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"计算SHA256哈希值操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步计算SHA256哈希值失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步计算SHA256哈希值失败: {path}", ex);
            }
        }

        #endregion

        #region 异步IO操作

        /// <summary>
        /// 异步读取文件的所有文本内容
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，包含文件的文本内容</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 使用FileOptions.Asynchronous提高异步性能
        /// - 使用ArrayPool减少内存分配
        /// - 支持进度报告
        /// - 优化取消处理
        /// </remarks>
        public static async Task<string> ReadAllTextAsync(
            string path, 
            Encoding encoding = null, 
            IProgress<float> progress = null, 
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);

            encoding = encoding ?? CachedUtf8Encoding;
            
            try
            {
                using (var fileStream = new FileStream(
                    path, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.Read, 
                    DefaultBufferSize, 
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var length = fileStream.Length;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
                    try
                    {
                        using (var memoryStream = new MemoryStream((int)length))
                        {
                            int totalBytesRead = 0;
                            while (totalBytesRead < length)
                            {
                                int bytesRead = await fileStream.ReadAsync(
                                    buffer, 
                                    0, 
                                    (int)Math.Min(buffer.Length, length - totalBytesRead), 
                                    cancellationToken).ConfigureAwait(false);
                                    
                                if (bytesRead == 0) break;
                                
                                await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                                totalBytesRead += bytesRead;
                                
                                progress?.Report(totalBytesRead / (float)length);
                            }
                            
                            return encoding.GetString(memoryStream.ToArray());
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"读取文件操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步读取文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步读取文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步读取文件的所有行
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，包含文件的所有行</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static async Task<string[]> ReadAllLinesAsync(
            string path, 
            Encoding encoding = null, 
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);

            encoding = encoding ?? CachedUtf8Encoding;
            
            try
            {
                var lines = new List<string>();
                using (var fileStream = new FileStream(
                    path, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.Read, 
                    DefaultBufferSize, 
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var reader = new StreamReader(fileStream, encoding))
                {
                    var fileLength = fileStream.Length;
                    var lastProgress = 0f;
                    
                    string line;
                    while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lines.Add(line);
                        
                        // 每读取一行更新进度
                        if (progress != null)
                        {
                            var currentProgress = fileStream.Position / (float)fileLength;
                            if (currentProgress - lastProgress >= 0.01f) // 每1%更新一次
                            {
                                progress.Report(currentProgress);
                                lastProgress = currentProgress;
                            }
                        }
                    }
                    
                    progress?.Report(1f);
                }
                return lines.ToArray();
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"读取文件操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步读取文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步读取文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步写入文本到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="content">要写入的内容</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <param name="append">是否追加到文件末尾，而不是覆盖</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="IOException">写入文件时发生IO错误时抛出</exception>
        public static async Task WriteAllTextAsync(
            string path, 
            string content, 
            Encoding encoding = null, 
            bool append = false,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (content == null) throw new ArgumentNullException(nameof(content));
            
            encoding = encoding ?? CachedUtf8Encoding;
            
            try
            {
                // 确保目录存在
                string directory = GetDirectoryPath(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 将字符串转换为字节数组
                byte[] bytes = encoding.GetBytes(content);
                int totalBytes = bytes.Length;
                int bytesWritten = 0;

                using (var fileStream = new FileStream(
                    path, 
                    append ? FileMode.Append : FileMode.Create, 
                    FileAccess.Write, 
                    FileShare.None, 
                    DefaultBufferSize, 
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    int bufferSize = Math.Min(DefaultBufferSize, totalBytes);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        while (bytesWritten < totalBytes)
                        {
                            int count = Math.Min(bufferSize, totalBytes - bytesWritten);
                            Buffer.BlockCopy(bytes, bytesWritten, buffer, 0, count);
                            
                            await fileStream.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                            bytesWritten += count;
                            
                            progress?.Report(bytesWritten / (float)totalBytes);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"写入文件操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步写入文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步写入文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步写入多行文本到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="lines">要写入的行</param>
        /// <param name="encoding">字符编码，默认为UTF8</param>
        /// <param name="append">是否追加到文件末尾，而不是覆盖</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <exception cref="ArgumentNullException">path或lines为null时抛出</exception>
        /// <exception cref="IOException">写入文件时发生IO错误时抛出</exception>
        public static async Task WriteAllLinesAsync(
            string path, 
            IEnumerable<string> lines, 
            Encoding encoding = null, 
            bool append = false,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            
            encoding = encoding ?? CachedUtf8Encoding;
            
            try
            {
                // 确保目录存在
                string directory = GetDirectoryPath(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 计算总行数（如果可能）
                int? totalLines = lines is ICollection<string> collection ? collection.Count : null;
                int currentLine = 0;

                using (var fileStream = new FileStream(
                    path, 
                    append ? FileMode.Append : FileMode.Create, 
                    FileAccess.Write, 
                    FileShare.None, 
                    DefaultBufferSize, 
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                using (var writer = new StreamWriter(fileStream, encoding))
                {
                    foreach (var line in lines)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        if (line != null)
                        {
                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
                        
                        currentLine++;
                        if (totalLines.HasValue)
                        {
                            progress?.Report(currentLine / (float)totalLines.Value);
                        }
                    }
                    
                    await writer.FlushAsync().ConfigureAwait(false);
                }
                
                // 如果不知道总行数，写入完成时报告100%
                if (!totalLines.HasValue)
                {
                    progress?.Report(1f);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"写入文件操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步写入文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步写入文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步读取文件的所有字节
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，包含文件的字节数组</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">读取文件时发生IO错误时抛出</exception>
        public static async Task<byte[]> ReadAllBytesAsync(
            string path,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("指定的文件不存在", path);
            
            try
            {
                using (var fileStream = new FileStream(
                    path, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.Read, 
                    DefaultBufferSize, 
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var length = fileStream.Length;
                    if (length > int.MaxValue)
                        throw new IOException("文件太大，无法一次性读取到内存");

                    var result = new byte[length];
                    int totalBytesRead = 0;
                    int bufferSize = Math.Min(DefaultBufferSize, (int)length);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    
                    try
                    {
                        while (totalBytesRead < length)
                        {
                            int bytesToRead = (int)Math.Min(bufferSize, length - totalBytesRead);
                            int bytesRead = await fileStream.ReadAsync(
                                buffer, 
                                0, 
                                bytesToRead, 
                                cancellationToken).ConfigureAwait(false);
                                
                            if (bytesRead == 0) break;
                            
                            Buffer.BlockCopy(buffer, 0, result, totalBytesRead, bytesRead);
                            totalBytesRead += bytesRead;
                            
                            progress?.Report(totalBytesRead / (float)length);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"读取文件操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步读取文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步读取文件失败: {path}", ex);
            }
        }

        /// <summary>
        /// 异步写入字节数组到文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="bytes">要写入的字节数组</param>
        /// <param name="append">是否追加到文件末尾，而不是覆盖</param>
        /// <param name="progress">进度报告回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        /// <exception cref="ArgumentNullException">path或bytes为null时抛出</exception>
        /// <exception cref="IOException">写入文件时发生IO错误时抛出</exception>
        public static async Task WriteAllBytesAsync(
            string path, 
            byte[] bytes, 
            bool append = false,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            
            try
            {
                // 确保目录存在
                string directory = GetDirectoryPath(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var fileStream = new FileStream(
                    path, 
                    append ? FileMode.Append : FileMode.Create, 
                    FileAccess.Write, 
                    FileShare.None, 
                    DefaultBufferSize, 
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    int totalBytes = bytes.Length;
                    int bytesWritten = 0;
                    int bufferSize = Math.Min(DefaultBufferSize, totalBytes);
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    
                    try
                    {
                        while (bytesWritten < totalBytes)
                        {
                            int count = Math.Min(bufferSize, totalBytes - bytesWritten);
                            Buffer.BlockCopy(bytes, bytesWritten, buffer, 0, count);
                            
                            await fileStream.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
                            bytesWritten += count;
                            
                            progress?.Report(bytesWritten / (float)totalBytes);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"写入文件操作被取消: {path}");
                throw;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError($"异步写入文件失败: {path}, 错误: {ex.Message}");
                throw new IOException($"异步写入文件失败: {path}", ex);
            }
        }

        #endregion

        #region 文件监控

        // 存储活动的文件监控器
        private static Dictionary<string, FileSystemWatcher> _activeWatchers = new Dictionary<string, FileSystemWatcher>();
        
        // 存储节流状态
        private static readonly Dictionary<string, ThrottleInfo> _throttleStates = new Dictionary<string, ThrottleInfo>();
        
        // 节流信息类
        private class ThrottleInfo
        {
            public DateTime LastEventTime { get; set; } = DateTime.MinValue;
            public bool IsPending { get; set; } = false;
            public object EventLock { get; } = new object();
            public List<FileSystemEventArgs> PendingEvents { get; } = new List<FileSystemEventArgs>();
        }

        /// <summary>
        /// 文件监控状态信息
        /// </summary>
        public class FileWatcherInfo
        {
            /// <summary>
            /// 监控器ID
            /// </summary>
            public string Id { get; internal set; }
            
            /// <summary>
            /// 被监控的路径
            /// </summary>
            public string Path { get; internal set; }
            
            /// <summary>
            /// 监控器是否启用
            /// </summary>
            public bool IsEnabled { get; internal set; }
            
            /// <summary>
            /// 监控器是否包含子目录
            /// </summary>
            public bool IncludeSubdirectories { get; internal set; }
            
            /// <summary>
            /// 监控的事件类型
            /// </summary>
            public NotifyFilters NotifyFilters { get; internal set; }
            
            /// <summary>
            /// 过滤器
            /// </summary>
            public string Filter { get; internal set; }
            
            /// <summary>
            /// 创建时间
            /// </summary>
            public DateTime CreatedTime { get; internal set; }
            
            /// <summary>
            /// 最后一次事件时间
            /// </summary>
            public DateTime LastEventTime { get; internal set; }
            
            /// <summary>
            /// 事件计数
            /// </summary>
            public int EventCount { get; internal set; }
        }

        /// <summary>
        /// 开始监控文件或目录的变化
        /// </summary>
        /// <param name="path">要监控的文件或目录路径</param>
        /// <param name="onChange">文件变化时的回调</param>
        /// <param name="onCreate">文件创建时的回调</param>
        /// <param name="onDelete">文件删除时的回调</param>
        /// <param name="onRename">文件重命名时的回调</param>
        /// <param name="filter">文件过滤器，例如"*.txt"</param>
        /// <param name="includeSubdirectories">是否包含子目录</param>
        /// <param name="throttleInterval">事件节流间隔（毫秒），防止短时间内触发过多事件，默认为300毫秒</param>
        /// <returns>监控器ID，用于停止监控</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <exception cref="ArgumentException">path不存在时抛出</exception>
        /// <remarks>
        /// 性能优化：
        /// - 实现事件节流，避免短时间内触发过多事件
        /// - 优化资源管理，确保监控器正确释放
        /// - 提供监控状态查询功能
        /// </remarks>
        public static string StartWatching(
            string path, 
            Action<FileSystemEventArgs> onChange = null, 
            Action<FileSystemEventArgs> onCreate = null, 
            Action<FileSystemEventArgs> onDelete = null, 
            Action<RenamedEventArgs> onRename = null, 
            string filter = "*.*", 
            bool includeSubdirectories = false,
            int throttleInterval = 300)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            
            path = NormalizePath(path);
            
            if (!Directory.Exists(path) && !File.Exists(path))
                throw new ArgumentException("指定的路径不存在", nameof(path));
            
            // 生成唯一ID
            string watcherId = Guid.NewGuid().ToString();
            
            try
            {
                var watcher = new FileSystemWatcher
                {
                    Path = Directory.Exists(path) ? path : Path.GetDirectoryName(path),
                    Filter = filter,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
                    IncludeSubdirectories = includeSubdirectories
                };
                
                // 创建节流信息
                var throttleInfo = new ThrottleInfo();
                
                // 注册事件处理器
                if (onChange != null)
                {
                    watcher.Changed += (sender, e) => HandleWatcherEvent(e, onChange, watcherId, throttleInterval, throttleInfo);
                }
                
                if (onCreate != null)
                {
                    watcher.Created += (sender, e) => HandleWatcherEvent(e, onCreate, watcherId, throttleInterval, throttleInfo);
                }
                
                if (onDelete != null)
                {
                    watcher.Deleted += (sender, e) => HandleWatcherEvent(e, onDelete, watcherId, throttleInterval, throttleInfo);
                }
                
                if (onRename != null)
                {
                    watcher.Renamed += (sender, e) => HandleWatcherEvent(e, onRename, watcherId, throttleInterval, throttleInfo);
                }
                
                // 启用监控器
                watcher.EnableRaisingEvents = true;
                
                // 存储监控器和节流信息
                lock (_activeWatchers)
                {
                    _activeWatchers[watcherId] = watcher;
                    _throttleStates[watcherId] = throttleInfo;
                }
                
                return watcherId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"启动文件监控失败: {ex.Message}");
                throw;
            }
        }
        
        // 处理监控事件，实现节流
        private static void HandleWatcherEvent<T>(
            T eventArgs, 
            Action<T> callback, 
            string watcherId, 
            int throttleInterval,
            ThrottleInfo throttleInfo) where T : FileSystemEventArgs
        {
            if (!_activeWatchers.ContainsKey(watcherId)) return;
            
            lock (throttleInfo.EventLock)
            {
                // 更新最后事件时间
                throttleInfo.LastEventTime = DateTime.Now;
                
                // 添加到待处理事件列表
                throttleInfo.PendingEvents.Add(eventArgs);
                
                // 如果已经有一个待处理的节流任务，不需要创建新的
                if (throttleInfo.IsPending) return;
                
                // 标记为有待处理的节流任务
                throttleInfo.IsPending = true;
                
                // 启动节流任务
                Task.Delay(throttleInterval).ContinueWith(t =>
                {
                    ProcessThrottledEvents(watcherId, callback);
                });
            }
        }
        
        // 处理节流后的事件
        private static void ProcessThrottledEvents<T>(string watcherId, Action<T> callback) where T : FileSystemEventArgs
        {
            if (!_throttleStates.TryGetValue(watcherId, out var throttleInfo)) return;
            
            List<FileSystemEventArgs> eventsToProcess = null;
            
            lock (throttleInfo.EventLock)
            {
                // 获取所有待处理事件
                eventsToProcess = new List<FileSystemEventArgs>(throttleInfo.PendingEvents);
                throttleInfo.PendingEvents.Clear();
                
                // 检查是否需要继续节流
                var now = DateTime.Now;
                var timeSinceLastEvent = now - throttleInfo.LastEventTime;
                
                if (timeSinceLastEvent.TotalMilliseconds < 300)
                {
                    // 如果最后一个事件发生在300毫秒内，继续节流
                    Task.Delay(300).ContinueWith(t =>
                    {
                        ProcessThrottledEvents(watcherId, callback);
                    });
                }
                else
                {
                    // 否则，标记为没有待处理的节流任务
                    throttleInfo.IsPending = false;
                }
            }
            
            // 处理事件
            if (eventsToProcess != null)
            {
                foreach (var evt in eventsToProcess)
                {
                    try
                    {
                        if (evt is T typedEvent)
                        {
                            callback(typedEvent);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"处理文件监控事件时出错: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 停止指定ID的文件监控
        /// </summary>
        /// <param name="watcherId">监控器ID</param>
        /// <returns>是否成功停止监控</returns>
        public static bool StopWatching(string watcherId)
        {
            if (string.IsNullOrEmpty(watcherId)) return false;
            
            lock (_activeWatchers)
            {
                if (_activeWatchers.TryGetValue(watcherId, out var watcher))
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                        _activeWatchers.Remove(watcherId);
                        _throttleStates.Remove(watcherId);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"停止文件监控时出错: {ex.Message}");
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// 停止所有文件监控
        /// </summary>
        public static void StopAllWatching()
        {
            lock (_activeWatchers)
            {
                foreach (var watcher in _activeWatchers.Values)
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"停止文件监控时出错: {ex.Message}");
                    }
                }
                
                _activeWatchers.Clear();
                _throttleStates.Clear();
            }
        }
        
        /// <summary>
        /// 获取所有活动的文件监控器信息
        /// </summary>
        /// <returns>监控器信息列表</returns>
        public static List<FileWatcherInfo> GetAllWatchers()
        {
            var result = new List<FileWatcherInfo>();
            
            lock (_activeWatchers)
            {
                foreach (var pair in _activeWatchers)
                {
                    var watcher = pair.Value;
                    var throttleInfo = _throttleStates.TryGetValue(pair.Key, out var info) ? info : null;
                    
                    result.Add(new FileWatcherInfo
                    {
                        Id = pair.Key,
                        Path = watcher.Path,
                        IsEnabled = watcher.EnableRaisingEvents,
                        IncludeSubdirectories = watcher.IncludeSubdirectories,
                        NotifyFilters = watcher.NotifyFilter,
                        Filter = watcher.Filter,
                        CreatedTime = DateTime.Now, // 无法获取创建时间，使用当前时间
                        LastEventTime = throttleInfo?.LastEventTime ?? DateTime.MinValue,
                        EventCount = throttleInfo?.PendingEvents.Count ?? 0
                    });
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取指定ID的文件监控器信息
        /// </summary>
        /// <param name="watcherId">监控器ID</param>
        /// <returns>监控器信息，如果不存在则返回null</returns>
        public static FileWatcherInfo GetWatcher(string watcherId)
        {
            if (string.IsNullOrEmpty(watcherId)) return null;
            
            lock (_activeWatchers)
            {
                if (_activeWatchers.TryGetValue(watcherId, out var watcher))
                {
                    var throttleInfo = _throttleStates.TryGetValue(watcherId, out var info) ? info : null;
                    
                    return new FileWatcherInfo
                    {
                        Id = watcherId,
                        Path = watcher.Path,
                        IsEnabled = watcher.EnableRaisingEvents,
                        IncludeSubdirectories = watcher.IncludeSubdirectories,
                        NotifyFilters = watcher.NotifyFilter,
                        Filter = watcher.Filter,
                        CreatedTime = DateTime.Now, // 无法获取创建时间，使用当前时间
                        LastEventTime = throttleInfo?.LastEventTime ?? DateTime.MinValue,
                        EventCount = throttleInfo?.PendingEvents.Count ?? 0
                    };
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 暂停指定ID的文件监控
        /// </summary>
        /// <param name="watcherId">监控器ID</param>
        /// <returns>是否成功暂停</returns>
        public static bool PauseWatching(string watcherId)
        {
            if (string.IsNullOrEmpty(watcherId)) return false;
            
            lock (_activeWatchers)
            {
                if (_activeWatchers.TryGetValue(watcherId, out var watcher))
                {
                    try
                    {
                        watcher.EnableRaisingEvents = false;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"暂停文件监控时出错: {ex.Message}");
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 恢复指定ID的文件监控
        /// </summary>
        /// <param name="watcherId">监控器ID</param>
        /// <returns>是否成功恢复</returns>
        public static bool ResumeWatching(string watcherId)
        {
            if (string.IsNullOrEmpty(watcherId)) return false;
            
            lock (_activeWatchers)
            {
                if (_activeWatchers.TryGetValue(watcherId, out var watcher))
                {
                    try
                    {
                        watcher.EnableRaisingEvents = true;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"恢复文件监控时出错: {ex.Message}");
                    }
                }
            }
            
            return false;
        }

        #endregion

        /// <summary>
        /// 获取文件扩展名（不包含点）
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>扩展名（小写，不包含点）。如果没有扩展名，则返回空字符串</returns>
        /// <exception cref="ArgumentNullException">path为null时抛出</exception>
        /// <remarks>
        /// 此方法是GetExtension的别名，保持API一致性
        /// </remarks>
        public static string GetFileExtension(string path)
        {
            return GetExtension(path);
        }
    }
}
