using System.Net;
using System.Net.Http.Json;

namespace ErrOrValue;

public static class ErrOrHelpers
{
  /// <summary>
  /// Get all error messages
  /// </summary>
  public static List<string> Errors(this ErrOr errOr)
  {
    return errOr.Messages
      .Where(m => m.Severity == Severity.Error)
      .Select(m => m.Message)
      .ToList();
  }

  /// <summary>
  /// Add a message
  /// </summary>
  public static ErrOr AddMessage(this ErrOr errOr, string message, Severity severity = Severity.Info)
  {
    errOr.Messages.Add((message, severity));

    return errOr;
  }

  /// <summary>
  /// Add many messages
  /// </summary>
  public static ErrOr AddMessages(this ErrOr errOr, IEnumerable<(string Message, Severity Severity)> messages)
  {
    errOr.Messages.AddRange(messages);

    return errOr;
  }

  /// <summary>
  /// Merge the Status Code and Messages from another ErrOr into this ErrOr
  /// </summary>
  public static ErrOr MergeWith(this ErrOr errOr, ErrOr otherErrOr)
  {
    errOr.Code = otherErrOr.Code;
    errOr.Messages.AddRange(otherErrOr.Messages);

    return errOr;
  }

  /// <summary>
  /// Merge the Status Code, Messages, and Value (if types match) from another ErrOr into this ErrOr
  /// </summary>
  public static ErrOr<TTarget> MergeWith<TTarget, TSource>(this ErrOr<TTarget> errOr, ErrOr<TSource> otherErrOr)
  {
    // Merge base properties
    ((ErrOr)errOr).MergeWith(otherErrOr);

    // If the types match, try to update the value
    if (otherErrOr is ErrOr<TTarget> sameTypeErrOr && sameTypeErrOr.Value != null)
    {
      errOr.Value = sameTypeErrOr.Value;
    }

    return errOr;
  }

  /// <summary>
  /// Set all values at once
  /// </summary>
  public static ErrOr Set(
    this ErrOr errOr,
    string? message = null,
    Severity severity = Severity.Info,
    HttpStatusCode? code = null,
    Exception? ex = null)
  {
    if (!string.IsNullOrWhiteSpace(message))
    {
      errOr.AddMessage(message, severity);
    }

    if (code.HasValue)
    {
      errOr.Code = code.Value;
    }

    var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    if (isDevelopment && ex != null)
    {
      var exceptionMessage = ex.InnerException == null ? ex.Message : ex.InnerException.Message;

      if (!string.IsNullOrWhiteSpace(exceptionMessage))
      {
        errOr.AddMessage(exceptionMessage, Severity.Error);
      }
    }

    return errOr;
  }

  /// <summary>
  /// Set all values at once
  /// </summary>
  public static ErrOr<T> Set<T>(
    this ErrOr<T> errOr,
    T? value = default,
    string? message = null,
    Severity severity = Severity.Info,
    HttpStatusCode? code = null,
    Exception? ex = null)
  {
    errOr.Set(message, severity, code, ex);

    if (value != null)
    {
      errOr.Value = value;
    }

    return errOr;
  }

  /// <summary>
  /// Map a HTTP response to an ErrOr 
  /// </summary>
  public static ErrOr FromHttpResponse(this ErrOr errOr, HttpResponseMessage httpResponse, string externalServiceName)
  {
    errOr.Code = httpResponse.StatusCode;

    if (!httpResponse.IsSuccessStatusCode)
    {
      errOr.AddMessage($"Error from {externalServiceName}: {httpResponse.ReasonPhrase}", Severity.Error);
    }

    return errOr;
  }

  /// <summary>
  /// Map a HTTP response to an ErrOr 
  /// </summary>
  public static async Task<ErrOr<T>> FromHttpResponse<T>(this ErrOr<T> errOr, HttpResponseMessage httpResponse, string externalServiceName)
  {
    ((ErrOr)errOr).FromHttpResponse(httpResponse, externalServiceName);

    if (httpResponse.Content != null)
    {
      errOr.Value = await httpResponse.Content.ReadFromJsonAsync<T>();
    }

    return errOr;
  }

  /// <summary>
  /// Map an ErrOr to an HTTP response for MVC controllers
  /// </summary>
  public static Microsoft.AspNetCore.Mvc.IActionResult ToApiResponse(this ErrOr errOr, object? value = null)
  {
    var body = GetApiResponsePayload(errOr, value);
    var res = new Microsoft.AspNetCore.Mvc.JsonResult(body) { StatusCode = (int)errOr.Code };
    return res;
  }

  /// <summary>
  /// Map an ErrOr to an HTTP response for MVC controllers
  /// </summary>
  public static Microsoft.AspNetCore.Mvc.IActionResult ToApiResponse<T>(this ErrOr<T> errOr) => errOr.ToApiResponse(errOr.Value);

  /// <summary>
  /// Map an ErrOr to an HTTP response for minimal APIs
  /// </summary>
  public static Microsoft.AspNetCore.Http.IResult ToMinimalApiResponse(this ErrOr errOr, object? value = null)
  {
    var body = GetApiResponsePayload(errOr, value);
    var res = Microsoft.AspNetCore.Http.Results.Json(body, statusCode: (int)errOr.Code);
    return res;
  }

  /// <summary>
  /// Map an ErrOr to an HTTP response for minimal APIs
  /// </summary>
  public static Microsoft.AspNetCore.Http.IResult ToMinimalApiResponse<T>(this ErrOr<T> errOr) => errOr.ToMinimalApiResponse(errOr.Value);

  /// <summary>
  /// Get the description attribute of an enum value or fallback to the enum as a string
  /// </summary>
  private static string GetEnumDescription(this Enum enumValue)
  {
    var field = enumValue.GetType().GetField(enumValue.ToString());

    if (field != null && Attribute.GetCustomAttribute(field, typeof(System.ComponentModel.DescriptionAttribute)) is System.ComponentModel.DescriptionAttribute attribute)
    {
      return attribute.Description;
    }

    return enumValue.ToString();
  }

  /// <summary>
  /// Creates a standardized response payload from an ErrOr
  /// </summary>
  private static object GetApiResponsePayload(ErrOr errOr, object? value = null)
  {
    // Convert tuples to named properties for JSON serialization
    var jsonSerializableMessages = errOr.Messages.Select(m => new { message = m.Message, severity = m.Severity.GetEnumDescription() }).ToList();

    return value != null
      ? new { value = value, messages = jsonSerializableMessages }
      : new { messages = jsonSerializableMessages };
  }
}
