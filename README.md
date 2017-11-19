## NetCoreStack.ModelBinding
#### Model Binding library for .NET Core without HttpContext dependency

This library can be used for model binding without http request.
A lot of the code was inspired from ASP.NET Core MVC but this is a complete (and a more lightweight) rewrite. I'll refer to this library as "Context free Model Binding".

### Sample Models
```csharp
public class Person
{
    public string Name { get; set; }
    public IList<Address> Addresses { get; set; }
}

public class Address
{
    public int Zip { get; set; }

    public string Street { get; set; }

    public City City { get; set; }
}

public class City
{
    public string Name { get; set; }
    public float Latitude { get; set; }
    public float Longitude { get; set; }
}
```

### Model Binding
```csharp
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
```

### Prerequisites
> [ASP.NET Core](https://github.com/aspnet/Home)