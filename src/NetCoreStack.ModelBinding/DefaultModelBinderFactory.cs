using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Options;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NetCoreStack.ModelBinding
{
    public abstract class IndexModelBinderProviderContext
    {
        public abstract IIndexModelBinder CreateBinder(ModelMetadata metadata);

        public abstract BindingInfo BindingInfo { get; }

        public abstract ModelMetadata Metadata { get; }

        public abstract IModelMetadataProvider MetadataProvider { get; }
    }

    public class DefaultModelBinderFactory
    {
        private readonly IModelMetadataProvider _metadataProvider;
        private readonly IIndexModelBinderProvider[] _providers;

        private readonly ConcurrentDictionary<Key, IIndexModelBinder> _cache;

        public DefaultModelBinderFactory(IModelMetadataProvider metadataProvider, IOptions<IndexOptions> options)
        {
            _metadataProvider = metadataProvider;
            _providers = options.Value.ModelBinderProviders.ToArray();

            _cache = new ConcurrentDictionary<Key, IIndexModelBinder>();
        }

        public IIndexModelBinder CreateBinder(ModelBinderFactoryContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (_providers.Length == 0)
            {
                throw new InvalidOperationException($"Missing required services FormatModelBinderProvidersAreRequired");
            }

            IIndexModelBinder binder;
            if (TryGetCachedBinder(context.Metadata, context.CacheToken, out binder))
            {
                return binder;
            }

            var providerContext = new DefaultModelBinderProviderContext(this, context);
            binder = CreateBinderCoreUncached(providerContext, context.CacheToken);
            if (binder == null)
            {
                var message = "FormatCouldNotCreateIModelBinder";
                throw new InvalidOperationException(message);
            }

            Debug.Assert(!(binder is PlaceholderBinder));
            AddToCache(context.Metadata, context.CacheToken, binder);

            return binder;
        }

        private IIndexModelBinder CreateBinderCoreCached(DefaultModelBinderProviderContext providerContext, object token)
        {
            IIndexModelBinder binder;
            if (TryGetCachedBinder(providerContext.Metadata, token, out binder))
            {
                return binder;
            }

            binder = CreateBinderCoreUncached(providerContext, token) ?? IndexNoOpBinder.Instance;

            if (!(binder is PlaceholderBinder))
            {
                AddToCache(providerContext.Metadata, token, binder);
            }

            return binder;
        }

        private IIndexModelBinder CreateBinderCoreUncached(DefaultModelBinderProviderContext providerContext, object token)
        {
            if (!providerContext.Metadata.IsBindingAllowed)
            {
                return IndexNoOpBinder.Instance;
            }

            var key = new Key(providerContext.Metadata, token);

            var visited = providerContext.Visited;

            IIndexModelBinder binder;
            if (visited.TryGetValue(key, out binder))
            {
                if (binder != null)
                {
                    return binder;
                }

                binder = new IndexPlaceholderBinder();
                visited[key] = binder;
                return binder;
            }

            visited.Add(key, null);

            IIndexModelBinder result = null;

            for (var i = 0; i < _providers.Length; i++)
            {
                var provider = _providers[i];
                result = provider.GetBinder(providerContext);
                if (result != null)
                {
                    break;
                }
            }
            
            var placeholderBinder = visited[key] as IndexPlaceholderBinder;
            if (placeholderBinder != null)
            {
                placeholderBinder.Inner = result ?? IndexNoOpBinder.Instance;
            }

            if (result != null)
            {
                visited[key] = result;
            }

            return result;
        }

        private void AddToCache(ModelMetadata metadata, object cacheToken, IIndexModelBinder binder)
        {
            Debug.Assert(metadata != null);
            Debug.Assert(binder != null);

            if (cacheToken == null)
            {
                return;
            }

            _cache.TryAdd(new Key(metadata, cacheToken), binder);
        }

        private bool TryGetCachedBinder(ModelMetadata metadata, object cacheToken, out IIndexModelBinder binder)
        {
            Debug.Assert(metadata != null);

            if (cacheToken == null)
            {
                binder = null;
                return false;
            }

            return _cache.TryGetValue(new Key(metadata, cacheToken), out binder);
        }

        private class DefaultModelBinderProviderContext : IndexModelBinderProviderContext
        {
            private readonly DefaultModelBinderFactory _factory;

            public DefaultModelBinderProviderContext(
                DefaultModelBinderFactory factory,
                ModelBinderFactoryContext factoryContext)
            {
                _factory = factory;
                Metadata = factoryContext.Metadata;
                BindingInfo = new BindingInfo
                {
                    BinderModelName = factoryContext.BindingInfo?.BinderModelName ?? Metadata.BinderModelName,
                    BinderType = factoryContext.BindingInfo?.BinderType ?? Metadata.BinderType,
                    BindingSource = factoryContext.BindingInfo?.BindingSource ?? Metadata.BindingSource,
                    PropertyFilterProvider =
                        factoryContext.BindingInfo?.PropertyFilterProvider ?? Metadata.PropertyFilterProvider,
                };

                MetadataProvider = _factory._metadataProvider;
                Visited = new Dictionary<Key, IIndexModelBinder>();
            }

            private DefaultModelBinderProviderContext(
                DefaultModelBinderProviderContext parent,
                ModelMetadata metadata)
            {
                Metadata = metadata;

                _factory = parent._factory;
                MetadataProvider = parent.MetadataProvider;
                Visited = parent.Visited;

                BindingInfo = new BindingInfo()
                {
                    BinderModelName = metadata.BinderModelName,
                    BinderType = metadata.BinderType,
                    BindingSource = metadata.BindingSource,
                    PropertyFilterProvider = metadata.PropertyFilterProvider,
                };
            }

            public override BindingInfo BindingInfo { get; }

            public override ModelMetadata Metadata { get; }

            public override IModelMetadataProvider MetadataProvider { get; }

            public Dictionary<Key, IIndexModelBinder> Visited { get; }

            public override IIndexModelBinder CreateBinder(ModelMetadata metadata)
            {
                if (metadata == null)
                {
                    throw new ArgumentNullException(nameof(metadata));
                }

                var token = metadata;

                var nestedContext = new DefaultModelBinderProviderContext(this, metadata);
                return _factory.CreateBinderCoreCached(nestedContext, token);
            }
        }

        private struct Key : IEquatable<Key>
        {
            private readonly ModelMetadata _metadata;
            private readonly object _token; // Explicitly using ReferenceEquality for tokens.

            public Key(ModelMetadata metadata, object token)
            {
                _metadata = metadata;
                _token = token;
            }

            public bool Equals(Key other)
            {
                return _metadata.Equals(other._metadata) && object.ReferenceEquals(_token, other._token);
            }

            public override bool Equals(object obj)
            {
                var other = obj as Key?;
                return other.HasValue && Equals(other.Value);
            }

            public override int GetHashCode()
            {
                var hash = new HashCodeCombiner();
                hash.Add(_metadata);
                hash.Add(RuntimeHelpers.GetHashCode(_token));
                return hash;
            }

            public override string ToString()
            {
                if (_metadata.MetadataKind == ModelMetadataKind.Type)
                {
                    return $"{_token} (Type: '{_metadata.ModelType.Name}')";
                }
                else
                {
                    return $"{_token} (Property: '{_metadata.ContainerType.Name}.{_metadata.PropertyName}' Type: '{_metadata.ModelType.Name}')";
                }
            }
        }
    }

    internal struct HashCodeCombiner
    {
        private long _combinedHash64;

        public int CombinedHash
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _combinedHash64.GetHashCode(); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HashCodeCombiner(long seed)
        {
            _combinedHash64 = seed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(IEnumerable e)
        {
            if (e == null)
            {
                Add(0);
            }
            else
            {
                var count = 0;
                foreach (object o in e)
                {
                    Add(o);
                    count++;
                }
                Add(count);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(HashCodeCombiner self)
        {
            return self.CombinedHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int i)
        {
            _combinedHash64 = ((_combinedHash64 << 5) + _combinedHash64) ^ i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(string s)
        {
            var hashCode = (s != null) ? s.GetHashCode() : 0;
            Add(hashCode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(object o)
        {
            var hashCode = (o != null) ? o.GetHashCode() : 0;
            Add(hashCode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<TValue>(TValue value, IEqualityComparer<TValue> comparer)
        {
            var hashCode = value != null ? comparer.GetHashCode(value) : 0;
            Add(hashCode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HashCodeCombiner Start()
        {
            return new HashCodeCombiner(0x1505L);
        }
    }

    public class IndexNoOpBinder : IIndexModelBinder
    {
        public static readonly IIndexModelBinder Instance = new IndexNoOpBinder();

        public Task BindModelAsync(IndexModelBindingContext bindingContext)
        {
            return Task.CompletedTask;
        }
    }

    public class IndexPlaceholderBinder : IIndexModelBinder
    {
        public IIndexModelBinder Inner { get; set; }

        public Task BindModelAsync(IndexModelBindingContext bindingContext)
        {
            return Inner.BindModelAsync(bindingContext);
        }
    }
}
