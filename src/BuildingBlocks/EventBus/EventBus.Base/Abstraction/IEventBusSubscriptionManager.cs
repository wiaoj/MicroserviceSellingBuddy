using EventBus.Base.Events;

namespace EventBus.Base.Abstraction;
public interface IEventBusSubscriptionManager {
    Boolean IsEmpty { get; } //event dinleniyor mu
    event EventHandler<String> OnEventRemoved; //unsubscription çalışınca çalışacak
    void AddSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    void RemoveSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    Boolean HasSubscriptionForEvent<T>() where T : IntegrationEvent; //event gelince dinleyecek mi diye kontrol edecek
    Boolean HasSubscriptionForEvent(String eventName);
    Type? GetEventTypeByName(String eventName);
    void Clear();
    IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T: IntegrationEvent;//eventin tüm subslarını döneceğimiz bir metod
    IEnumerable<SubscriptionInfo> GetHandlersForEvent(String eventName);
    String GetEventKey<T>() where T : IntegrationEvent; //integration event key
}