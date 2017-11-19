using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NetCoreStack.ModelBinding
{
    public class IndexModelValidationContext
    {
        public ModelStateDictionary ModelState { get; }
        public ModelMetadata ModelMetadata { get; }

        public IModelMetadataProvider MetadataProvider { get; }

        public IndexModelValidationContext(
            ModelStateDictionary modelState,
            ModelMetadata modelMetadata,
            IModelMetadataProvider metadataProvider,
            object container,
            object model)
        {
            ModelState = modelState;
            ModelMetadata = modelMetadata;
            MetadataProvider = metadataProvider;
            Container = container;
            Model = model;
        }

        public object Model { get; }

        public object Container { get; }
    }

    public class IndexValidationVisitor
    {
        private readonly IIndexModelValidatorProvider _validatorProvider;
        private readonly IModelMetadataProvider _metadataProvider;
        private readonly IndexValidatorCache _validatorCache;
        private readonly ModelStateDictionary _modelState;
        private readonly ValidationStateDictionary _validationState;
        private readonly ValidationStack _currentPath;

        private object _container;
        private string _key;
        private object _model;
        private ModelMetadata _metadata;
        private IValidationStrategy _strategy;

        public IndexValidationVisitor(
            ModelStateDictionary modelState,
            IIndexModelValidatorProvider validatorProvider,
            IndexValidatorCache validatorCache,
            IModelMetadataProvider metadataProvider,
            ValidationStateDictionary validationState)
        {
            if (validatorProvider == null)
            {
                throw new ArgumentNullException(nameof(validatorProvider));
            }

            if (validatorCache == null)
            {
                throw new ArgumentNullException(nameof(validatorCache));
            }
            
            _validatorProvider = validatorProvider;
            _validatorCache = validatorCache;

            _metadataProvider = metadataProvider;
            _validationState = validationState;

            _modelState = modelState;
            _currentPath = new ValidationStack();
        }

        public bool Validate(ModelMetadata metadata, string key, object model)
        {
            if (model == null && key != null)
            {
                var entry = _modelState[key];
                if (entry != null && entry.ValidationState != ModelValidationState.Valid)
                {
                    entry.ValidationState = ModelValidationState.Valid;
                }

                return true;
            }

            return Visit(metadata, key, model);
        }

        protected virtual bool ValidateNode()
        {
            var state = _modelState.GetValidationState(_key);

            if (state != ModelValidationState.Invalid)
            {
                var validators = _validatorCache.GetValidators(_metadata, _validatorProvider);

                var count = validators.Count;
                if (count > 0)
                {
                    var context = new IndexModelValidationContext(
                        _modelState,
                        _metadata,
                        _metadataProvider,
                        _container,
                        _model);

                    var results = new List<ModelValidationResult>();
                    for (var i = 0; i < count; i++)
                    {
                        results.AddRange(validators[i].Validate(context));
                    }

                    var resultsCount = results.Count;
                    for (var i = 0; i < resultsCount; i++)
                    {
                        var result = results[i];
                        var key = ModelNames.CreatePropertyModelName(_key, result.MemberName);
                        _modelState.TryAddModelError(key, result.Message);
                    }
                }
            }

            state = _modelState.GetFieldValidationState(_key);
            if (state == ModelValidationState.Invalid)
            {
                return false;
            }
            else
            {
                var entry = _modelState[_key];
                if (entry != null)
                {
                    entry.ValidationState = ModelValidationState.Valid;
                }

                return true;
            }
        }

        private bool Visit(ModelMetadata metadata, string key, object model)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();

            if (model != null && !_currentPath.Push(model))
            {
                return true;
            }

            var entry = GetValidationEntry(model);
            key = entry?.Key ?? key ?? string.Empty;
            metadata = entry?.Metadata ?? metadata;
            var strategy = entry?.Strategy;

            if (_modelState.HasReachedMaxErrors)
            {
                SuppressValidation(key);
                return false;
            }
            else if (entry != null && entry.SuppressValidation)
            {
                SuppressValidation(entry.Key);
                _currentPath.Pop(model);
                return true;
            }

            using (StateManager.Recurse(this, key ?? string.Empty, metadata, model, strategy))
            {
                if (_metadata.IsEnumerableType)
                {
                    return VisitComplexType(DefaultCollectionValidationStrategy.Instance);
                }

                if (_metadata.IsComplexType)
                {
                    return VisitComplexType(DefaultComplexObjectValidationStrategy.Instance);
                }

                return VisitSimpleType();
            }
        }
        
        private bool VisitComplexType(IValidationStrategy defaultStrategy)
        {
            var isValid = true;

            if (_model != null && _metadata.ValidateChildren)
            {
                var strategy = _strategy ?? defaultStrategy;
                isValid = VisitChildren(strategy);
            }
            else if (_model != null)
            {
                SuppressValidation(_key);
            }
            
            if (isValid && !_modelState.HasReachedMaxErrors)
            {
                isValid &= ValidateNode();
            }

            return isValid;
        }

        private bool VisitSimpleType()
        {
            if (_modelState.HasReachedMaxErrors)
            {
                SuppressValidation(_key);
                return false;
            }

            return ValidateNode();
        }

        private bool VisitChildren(IValidationStrategy strategy)
        {
            var isValid = true;
            var enumerator = strategy.GetChildren(_metadata, _key, _model);
            var parentEntry = new ValidationEntry(_metadata, _key, _model);

            while (enumerator.MoveNext())
            {
                var entry = enumerator.Current;
                var metadata = entry.Metadata;
                var key = entry.Key;

                isValid &= Visit(metadata, key, entry.Model);
            }

            return isValid;
        }

        private void SuppressValidation(string key)
        {
            if (key == null)
            {
                return;
            }

            var entries = _modelState.FindKeysWithPrefix(key);
            foreach (var entry in entries)
            {
                entry.Value.ValidationState = ModelValidationState.Skipped;
            }
        }

        private ValidationStateEntry GetValidationEntry(object model)
        {
            if (model == null || _validationState == null)
            {
                return null;
            }

            ValidationStateEntry entry;
            _validationState.TryGetValue(model, out entry);
            return entry;
        }

        private struct StateManager : IDisposable
        {
            private readonly IndexValidationVisitor _visitor;
            private readonly object _container;
            private readonly string _key;
            private readonly ModelMetadata _metadata;
            private readonly object _model;
            private readonly object _newModel;
            private readonly IValidationStrategy _strategy;

            public static StateManager Recurse(
                IndexValidationVisitor visitor,
                string key,
                ModelMetadata metadata,
                object model,
                IValidationStrategy strategy)
            {
                var recursifier = new StateManager(visitor, model);

                visitor._container = visitor._model;
                visitor._key = key;
                visitor._metadata = metadata;
                visitor._model = model;
                visitor._strategy = strategy;

                return recursifier;
            }

            public StateManager(IndexValidationVisitor visitor, object newModel)
            {
                _visitor = visitor;
                _newModel = newModel;

                _container = _visitor._container;
                _key = _visitor._key;
                _metadata = _visitor._metadata;
                _model = _visitor._model;
                _strategy = _visitor._strategy;
            }

            public void Dispose()
            {
                _visitor._container = _container;
                _visitor._key = _key;
                _visitor._metadata = _metadata;
                _visitor._model = _model;
                _visitor._strategy = _strategy;

                _visitor._currentPath.Pop(_newModel);
            }
        }
    }
}
