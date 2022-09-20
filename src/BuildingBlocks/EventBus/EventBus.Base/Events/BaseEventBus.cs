using EventBus.Base.Abstraction;
using EventBus.Base.SubscriptionManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace EventBus.Base.Events;
public abstract class BaseEventBus : IEventBus {
    public readonly IServiceProvider ServiceProvider;
    public readonly IEventBusSubscriptionManager SubscriptionManager;
    protected EventBusConfig EventBusConfig { get; set; }

    public BaseEventBus(
        EventBusConfig eventBusConfig,
        IServiceProvider serviceProvider,
        IEventBusSubscriptionManager subscriptionManager
        ) {
        this.EventBusConfig = eventBusConfig;
        this.ServiceProvider = serviceProvider;
        this.SubscriptionManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
    }

    public virtual String ProcessEventName(String eventName) {
        if(EventBusConfig.DeleteEventPrefix)
            eventName = eventName.TrimStart(EventBusConfig.EventNamePrefix.ToArray());
        if(EventBusConfig.DeleteEventSuffix)
            eventName = eventName.TrimEnd(EventBusConfig.EventNameSuffix.ToArray());
        return eventName;
    }

    public virtual String GetSubName(String eventName)
        => $"{EventBusConfig?.SubscriberClientApplicationName}.{ProcessEventName(eventName)}";
    public virtual void Dispose() {
        EventBusConfig = null;
        SubscriptionManager.Clear();
    }

    public async Task<Boolean> ProcessEvent(String eventName, String message) {
        eventName = ProcessEventName(eventName);

        var processed = default(Boolean);

        if(SubscriptionManager.HasSubscriptionForEvent(eventName)) { //event takip ediliyor mu dinliyorsak devam ediyoruz içinde
            var subscriptions = SubscriptionManager.GetHandlersForEvent(eventName); //kaç kişi subscriber oluyo
            await using var scope = ServiceProvider.CreateAsyncScope();

            foreach(var subscription in subscriptions) { //subscriplera gönderiyoruz
                var handler = ServiceProvider.GetService(subscription.HandlerType); //DI ile servisi alıyoruz dinamik olarak
                if(handler is null)
                    continue;
                //tipe ulaşıyoruz
                var eventType = SubscriptionManager.GetEventTypeByName($"{EventBusConfig.EventNamePrefix}{eventName}{EventBusConfig.EventNameSuffix}");
                var integrationEvent = JsonConvert.DeserializeObject(message, eventType);

                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType); //reflection ile metoda ulaşıyoruz
                await (Task)concreteType.GetMethod("Handle").Invoke(handler, new[] { integrationEvent });
            }
            processed = true;
        }
        return processed;
    }

    public abstract void Publish(IntegrationEvent @event);
    public abstract void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    public abstract void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
}