using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyProt
{
    public static class ProtocolEngine
    {
        private static int _transactionId = 0;

        /// <summary>
        /// 根据模板和变量构建请求字节数组
        /// </summary>
        public static byte[] BuildRequest(List<string> template, Dictionary<string, object> variables)
        {
            // 第一遍：生成“半成品”字节列表，标记 calc 位置
            var segments = new List<Segment>();
            foreach (var part in template)
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    // 变量占位符 {Name:func:fmt}
                    var content = trimmed.Substring(1, trimmed.Length - 2);
                    var parts = content.Split(':');
                    string varName = parts[0];
                    string func = "";
                    string format = "";
                    if (parts.Length == 3)
                    {
                        func = parts.Length > 1 ? parts[1] : null;
                        format = parts.Length > 2 ? parts[2] : null;
                    }
                    else if (parts.Length == 2)
                    {
                        format = parts.Length > 1 ? parts[1] : null;
                    }

                    if (func == "auto" && varName == "TransactionID")
                    {
                        // 自动递增事务 ID
                        int tid = Interlocked.Increment(ref _transactionId);
                        // 格式 X4 表示 2 字节大端
                        byte[] tidBytes = BitConverter.GetBytes((ushort)tid);
                        if (BitConverter.IsLittleEndian) Array.Reverse(tidBytes);
                        segments.Add(new Segment { Data = tidBytes, IsCalcLength = false });
                    }
                    else if (func == "calc" && varName == "Length")
                    {
                        // 标记需要计算长度的位置，暂留空
                        segments.Add(new Segment { IsCalcLength = true });
                    }
                    else
                    {
                        // 普通变量
                        object value = variables[varName];
                        byte[] valueBytes = FormatVariable(value, format);
                        segments.Add(new Segment { Data = valueBytes, IsCalcLength = false });
                    }
                }
                else
                {
                    // 固定十六进制，允许空格
                    string hex = trimmed.Replace(" ", "");
                    byte[] bytes = HexToBytes(hex);
                    segments.Add(new Segment { Data = bytes, IsCalcLength = false });
                }
            }

            // 第二遍：计算 calc 长度字段的值
            int totalLengthAfterLengthField = 0;
            int calcIndex = -1;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].IsCalcLength)
                {
                    calcIndex = i;
                    totalLengthAfterLengthField = 0; // 重置，从这个之后累加
                }
                else if (calcIndex >= 0)
                {
                    totalLengthAfterLengthField += segments[i].Data.Length;
                }
            }
            if (calcIndex >= 0)
            {
                // 长度字段占 2 字节，大端
                byte[] lenBytes = BitConverter.GetBytes((ushort)totalLengthAfterLengthField);
                if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                segments[calcIndex] = new Segment { Data = lenBytes, IsCalcLength = false };
            }

            // 拼接所有段
            var ms = new MemoryStream();
            foreach (var seg in segments)
            {
                if (!seg.IsCalcLength && seg.Data != null)
                    ms.Write(seg.Data, 0, seg.Data.Length);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// 解析响应字节数组，返回原始数据（根据 valueType）
        /// </summary>
        public static object ParseResponse(byte[] response, ResponseParserConfig parser)
        {
            // 验证条件
            if (!string.IsNullOrEmpty(parser.validCondition))
            {
                if (!EvaluateCondition(parser.validCondition, response))
                    throw new Exception($"Response invalid: {parser.validCondition}");
            }

            if (parser.valueType == "Empty")
                return null;

            // 提取数据段
            int dataLen = EvaluateExpression(parser.dataLengthExpr, response);
            if (dataLen == 0)
                return Array.Empty<byte>();

            byte[] data = new byte[dataLen];
            Buffer.BlockCopy(response, parser.dataStartIndex, data, 0, dataLen);

            if (parser.valueType == "ByteArray")
                return data;
            if (parser.valueType == "UInt16" && dataLen >= 2)
                return (data[0] << 8) | data[1];

            throw new NotSupportedException($"ValueType {parser.valueType} not supported");
        }

        // ---- 辅助方法 ----
        private static byte[] FormatVariable(object value, string format)
        {
            int iVal = 0;
            if (int.TryParse(value.ToString(), out iVal))
            {
                int byteCount = format == "X4" ? 2 : format == "X8" ? 4 : 1;
                byte[] bytes = new byte[byteCount];
                for (int i = 0; i < byteCount; i++)
                    bytes[byteCount - 1 - i] = (byte)(iVal >> (8 * i) & 0xFF);
                return bytes;
            }
            return null;
        }

        private static byte[] HexToBytes(string hex)
        {
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private static bool EvaluateCondition(string condition, byte[] resp)
        {
            // 简单实现：resp[7] == 0x03
            // 可扩展为更强大的表达式解析，此处硬编码最简单形式
            if (condition.StartsWith("resp[") && condition.Contains("] =="))
            {
                var match = System.Text.RegularExpressions.Regex.Match(condition, @"resp\[(\d+)\]\s*==\s*(0x[0-9a-fA-F]+|\d+)");
                if (match.Success)
                {
                    int index = int.Parse(match.Groups[1].Value);
                    string valStr = match.Groups[2].Value;
                    byte expected = valStr.StartsWith("0x") ? Convert.ToByte(valStr, 16) : byte.Parse(valStr);
                    return resp[index] == expected;
                }
            }
            return true; // 未实现则默认通过
        }

        private static int EvaluateExpression(string expr, byte[] resp)
        {
            if (string.IsNullOrEmpty(expr)) return 0;
            // 支持 resp[8] 这种简单形式
            var match = System.Text.RegularExpressions.Regex.Match(expr, @"resp\[(\d+)\]");
            if (match.Success)
            {
                int index = int.Parse(match.Groups[1].Value);
                return resp[index];
            }
            return int.Parse(expr); // 纯数字
        }

        private class Segment
        {
            public byte[] Data { get; set; }
            public bool IsCalcLength { get; set; }
        }
    }
}
