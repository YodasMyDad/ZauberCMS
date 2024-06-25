using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Shared.Validation.Interfaces;

public interface IValidate<T>
{
    Task<ValidateResult> Validate(T item);
}