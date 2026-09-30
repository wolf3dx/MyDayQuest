using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MyDayQuest.Data;

namespace MyDayQuest.Cloud;

public enum CloudSyncOutcome { Disabled, UpToDate, Pushed, Pulled, Failed }

public record CloudSyncReport(CloudSyncOutcome Outcome, string Message);

/// <summary>
/// Держит файл MyDayQuest.mdq в облаке в актуальном виде САМ: после входа приложение
/// больше ничего не спрашивает — фоновый таймер видит изменения в базе и выгружает их,
/// а появление более свежего файла в облаке подтягивает обратно.
/// </summary>
public class CloudSyncManager : IAsyncDisposable
{
    /// <summary>Как часто смотрим, не изменились ли локальные данные.</summary>
    private static readonly TimeSpan LocalCheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>Как часто спрашиваем облако, не появился ли более свежий файл.</summary>
    private static readonly TimeSpan RemoteCheckInterval = TimeSpan.FromMinutes(2);

    private readonly AppDatabase _db;
    private readonly SyncService _sync;
    private readonly CloudStorageFactory _factory;
    private readonly CloudSyncState _state;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ICloudStorage? _storage;
    private CancellationTokenSource? _loopCts;
    private Task? _loop;
    private DateTime _lastRemoteCheckUtc = DateTime.MinValue;

    public CloudSyncManager(AppDatabase db, SyncService sync, CloudStorageFactory factory, string stateFolder)
    {
        _db = db;
        _sync = sync;
        _factory = factory;
        _state = CloudSyncState.Load(stateFolder);
        _storage = _factory.Create(_state.Provider);
    }

    /// <summary>Данные пришли из облака — голова должна перечитать экран.</summary>
    public event EventHandler? PulledFromCloud;

    /// <summary>Изменился статус: подключение, время последней синхронизации, ошибка.</summary>
    public event EventHandler<CloudSyncReport>? StatusChanged;

    public CloudProvider Provider => _state.Provider;
    public ICloudStorage? Storage => _storage;
    public bool IsEnabled => _storage is not null && _storage.IsSignedIn;
    public DateTime? LastSyncUtc => _state.LastSyncUtc;

    public string StatusText => _storage is null
        ? "Облако не подключено"
        : !_storage.IsSignedIn
            ? $"{Provider.Display()}: нужен вход"
            : _state.LastSyncUtc is { } t
                ? $"{Provider.Display()}: синхронизировано {t.ToLocalTime():dd.MM HH:mm}"
                : $"{Provider.Display()}: подключено";

    public bool IsConfigured(CloudProvider provider) => _factory.IsConfigured(provider);

    /// <summary>Подключить облако: вход через браузер + первая синхронизация.</summary>
    public async Task<CloudSyncReport> ConnectAsync(CloudProvider provider, CancellationToken ct = default)
    {
        var storage = _factory.Create(provider)
                      ?? throw new ArgumentOutOfRangeException(nameof(provider), "Неизвестный провайдер облака.");

        await storage.SignInAsync(ct);

        _storage = storage;
        _state.Provider = provider;
        _state.LastSyncedHash = string.Empty;
        _state.LastRemoteModifiedUtc = null;
        _state.Save();

        var report = await SyncNowAsync(ct, force: true);
        Start();
        return report;
    }

    /// <summary>
    /// Подключить WebDAV-хранилище логином и паролем — без регистрации приложения.
    /// </summary>
    public async Task<CloudSyncReport> ConnectWebDavAsync(
        string server, string user, string password, CancellationToken ct = default)
    {
        var storage = _factory.Create(CloudProvider.WebDav) as WebDavStorage
                      ?? throw new InvalidOperationException("WebDAV недоступен.");

        await storage.ConnectAsync(server, user, password, ct);

        _storage = storage;
        _state.Provider = CloudProvider.WebDav;
        _state.LastSyncedHash = string.Empty;
        _state.LastRemoteModifiedUtc = null;
        _state.Save();

        var report = await SyncNowAsync(ct, force: true);
        Start();
        return report;
    }

    /// <summary>Отключить облако: забыть токены и перестать что-либо выгружать.</summary>
    public void Disconnect()
    {
        _storage?.SignOut();
        _storage = null;
        _state.Provider = CloudProvider.None;
        _state.LastSyncedHash = string.Empty;
        _state.LastRemoteModifiedUtc = null;
        _state.LastSyncUtc = null;
        _state.Save();
        StatusChanged?.Invoke(this, new CloudSyncReport(CloudSyncOutcome.Disabled, "Облако отключено."));
    }

    /// <summary>Запустить фоновое слежение (вызывается при старте приложения).</summary>
    public void Start()
    {
        if (_loop is not null || !IsEnabled) return;
        _loopCts = new CancellationTokenSource();
        _loop = LoopAsync(_loopCts.Token);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(LocalCheckInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await SyncNowAsync(ct);
        }
        catch (OperationCanceledException) { /* штатная остановка */ }
    }

    /// <summary>
    /// Одна попытка синхронизации. Локальные изменения выгружаются сразу, облако
    /// опрашивается не чаще RemoteCheckInterval (force снимает это ограничение).
    /// </summary>
    public async Task<CloudSyncReport> SyncNowAsync(CancellationToken ct = default, bool force = false)
    {
        if (_storage is null || !_storage.IsSignedIn)
            return new CloudSyncReport(CloudSyncOutcome.Disabled, "Облако не подключено.");

        if (!await _gate.WaitAsync(0, ct))
            return new CloudSyncReport(CloudSyncOutcome.UpToDate, "Синхронизация уже идёт.");

        try
        {
            var localHash = await _sync.ContentHashAsync();
            var localChanged = localHash != _state.LastSyncedHash;
            var checkRemote = force
                              || _state.LastSyncedHash.Length == 0
                              || DateTime.UtcNow - _lastRemoteCheckUtc >= RemoteCheckInterval;

            if (!localChanged && !checkRemote)
                return new CloudSyncReport(CloudSyncOutcome.UpToDate, "Изменений нет.");

            DateTime? remoteModified = null;
            if (checkRemote)
            {
                remoteModified = await _storage.GetModifiedUtcAsync(ct);
                _lastRemoteCheckUtc = DateTime.UtcNow;

                // Файла в облаке ещё нет — просто кладём свой.
                if (remoteModified is null)
                    return await PushAsync(localHash, ct);
            }

            var remoteIsNewer = remoteModified is { } rm &&
                                (_state.LastRemoteModifiedUtc is null || rm > _state.LastRemoteModifiedUtc);

            if (remoteIsNewer)
            {
                var json = await _storage.DownloadAsync(ct);
                if (json is not null)
                {
                    if (PayloadHash(json) == localHash)
                    {
                        // Содержимое совпало — расходимся только штампом времени.
                        Remember(localHash, remoteModified);
                        return Report(CloudSyncOutcome.UpToDate, "Уже синхронизировано.");
                    }

                    var remoteExported = SyncService.ReadExportedUtc(json) ?? remoteModified;
                    var localChangedUtc = await _db.GetLastChangeUtcAsync();

                    // Расхождение в обе стороны: побеждает тот, кто изменён позже.
                    if (localChanged && localChangedUtc > remoteExported)
                        return await PushAsync(localHash, ct);

                    await _sync.ImportReplaceAsync(json);
                    Remember(await _sync.ContentHashAsync(), remoteModified);
                    PulledFromCloud?.Invoke(this, EventArgs.Empty);
                    return Report(CloudSyncOutcome.Pulled, "Данные загружены из облака.");
                }
            }

            if (localChanged)
                return await PushAsync(localHash, ct);

            Remember(localHash, remoteModified);
            return Report(CloudSyncOutcome.UpToDate, "Уже синхронизировано.");
        }
        catch (Exception ex)
        {
            return Report(CloudSyncOutcome.Failed, $"Синхронизация не удалась: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<CloudSyncReport> PushAsync(string localHash, CancellationToken ct)
    {
        var json = await _sync.ExportJsonAsync();
        await _storage!.UploadAsync(json, ct);

        var modified = await _storage.GetModifiedUtcAsync(ct);
        _lastRemoteCheckUtc = DateTime.UtcNow;
        Remember(localHash, modified);
        return Report(CloudSyncOutcome.Pushed, "Изменения выгружены в облако.");
    }

    /// <summary>
    /// Отпечаток содержимого скачанного файла — той же меркой, что ContentHashAsync:
    /// штамп ExportedUtc в сравнении не участвует.
    /// </summary>
    private static string PayloadHash(string json)
    {
        var normalized = Regex.Replace(json,
            "\"ExportedUtc\"\\s*:\\s*\"[^\"]*\"", "\"ExportedUtc\": \"0001-01-01T00:00:00\"");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private void Remember(string hash, DateTime? remoteModified)
    {
        _state.LastSyncedHash = hash;
        if (remoteModified is not null) _state.LastRemoteModifiedUtc = remoteModified;
        _state.LastSyncUtc = DateTime.UtcNow;
        _state.Save();
    }

    private CloudSyncReport Report(CloudSyncOutcome outcome, string message)
    {
        var report = new CloudSyncReport(outcome, message);
        StatusChanged?.Invoke(this, report);
        return report;
    }

    public async ValueTask DisposeAsync()
    {
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
            if (_loop is not null)
            {
                try { await _loop; } catch (OperationCanceledException) { }
            }
            _loopCts.Dispose();
            _loopCts = null;
            _loop = null;
        }
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
