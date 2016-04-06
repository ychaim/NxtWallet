using System.Threading.Tasks;
using NxtLib;

namespace NxtWallet.Model
{
    public interface IWalletRepository
    {
        AccountWithPublicKey NxtAccount { get; }
        string NxtServer { get; }
        string SecretPhrase { get; }
        string Balance { get; }
        bool BackupCompleted { get; }

        Task LoadAsync();
        Task SaveBalanceAsync(string balance);
        Task UpdateNxtServer(string newServerAddress);
    }
}