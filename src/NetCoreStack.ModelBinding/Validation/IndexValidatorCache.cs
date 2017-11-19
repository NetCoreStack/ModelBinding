using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace NetCoreStack.ModelBinding
{
    public class IndexValidatorCache
    {
        private readonly ConcurrentDictionary<ModelMetadata, CacheEntry> _cacheEntries = new ConcurrentDictionary<ModelMetadata, CacheEntry>();

        public IReadOnlyList<IIndexModelValidator> GetValidators(ModelMetadata metadata, IIndexModelValidatorProvider validatorProvider)
        {
            CacheEntry entry;
            if (_cacheEntries.TryGetValue(metadata, out entry))
            {
                return GetValidatorsFromEntry(entry, metadata, validatorProvider);
            }

            var items = new List<IndexValidatorItem>(metadata.ValidatorMetadata.Count);
            for (var i = 0; i < metadata.ValidatorMetadata.Count; i++)
            {
                items.Add(new IndexValidatorItem(metadata.ValidatorMetadata[i]));
            }

            ExecuteProvider(validatorProvider, metadata, items);

            var validators = ExtractValidators(items);

            var allValidatorsCached = true;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!item.IsReusable)
                {
                    item.Validator = null;
                    allValidatorsCached = false;
                }
            }

            if (allValidatorsCached)
            {
                entry = new CacheEntry(validators);
            }
            else
            {
                entry = new CacheEntry(items);
            }

            _cacheEntries.TryAdd(metadata, entry);

            return validators;
        }

        private IReadOnlyList<IIndexModelValidator> GetValidatorsFromEntry(CacheEntry entry, ModelMetadata metadata, IIndexModelValidatorProvider validationProvider)
        {
            Debug.Assert(entry.Validators != null || entry.Items != null);

            if (entry.Validators != null)
            {
                return entry.Validators;
            }

            var items = new List<IndexValidatorItem>(entry.Items.Count);
            for (var i = 0; i < entry.Items.Count; i++)
            {
                var item = entry.Items[i];
                if (item.IsReusable)
                {
                    items.Add(item);
                }
                else
                {
                    items.Add(new IndexValidatorItem(item.ValidatorMetadata));
                }
            }

            ExecuteProvider(validationProvider, metadata, items);

            return ExtractValidators(items);
        }

        private void ExecuteProvider(IIndexModelValidatorProvider validatorProvider, ModelMetadata metadata, List<IndexValidatorItem> items)
        {
            var context = new IndexModelValidatorProviderContext(metadata, items);
            validatorProvider.CreateValidators(context);
        }

        private IReadOnlyList<IIndexModelValidator> ExtractValidators(List<IndexValidatorItem> items)
        {
            var count = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Validator != null)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return Array.Empty<IIndexModelValidator>();
            }

            var validators = new IIndexModelValidator[count];

            var validatorIndex = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var validator = items[i].Validator;
                if (validator != null)
                {
                    validators[validatorIndex++] = validator;
                }
            }

            return validators;
        }

        private struct CacheEntry
        {
            public CacheEntry(IReadOnlyList<IIndexModelValidator> validators)
            {
                Validators = validators;
                Items = null;
            }

            public CacheEntry(List<IndexValidatorItem> items)
            {
                Items = items;
                Validators = null;
            }

            public IReadOnlyList<IIndexModelValidator> Validators { get; }

            public List<IndexValidatorItem> Items { get; }
        }
    }
}
