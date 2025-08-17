using System.Diagnostics.CodeAnalysis;

namespace Api.Utils
{
  public sealed class Result<T>
  {
    private Result(bool isSuccess, T? content)
    {
      IsSuccess = isSuccess;
      Content = content;
    }

    [MemberNotNullWhen(true, nameof(Content))]
    public bool IsSuccess { get; init; }
    public T? Content { get; init; }

    public static Result<T> Success(T content) => new(true, content);

    public static Result<T> Failure() => new(false, default);
  }
}