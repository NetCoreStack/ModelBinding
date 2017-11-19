using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;

namespace NetCoreStack.ModelBinding
{
    public interface IObjectIndexModelValidator
    {
        void Validate(
            ModelStateDictionary modelState,
            ValidationStateDictionary validationState,
            string prefix,
            object model);
    }

    public class DefaultIndexObjectValidator : IObjectIndexModelValidator
    {
        private readonly IModelMetadataProvider _modelMetadataProvider;
        private readonly IndexValidatorCache _validatorCache;
        private readonly IIndexModelValidatorProvider _validatorProvider;

        public DefaultIndexObjectValidator(
            IModelMetadataProvider modelMetadataProvider,
            IList<IIndexModelValidatorProvider> validatorProviders)
        {
            if (modelMetadataProvider == null)
            {
                throw new ArgumentNullException(nameof(modelMetadataProvider));
            }

            if (validatorProviders == null)
            {
                throw new ArgumentNullException(nameof(validatorProviders));
            }

            _modelMetadataProvider = modelMetadataProvider;
            _validatorCache = new IndexValidatorCache();

            _validatorProvider = new CompositeIndexModelValidatorProvider(validatorProviders);
        }

        public void Validate(
            ModelStateDictionary modelState,
            ValidationStateDictionary validationState,
            string prefix,
            object model)
        {
            var visitor = new IndexValidationVisitor(
                modelState,
                _validatorProvider,
                _validatorCache,
                _modelMetadataProvider,
                validationState);

            var metadata = model == null ? null : _modelMetadataProvider.GetMetadataForType(model.GetType());
            visitor.Validate(metadata, prefix, model);
        }
    }
}
