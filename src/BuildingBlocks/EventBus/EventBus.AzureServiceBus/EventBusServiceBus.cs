using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Base.Events;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace EventBus.AzureServiceBus;
public class EventBusServiceBus : BaseEventBus {
    private ITopicClient? _topicClient;
    private ManagementClient? _managementClient;
    private ILogger _logger;
    public EventBusServiceBus(
        EventBusConfig eventBusConfig,
        IServiceProvider serviceProvider,
        IEventBusSubscriptionManager subscriptionManager
        ) : base(eventBusConfig, serviceProvider, subscriptionManager) {
        _managementClient = new(eventBusConfig.EventBusConnectionString);
        _topicClient = CreateTopicClient();
        _logger = serviceProvider.GetService(typeof(ILogger<EventBusServiceBus>)) as ILogger<EventBusServiceBus>;
    }

    private ITopicClient CreateTopicClient() {
        if(_topicClient is null || _topicClient.IsClosedOrClosing)
            _topicClient = new TopicClient(EventBusConfig.EventBusConnectionString, EventBusConfig.DefaultTopicName, RetryPolicy.Default);


        // Ensure that topic alredy exist
        if(_managementClient?.TopicExistsAsync(EventBusConfig.DefaultTopicName).GetAwaiter().GetResult() is false)
            _managementClient.CreateTopicAsync(EventBusConfig.DefaultTopicName).GetAwaiter().GetResult();


        return _topicClient;
    }

    public override void Publish(IntegrationEvent @event) {
        var eventName = @event.GetType().Name; // example OrderCreatedIntegrationEvent
        eventName = ProcessEventName(eventName); // example OrderCreated

        var eventString = JsonConvert.SerializeObject(@event);
        var bodyArray = Encoding.UTF8.GetBytes(eventString);

        var message = new Message {
            MessageId = Guid.NewGuid().ToString(),
            Body = bodyArray,
            Label = eventName
        };

        _topicClient?.SendAsync(message).GetAwaiter().GetResult();
    }

    public override void Subscribe<T, TH>() {
        var eventName = typeof(T).Name;
        eventName = ProcessEventName(eventName);
        if(SubscriptionManager.HasSubscriptionForEvent(eventName) is false) {
            var subscriptionClient = CreateSubscriptionClient(eventName);

            ResigterSubscription(subscriptionClient);
        }

        _logger.LogInformation($"Subscribing to event {eventName} with {typeof(TH).Name}");

        SubscriptionManager.AddSubscription<T, TH>();
    }

    public override void UnSubscribe<T, TH>() {
        var eventName = typeof(T).Name;

        try {
            // Subscription will be there but we don't subscribe
            var subscriptionClient = CreateSubscriptionClient(eventName);

            subscriptionClient
                .RemoveRuleAsync(eventName)
                .GetAwaiter()
                .GetResult();
        } catch(MessagingEntityNotFoundException) {
            _logger.LogWarning($"The messaging entity {eventName} could not be found.");
        }

        _logger.LogInformation($"Unsubscribing from event {eventName}");

        SubscriptionManager.RemoveSubscription<T,TH>();
    }

    private void ResigterSubscription(ISubscriptionClient subscriptionClient) {
        subscriptionClient.RegisterMessageHandler(
            async (message, token) => {
                var eventName = $"{message.Label}";
                var messageData = Encoding.UTF8.GetString(message.Body);

                // Complete the message so that it is not received again.
                if(await ProcessEvent(ProcessEventName(eventName), messageData)) {
                    await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
                }
            },
            new MessageHandlerOptions(ExceptionReceivedHandler) {
                MaxConcurrentCalls = 10,
                AutoComplete = default
            });
    }

    private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs) {
        var exception = exceptionReceivedEventArgs.Exception;
        var exceptionContext = exceptionReceivedEventArgs.ExceptionReceivedContext;

        _logger.LogError(exception, $"ERROR Handling Message: {exception.Message} - Context: {exceptionContext}");

        return Task.CompletedTask;
    }

    private ISubscriptionClient CreateSubscriptionClientIfNotExist(String eventName) {
        var subscriptionClient = CreateSubscriptionClient(eventName);
        var exist = _managementClient?.SubscriptionExistsAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName)).GetAwaiter().GetResult();
        if(exist is false) {
            _managementClient.CreateSubscriptionAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName)).GetAwaiter().GetResult();
            RemoveDefaultRule(subscriptionClient);
        }

        CreateRuleIfNotExist(ProcessEventName(eventName), subscriptionClient);

        return subscriptionClient;
    }

    private void CreateRuleIfNotExist(String eventName, ISubscriptionClient subscriptionClient) {
        Boolean ruleExists;

        try {
            var rule = _managementClient?.GetRuleAsync(EventBusConfig.DefaultTopicName, eventName, eventName).GetAwaiter().GetResult();
            ruleExists = rule is null;
        } catch(MessagingEntityNotFoundException) {
            // Azure Management Client doesn't have RuleExists method
            ruleExists = false;
        }

        if(ruleExists is false) {
            subscriptionClient.AddRuleAsync(new RuleDescription {
                Filter = new CorrelationFilter {
                    Label = eventName
                },
                Name = eventName
            }).GetAwaiter().GetResult();
        }
    }

    private void RemoveDefaultRule(SubscriptionClient subscriptionClient) {
        try {
            subscriptionClient.RemoveRuleAsync(RuleDescription.DefaultRuleName)
                .GetAwaiter()
                .GetResult();
        } catch(MessagingEntityNotFoundException) {
            _logger.LogWarning($"The messaging entity {RuleDescription.DefaultRuleName} could not be found.");
        }
    }
    private SubscriptionClient CreateSubscriptionClient(String eventName)
        => new(EventBusConfig.EventBusConnectionString, EventBusConfig.DefaultTopicName, GetSubName(eventName));

    public override void Dispose() {
        base.Dispose();

        _topicClient?.CloseAsync().GetAwaiter().GetResult();
        _managementClient?.CloseAsync().GetAwaiter().GetResult();
        _topicClient = null;
        _managementClient = null;
    }
}