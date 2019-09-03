using Microsoft.AspNetCore.Http;
using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ngdb
{
    public class NgDbHttpHandler : IMiddleware
    {
        private const string ApplicationJson = "application/json";

        private const string CollectionsApi = "/api/collections";
        private const string GetApi = "/api/get";
        private const string SetApi = "/api/set";

        private const string CollectionQueryParam = "collection";
        private const string KeyQueryParam = "key";
        private const string CasQueryParam = "cas";
        private const string PersistenceQueryParam = "persistence";

        private readonly JsonSerializerOptions serializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly StoreService store;

        public NgDbHttpHandler(StoreService store)
        {
            this.store = store;
            serializerOptions.Converters.Add(new EnumAsStringConverter());
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            await HandleRequest(context.Request, context.Response);
        }

        private async Task HandleRequest(HttpRequest request, HttpResponse response)
        {
            object result = default(object);
            response.ContentType = ApplicationJson;
            var queryHasCollection = request.Query.TryGetValue(CollectionQueryParam, out var collection) && !string.IsNullOrWhiteSpace(collection);
            var queryHasKey = request.Query.TryGetValue(KeyQueryParam, out var key) && !string.IsNullOrWhiteSpace(key);
            var queryHasCas = request.Query.TryGetValue(CasQueryParam, out var stringCas) && !string.IsNullOrWhiteSpace(stringCas);
            switch (request.Path.Value)
            {
                case GetApi:
                    if (!HttpMethods.IsGet(request.Method))
                    {
                        response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    }
                    else
                    {
                        if (!queryHasCollection || !queryHasKey)
                        {
                            response.StatusCode = StatusCodes.Status400BadRequest;
                        }
                        else
                        {
                            result = store.Get<object>(collection, key);
                            response.StatusCode = StatusCodes.Status200OK;
                        }
                    }
                    break;
                case SetApi:
                    if (!HttpMethods.IsPost(request.Method))
                    {
                        response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    }
                    else
                    {
                        if (!queryHasCollection || !queryHasKey || !queryHasCas || !long.TryParse(stringCas, out var cas))
                        {
                            response.StatusCode = StatusCodes.Status400BadRequest;
                        }
                        else
                        {
                            var payload = await request.BodyReader.ReadAsync();
                            var jsonPayload = GetAsciiString(payload.Buffer);
                            var objectValue = JsonSerializer.Deserialize<object>(jsonPayload, serializerOptions);
                            result = store.Set(collection, key, objectValue, cas);
                            response.StatusCode = StatusCodes.Status200OK;
                        }
                    }
                    break;
                case CollectionsApi:
                    if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsPost(request.Method))
                    {
                        response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    }
                    else
                    {
                        if (HttpMethods.IsGet(request.Method))
                        {
                            result = store.GetCollections();
                        }
                        else
                        {
                            if (!queryHasCollection)
                            {
                                response.StatusCode = StatusCodes.Status400BadRequest;
                            }
                            else
                            {
                                var persistenceEnabled = request.Query.TryGetValue(PersistenceQueryParam, out var persistenceString) && bool.TryParse(persistenceString, out var persistenceBool) && persistenceBool;
                                result = store.CreateCollection(collection, persistenceEnabled);
                                response.StatusCode = StatusCodes.Status200OK;
                            }
                        }
                    }
                    break;
                default:
                    response.StatusCode = StatusCodes.Status404NotFound;
                    await response.WriteAsync($"NOT FOUND {request.Path.Value}");
                    return;
            }

            await response.WriteAsync(JsonSerializer.Serialize(result, serializerOptions));
        }

        private string GetAsciiString(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                return Encoding.ASCII.GetString(buffer.First.Span);
            }

            return string.Create((int)buffer.Length, buffer, (span, sequence) =>
            {
                foreach (var segment in sequence)
                {
                    Encoding.ASCII.GetChars(segment.Span, span);
                    span = span.Slice(segment.Length);
                }
            });
        }
    }

    public class EnumAsStringConverter : JsonConverter<Enum>
    {

        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override Enum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Enum.TryParse(typeToConvert, reader.GetString(), out var enumValue);
            return enumValue as Enum;
        }

        public override void Write(Utf8JsonWriter writer, Enum value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString());
        }
    }
}
