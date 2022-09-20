using EventBus.Base.Abstraction;
using EventBus.Base.Events;

namespace EventBus.Base.SubscriptionManagers;
public class InMemoryEventBusSubscriptionManager : IEventBusSubscriptionManager {
    private readonly Dictionary<String, List<SubscriptionInfo>> _handlers;
    private readonly List<Type> _eventTypes;

    public Func<String, String> eventNameGetter;

    public InMemoryEventBusSubscriptionManager(Func<String, String> eventNameGetter) {
        this.eventNameGetter = eventNameGetter;
        _handlers = new();
        _eventTypes = new();
    }

    public Boolean IsEmpty => _handlers.Keys.Any() is false;

    public event EventHandler<String>? OnEventRemoved;

    public void AddSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T> {
        this.AddSubscription(typeof(TH), GetEventKey<T>());

        if(_eventTypes.Contains(typeof(T)) is false) {
            _eventTypes.Add(typeof(T));
        }
    }

    private void AddSubscription(Type handlerType, String eventName) {
        if(HasSubscriptionForEvent(eventName) is false) {
            _handlers.Add(eventName, new());
        }

        if(_handlers[eventName].Any(s => s.HandlerType.Equals(handlerType))) {
            throw new ArgumentException($"Handler Type {handlerType.Name} alredy registered for '{eventName}'", nameof(handlerType));
        }

        _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
    }

    public void Clear() => _handlers.Clear();
    public String GetEventKey<T>() where T : IntegrationEvent => eventNameGetter(typeof(T).Name);
    public Type? GetEventTypeByName(String eventName) => _eventTypes.SingleOrDefault(x => x.Name.Equals(eventName));


    public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
        => GetHandlersForEvent(GetEventKey<T>());
    public IEnumerable<SubscriptionInfo> GetHandlersForEvent(String eventName)
        => _handlers[eventName];


    public Boolean HasSubscriptionForEvent<T>() where T : IntegrationEvent 
        => HasSubscriptionForEvent(GetEventKey<T>());
    public Boolean HasSubscriptionForEvent(String eventName)
        => _handlers.ContainsKey(eventName);


    public void RemoveSubscription<T, TH>() where T : IntegrationEvent  where TH : IIntegrationEventHandler<T>
        => this.RemoveHandler(GetEventKey<T>(), this.FindSubscriptionRemove<T, TH>());
    private void RemoveHandler(String eventName, SubscriptionInfo? subscriptionToRemove) {
        if(subscriptionToRemove is not null) {
            _handlers[eventName].Remove(subscriptionToRemove);

            if(_handlers[eventName].Any() is false) {
                var eventType = _eventTypes.SingleOrDefault(e => e.Name.Equals(eventName));

                if(eventType is not null) {
                    _eventTypes.Remove(eventType);
                }

                this.RaiseOnEventRemoved(eventName);
            }
        }
    }
    private void RaiseOnEventRemoved(String eventName)
        => OnEventRemoved?.Invoke(this, eventName);

    private SubscriptionInfo? FindSubscriptionRemove<T, TH>() where T : IntegrationEvent
        where TH : IIntegrationEventHandler<T> 
        => FindSubscriptionRemove(GetEventKey<T>(), typeof(TH));
    private SubscriptionInfo? FindSubscriptionRemove(String eventName, Type handlerType)
        => HasSubscriptionForEvent(eventName) is false ?
        null :
        _handlers[eventName].SingleOrDefault(s => s.HandlerType.Equals(handlerType));
}