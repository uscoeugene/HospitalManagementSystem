using System;

namespace HMS.API.Application.Common
{
    public sealed record ApiError(string Code, string Message, string? Detail = null);

    public sealed record ApiResponse<T>(bool Success, int Status, T? Data = default, ApiError? Error = null)
    {
        public static ApiResponse<T> ForSuccess(T? data, int status = 200) => new(true, status, data, null);
        public static ApiResponse<T> ForError(string code, string message, int status, string? detail = null) => new(false, status, default, new ApiError(code, message, detail));
    }
}
