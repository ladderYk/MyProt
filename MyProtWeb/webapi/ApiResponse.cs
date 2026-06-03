using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyProtWeb.webapi
{
    public class ApiResponse
    {
        public int Code { get; set; }       // 状态码，200 表示成功
        public object? Data { get; set; }   // 实际数据
        public string Msg { get; set; } = string.Empty; // 提示信息

        public static ApiResponse Ok(object? data = null, string msg = "success")
        {
            return new ApiResponse { Code = 200, Data = data, Msg = msg };
        }

        public static ApiResponse Fail(int code = 400, string msg = "error", object? data = null)
        {
            return new ApiResponse { Code = code, Data = data, Msg = msg };
        }
    }
}
