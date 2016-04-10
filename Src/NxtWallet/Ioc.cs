﻿using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;
using NxtWallet.Controls;
using NxtWallet.Model;
using NxtWallet.ViewModel;
using NxtWallet.Views;

namespace NxtWallet
{
    public class Ioc
    {
        public OverviewViewModel OverviewViewModel => ServiceLocator.Current.GetInstance<OverviewViewModel>();
        public SendMoneyViewModel SendMoneyViewModel => ServiceLocator.Current.GetInstance<SendMoneyViewModel>();
        public TransactionDetailViewModel TransactionDetailViewModel => ServiceLocator.Current.GetInstance<TransactionDetailViewModel>();
        public TransactionListViewModel TransactionListViewModel => ServiceLocator.Current.GetInstance<TransactionListViewModel>();
        public SettingsViewModel SettingsViewModel => ServiceLocator.Current.GetInstance<SettingsViewModel>();
        public ReceiveMoneyViewModel ReceiveMoneyViewModel => ServiceLocator.Current.GetInstance<ReceiveMoneyViewModel>();
        public ContactsViewModel ContactsViewModel => ServiceLocator.Current.GetInstance<ContactsViewModel>();
        public BackupSecretPhraseViewModel BackupSecretPhraseViewModel => ServiceLocator.Current.GetInstance<BackupSecretPhraseViewModel>();
        public BackupConfirmViewModel BackupConfirmViewModel => ServiceLocator.Current.GetInstance<BackupConfirmViewModel>();

        static Ioc()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            if (GalaSoft.MvvmLight.ViewModelBase.IsInDesignModeStatic)
            {
                SimpleIoc.Default.Register<INxtServer, FakeNxtServer>();
                SimpleIoc.Default.Register<IWalletRepository, FakeWalletRepository>();
                SimpleIoc.Default.Register<ITransactionRepository, FakeTransactionRepository>();
                SimpleIoc.Default.Register<IContactRepository, FakeContactRepository>();
            }
            else
            {
                SimpleIoc.Default.Register<INxtServer, NxtServer>();
                SimpleIoc.Default.Register<IWalletRepository, WalletRepository>();
                SimpleIoc.Default.Register<ITransactionRepository, TransactionRepository>();
                SimpleIoc.Default.Register<IContactRepository, ContactRepository>();
            }
            var repo = ServiceLocator.Current.GetInstance<IWalletRepository>();

            SimpleIoc.Default.Register(() => MapperConfig.Setup(repo).CreateMapper());
            SimpleIoc.Default.Register<IBackgroundRunner, BackgroundRunner>();
            SimpleIoc.Default.Register<IBalanceCalculator, BalanceCalculator>();
            SimpleIoc.Default.Register<INavigationService, NavigationService>();
            SimpleIoc.Default.Register<ISendMoneyDialog, SendMoneyDialog>();
            SimpleIoc.Default.Register<IBackupInfoDialog, BackupInfoDialog>();
            SimpleIoc.Default.Register<IBackupDoneDialog, BackupDoneDialog>();
            SimpleIoc.Default.Register<OverviewViewModel>();
            SimpleIoc.Default.Register<SendMoneyViewModel>();
            SimpleIoc.Default.Register<TransactionListViewModel>();
            SimpleIoc.Default.Register<TransactionDetailViewModel>();
            SimpleIoc.Default.Register<SettingsViewModel>();
            SimpleIoc.Default.Register<ReceiveMoneyViewModel>();
            SimpleIoc.Default.Register<ContactsViewModel>();
            SimpleIoc.Default.Register<BackupSecretPhraseViewModel>();
            SimpleIoc.Default.Register<BackupConfirmViewModel>();
        }

        public static void Register()
        {
            // empty by design, logic is in static constructor
        }
    }
}
