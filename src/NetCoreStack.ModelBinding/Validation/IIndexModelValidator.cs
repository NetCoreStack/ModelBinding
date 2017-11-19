using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;

namespace NetCoreStack.ModelBinding
{
    public interface IIndexModelValidator
    {
        IEnumerable<ModelValidationResult> Validate(IndexModelValidationContext context);
    }
}
