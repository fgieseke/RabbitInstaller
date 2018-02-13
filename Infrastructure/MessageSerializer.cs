using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RabbitMQ.Client.Events;
using RawRabbit.Common;
using RawRabbit.Serialization;

namespace RabbitInstaller.Infrastructure
{
    #region Using

    #endregion

    /// <summary>
    /// The json message serializer used by <see cref="ServiceBusClient"/>.
    /// <remarks>
    /// This implementation replaces the original implementation because adding the capability of serializing and deserializing 
    /// dynamic properties without exceptions.
    /// </remarks>
    /// </summary>
    public class MessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializer _serializer;

        public MessageSerializer(JsonSerializer serializer, Action<JsonSerializer> config = null)
        {
            _serializer = serializer;

            // We do not want any type information to pollute our index...
            _serializer.TypeNameHandling = TypeNameHandling.None;

            // Dynamic objects (or JOBjects) should not contain fields with null values
            _serializer.NullValueHandling = NullValueHandling.Ignore;

            _serializer.ContractResolver = new CamelCasePropertyNamesContractResolver();

            config?.Invoke(_serializer);
        }

        public byte[] Serialize<T>(T obj)
        {
            if (obj == null)
            {
                return Encoding.UTF8.GetBytes(string.Empty);
            }

            string msgStr;
            var sw = new StringWriter();
            using (var jsonWriter = new JsonTextWriter(sw))
            {
                jsonWriter.Formatting = _serializer.Formatting;
                _serializer.Serialize(jsonWriter, obj);
                msgStr = sw.ToString();
            }

            var msgBytes = Encoding.UTF8.GetBytes(msgStr);
            return msgBytes;
        }

        public object Deserialize(BasicDeliverEventArgs args)
        {
            object typeBytes;
            if (args.BasicProperties.Headers.TryGetValue(PropertyHeaders.MessageType, out typeBytes))
            {
                var typeName = Encoding.UTF8.GetString(typeBytes as byte[] ?? new byte[0]);
                var type = Type.GetType(typeName, false);
                return Deserialize(args.Body, type);
            }
            else
            {
                var typeName = args.BasicProperties.Type;
                var type = Type.GetType(typeName, false);
                return Deserialize(args.Body, type);
            }
        }

        public T Deserialize<T>(byte[] bytes)
        {
            var obj = (T)Deserialize(bytes, typeof(T));
            return obj;
        }

        public object Deserialize(byte[] bytes, Type messageType)
        {
            var msgStr = Encoding.UTF8.GetString(bytes);
            var obj = JsonConvert.DeserializeObject(msgStr, messageType);
            return obj;
        }
    }
}