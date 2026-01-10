using System;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;
using HMS.API.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HMS.API.Infrastructure.Common
{
    public class EventPublisher : IEventPublisher, IDisposable
    {
        private readonly ILogger<EventPublisher> _logger;
        private readonly object? _connection;
        private readonly object? _channel;
        private readonly MethodInfo? _basicPublishMethod;
        private readonly MethodInfo? _createBasicPropertiesMethod;

        public EventPublisher(IConfiguration config, ILogger<EventPublisher> logger)
        {
            _logger = logger;

            try
            {
                var url = config["RabbitMq:Url"] ?? config["RABBITMQ__URL"];
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var factoryType = Type.GetType("RabbitMQ.Client.ConnectionFactory, RabbitMQ.Client");
                    if (factoryType != null)
                    {
                        var factory = Activator.CreateInstance(factoryType);
                        // set Uri property if available
                        var uriProp = factoryType.GetProperty("Uri");
                        if (uriProp != null && uriProp.CanWrite)
                        {
                            uriProp.SetValue(factory, new Uri(url));
                        }

                        var createConnection = factoryType.GetMethod("CreateConnection", Type.EmptyTypes);
                        if (createConnection != null)
                        {
                            _connection = createConnection.Invoke(factory, null);
                            if (_connection != null)
                            {
                                var connType = _connection.GetType();
                                var createModel = connType.GetMethod("CreateModel", Type.EmptyTypes);
                                if (createModel != null)
                                {
                                    _channel = createModel.Invoke(_connection, null);
                                    if (_channel != null)
                                    {
                                        var channelType = _channel.GetType();
                                        _createBasicPropertiesMethod = channelType.GetMethod("CreateBasicProperties");
                                        _basicPublishMethod = channelType.GetMethod("BasicPublish", new[] { typeof(string), typeof(string), typeof(object), typeof(byte[]) })
                                                              ?? channelType.GetMethod("BasicPublish", new[] { typeof(string), typeof(string), typeof(object), typeof(Array) })
                                                              ?? channelType.GetMethod("BasicPublish");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("RabbitMQ.Client assembly not loaded; continuing with logger-only publisher.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ integration setup failed; falling back to logger");
            }
        }

        public Task PublishAsync(object @event)
        {
            try
            {
                if (_channel != null && _basicPublishMethod != null && _createBasicPropertiesMethod != null)
                {
                    var routingKey = @event.GetType().Name;
                    var body = JsonSerializer.SerializeToUtf8Bytes(@event);

                    var props = _createBasicPropertiesMethod.Invoke(_channel, null);
                    // try set Persistent property if exists
                    var propsType = props?.GetType();
                    var persistentProp = propsType?.GetProperty("Persistent");
                    persistentProp?.SetValue(props, true);

                    // BasicPublish may have different signatures; attempt common one
                    try
                    {
                        _basicPublishMethod.Invoke(_channel, new object[] { string.Empty, routingKey, props, body });
                    }
                    catch
                    {
                        // fallback: log if invocation failed
                        _logger.LogWarning("BasicPublish invocation failed; falling back to log");
                        _logger.LogInformation("Event: {Event}", JsonSerializer.Serialize(@event));
                    }

                    _logger.LogInformation("Published event to RabbitMQ: {EventType}", routingKey);
                    return Task.CompletedTask;
                }

                _logger.LogInformation("Event: {Event}", JsonSerializer.Serialize(@event));
                return Task.CompletedTask;
            }
            catch (TargetInvocationException tie)
            {
                _logger.LogError(tie.InnerException ?? tie, "Failed to publish event via reflection");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish event");
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                // attempt to close channel and connection via reflection
                if (_channel != null)
                {
                    var chType = _channel.GetType();
                    var close = chType.GetMethod("Close", Type.EmptyTypes);
                    close?.Invoke(_channel, null);
                }

                if (_connection != null)
                {
                    var connType = _connection.GetType();
                    var close = connType.GetMethod("Close", Type.EmptyTypes);
                    close?.Invoke(_connection, null);
                }
            }
            catch { }
        }
    }
}