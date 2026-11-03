namespace UluBilge.Core.Exceptions
{
    // İşlemdeki değişiklikler kaydedildikten sonra istemciye iletilmesi gereken hata.
    public sealed class CommitException(string message = "İşlem gerçekleştirilemedi, veri kaydedildi.") : Exception(message)
    {

    }
}
