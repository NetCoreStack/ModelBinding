using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NetCoreStack.ModelBinding.Tests
{
    public class IndexModelBindingTests
    {
        private class Person
        {
            public string Name { get; set; }
            public IList<Address> Addresses { get; set; }
        }

        private class Address
        {
            public int Zip { get; set; }

            public string Street { get; set; }

            public City City { get; set; }
        }

        private class City
        {
            public string Name { get; set; }
            public float Latitude { get; set; }
            public float Longitude { get; set; }
        }

        [Fact]
        public async Task ModelBinder_Tests()
        {
            // Arrange
            var binder = IndexModelBinderHelper.GetIndexModelBinder();
            var formCollection = new FormCollection(new Dictionary<string, StringValues>()
                {
                    { "Name", new [] { "NetCoreStack" } },
                    { "Addresses.index", new [] { "Key1", "Key2" } },
                    { "Addresses[Key1].Street", new [] { "Street1" } },
                    { "Addresses[Key1].City.Name", new [] { "Yalova" } },
                    { "Addresses[Key1].City.Latitude", new [] { "40.631281" } },
                    { "Addresses[Key1].City.Longitude", new [] { "29.286804" } },
                    { "Addresses[Key2].Street", new [] { "Street2" } },
                });

            var valueProviders = IndexModelBinderHelper.GetValueProviders(formCollection);
            var parameter = new ParameterDescriptor()
            {
                Name = "parameter",
                ParameterType = typeof(Person)
            };

            var modelState = new ModelStateDictionary();

            // Act
            var modelBindingResult = await binder.BindModelAsync(new CompositeValueProvider(valueProviders), modelState, parameter);

            // Assert
            Assert.True(modelBindingResult.IsModelSet);
            Assert.IsType<Person>(modelBindingResult.Model);

            Assert.Equal(6, modelState.Count);
            Assert.Equal(0, modelState.ErrorCount);
            Assert.True(modelState.IsValid);
            var entry = Assert.Single(modelState, kvp => kvp.Key == "Addresses[Key1].Street").Value;
            Assert.Equal("Street1", entry.AttemptedValue);
            Assert.Equal("Street1", entry.RawValue);

            entry = Assert.Single(modelState, kvp => kvp.Key == "Addresses[Key2].Street").Value;
            Assert.Equal("Street2", entry.AttemptedValue);
            Assert.Equal("Street2", entry.RawValue);
        }
    }
}