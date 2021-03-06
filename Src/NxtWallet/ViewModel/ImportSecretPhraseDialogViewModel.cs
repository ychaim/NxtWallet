﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NxtLib.Local;
using NxtWallet.Core;
using NxtWallet.Core.Repositories;
using System.Threading.Tasks;

namespace NxtWallet.ViewModel
{
    public class ImportSecretPhraseDialogViewModel : ViewModelBase
    {
        private readonly IWalletRepository _walletRepository;
        private readonly IAccountLedgerRepository _accountLedgerRepository;

        private string _secretPhrase;

        public string SecretPhrase
        {
            get { return _secretPhrase; }
            set
            {
                Set(ref _secretPhrase, value);
                ImportSecretPhraseCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand ImportSecretPhraseCommand { get; }

        public ImportSecretPhraseDialogViewModel(IWalletRepository walletRepository, IAccountLedgerRepository accountLedgerRepository)
        {
            _walletRepository = walletRepository;
            _accountLedgerRepository = accountLedgerRepository;

            ImportSecretPhraseCommand = new RelayCommand(DoImport, () => !string.IsNullOrEmpty(SecretPhrase));
        }

        private async void DoImport()
        {
            await Task.Run(async () =>
            {
                await Task.WhenAll(new[]
                {
                    _accountLedgerRepository.DeleteAllLedgerEntriesAsync(),
                    _walletRepository.UpdateSecretPhraseAsync(SecretPhrase),
                    _walletRepository.UpdateLastLedgerEntryBlockIdAsync(Constants.GenesisBlockId),
                    _walletRepository.UpdateBalanceAsync(0),
                    _walletRepository.UpdateBackupCompletedAsync(true)
                });
            });
            MessengerInstance.Send(new SecretPhraseResetMessage());
        }
    }
}
