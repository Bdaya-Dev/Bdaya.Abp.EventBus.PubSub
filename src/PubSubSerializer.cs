using System;
using System.Text;
using System.Text.Json;
using Volo.Abp.DependencyInjection;

namespace Bdaya.Abp.EventBus.PubSub;

/// <summary>
/// Interface for serializing and deserializing event data for Pub/Sub messages.
/// </summary>
public interface IPubSubSerializer
{
    /// <summary>
    /// Serializes an object to a byte array.
    /// </summary>
    byte[] Serialize(object obj);

    /// <summary>
    /// Deserializes a byte array to an object of the specified type.
    /// </summary>
    object? Deserialize(byte[] data, Type type);

    /// <summary>
    /// Deserializes a byte array to an object of type T.
    /// </summary>
    T? Deserialize<T>(byte[] data);
}

/// <summary>
/// Default JSON serializer for Pub/Sub messages using System.Text.Json.
/// </summary>
public class PubSubSerializer : IPubSubSerializer, ITransientDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public byte[] Serialize(object obj)
    {
        var json = JsonSerializer.Serialize(obj, JsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    public object? Deserialize(byte[] data, Type type)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize(json, type, JsonOptions);
    }

    public T? Deserialize<T>(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
