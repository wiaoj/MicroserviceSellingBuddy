﻿using Newtonsoft.Json;

namespace EventBus.Base.Events;
public class IntegrationEvent {

    [JsonProperty]
    public Guid Id { get; private set; }
    [JsonProperty]
    public DateTime CreatedDate { get; private set; }

    public IntegrationEvent() {
        Id = Guid.NewGuid();
        CreatedDate = DateTime.Now;
    }

    [JsonConstructor] //bununla gerçekleştiği zaman dışarıdan gelen id ve createdDate ile set etmemizi sağlıyo
    public IntegrationEvent(Guid id, DateTime createdDate) {
        Id = id;
        CreatedDate = createdDate;
    }
}