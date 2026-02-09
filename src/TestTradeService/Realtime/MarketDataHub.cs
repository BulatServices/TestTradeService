using Microsoft.AspNetCore.SignalR;
using TestTradeService.Api.Contracts;

namespace TestTradeService.Realtime;

/// <summary>
/// SignalR-хаб рыночных данных.
/// </summary>
public sealed class MarketDataHub : Hub
{
    private readonly MarketHubConnectionStateStore _store;

    /// <summary>
    /// Инициализирует хаб рыночных данных.
    /// </summary>
    /// <param name="store">Хранилище подписок клиентов.</param>
    public MarketDataHub(MarketHubConnectionStateStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Устанавливает фильтр потока для текущего подключения.
    /// </summary>
    /// <param name="filter">Фильтр по бирже и символу.</param>
    /// <returns>Задача применения фильтра.</returns>
    public Task SetStreamFilter(StreamFilterDto filter)
    {
        _store.SetFilter(Context.ConnectionId, filter.Exchange, filter.Symbol);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Подписывает текущее подключение на поток тиков.
    /// </summary>
    /// <returns>Задача активации подписки на поток.</returns>
    public Task SubscribeStream()
    {
        _store.SetStreamSubscription(Context.ConnectionId, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Отписывает текущее подключение от потока тиков.
    /// </summary>
    /// <returns>Задача деактивации подписки на поток.</returns>
    public Task UnsubscribeStream()
    {
        _store.SetStreamSubscription(Context.ConnectionId, false);
        _store.SetFilter(Context.ConnectionId, null, null);
        _store.ClearSymbols(Context.ConnectionId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Подписывает текущее подключение на набор символов.
    /// </summary>
    /// <param name="symbols">Список символов.</param>
    /// <returns>Задача добавления подписок.</returns>
    public Task SubscribeSymbols(IReadOnlyCollection<string> symbols)
    {
        _store.SubscribeSymbols(Context.ConnectionId, symbols);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Отписывает текущее подключение от набора символов.
    /// </summary>
    /// <param name="symbols">Список символов.</param>
    /// <returns>Задача удаления подписок.</returns>
    public Task UnsubscribeSymbols(IReadOnlyCollection<string> symbols)
    {
        _store.UnsubscribeSymbols(Context.ConnectionId, symbols);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Освобождает состояние подключения после отключения клиента.
    /// </summary>
    /// <param name="exception">Исключение завершения подключения.</param>
    /// <returns>Задача завершения соединения.</returns>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _store.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
