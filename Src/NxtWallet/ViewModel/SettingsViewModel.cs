﻿using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using NxtWallet.Model;

namespace NxtWallet.ViewModel
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IWalletRepository _walletRepository;
        private readonly INxtServer _nxtServer;
        private string _serverAddress;

        public string ServerAddress
        {
            get { return _serverAddress; }
            set { Set(ref _serverAddress, value); }
        }
        public RelayCommand SaveCommand { get; }

        public SettingsViewModel(IWalletRepository walletRepository, INxtServer nxtServer)
        {
            _walletRepository = walletRepository;
            _nxtServer = nxtServer;
            SaveCommand = new RelayCommand(Save);
            ServerAddress = _walletRepository.NxtServer;
        }

        private async void Save()
        {
            _nxtServer.UpdateNxtServer(_serverAddress);
            await Task.Run(async () => await _walletRepository.UpdateNxtServer(_serverAddress));
        }
    }
}