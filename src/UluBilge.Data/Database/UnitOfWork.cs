using System.Data.Common;
using System.Runtime.ExceptionServices; // Hatayi ozgun cagrilma iziyle yeniden firlatmak icin kullanilir.
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using UluBilge.Core.Exceptions; // Commit gerektiren hatayi CommitException turunden tanimak icin kullanilir.
using UluBilge.Data.Abstract;

namespace UluBilge.Data.Database
{
    public sealed class UnitOfWork(DatabaseConnectionSettings settings, ILogger<UnitOfWork> logger) : IUnitOfWork
    {
        private DbConnection? _connection;
        private DbTransaction? _transaction;
        private int _operationActive;

        public DbConnection Connection => _connection ?? throw new InvalidOperationException("Veritabanı bağlantısına yalnızca etkin bir iş birimi işlemi içinden erişilebilir.");

        public DbTransaction? Transaction => _transaction;

        public Task<TResult> ExecuteQueryAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
        {
            return ExecuteAsync(action, isCommand: false, ct);
        }

        public Task<TResult> ExecuteCommandAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
        {
            return ExecuteAsync(action, isCommand: true, ct);
        }

        private async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, bool isCommand, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0)
            {
                throw new InvalidOperationException("Aynı iş birimi üzerinde iç içe veya eş zamanlı işlemler çalıştırılamaz.");
            }

            try
            {
                await using var connection = new SqlConnection(isCommand ? settings.WriteConnection : settings.ReadConnection);
                await connection.OpenAsync(cancellationToken);
                _connection = connection;

                if (!isCommand)
                {
                    return await action(cancellationToken);
                }

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                _transaction = transaction;
                TResult result; // Handler sonucunu try disinda tutar; commit sonrasinda dondurulebilmesini saglar.
                ExceptionDispatchInfo? committedFailure = null; // Commit tamamlanana kadar yeniden firlatilacak hatayi saklar.
                try
                {
                    try // CommitException'i ayri yakalayarak hatanin commit adimini engellemesini onler.
                    {
                        result = await action(cancellationToken); // Handler'i calistirir; basarili sonucu commit sonrasinda dondurmek icin saklar.
                    }
                    catch (CommitException commitException) // Yalnizca degisikliklerin kaydedilmesini isteyen hata turunu yakalar.
                    {
                        committedFailure = ExceptionDispatchInfo.Capture(commitException); // Hatanin olustugu cagrilma izini korur; hemen firlatmayarak commit'e izin verir.
                        result = default!; // Derleyicinin atama kosulunu saglar; hata yeniden firlatilacagindan bu sonuc donulmez.
                    }
                    await transaction.CommitAsync(cancellationToken); // Basarida veya CommitException durumunda kaydeder; commit hatasi dis catch ile rollback'e gider.
                }
                catch
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch (Exception rollbackException)
                    {
                        logger.LogError(rollbackException, "Veritabanı işlemi geri alınamadı.");
                    }

                    throw;
                }
                finally
                {
                    _transaction = null;
                }

                committedFailure?.Throw(); // Commit sonrasi hatayi ozgun iziyle iletir; rollback catch blogunun disinda oldugu icin kayit geri alinmaz.
                return result; // Yeniden firlatilacak hata yoksa kaydedilmis islemin sonucunu dondurur.
            }
            finally
            {
                _transaction = null;
                _connection = null;
                Volatile.Write(ref _operationActive, 0);
            }
        }
    }
}
