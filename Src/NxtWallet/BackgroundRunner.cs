using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Threading;
using NxtLib;
using NxtWallet.Model;
using NxtWallet.ViewModel.Model;
using Transaction = NxtWallet.ViewModel.Model.Transaction;

namespace NxtWallet
{
    public interface IBackgroundRunner
    {
        Task Run(CancellationToken token);

        event TransactionHandler TransactionConfirmationUpdated;
        event TransactionHandler TransactionBalanceUpdated;
        event TransactionHandler TransactionAdded;
        event BalanceHandler BalanceUpdated;
    }

    public delegate void TransactionHandler(IBackgroundRunner sender, Transaction transaction);
    public delegate void BalanceHandler(IBackgroundRunner sender, string balance);

    public class BackgroundRunner : IBackgroundRunner
    {
        private readonly INxtServer _nxtServer;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBalanceCalculator _balanceCalculator;
        private readonly IWalletRepository _walletRepository;
        private readonly IContactRepository _contactRepository;
        private readonly IAssetTracker _assetTracker;

        public event TransactionHandler TransactionConfirmationUpdated;
        public event TransactionHandler TransactionBalanceUpdated;
        public event TransactionHandler TransactionAdded;
        public event BalanceHandler BalanceUpdated;

        public BackgroundRunner(INxtServer nxtServer, ITransactionRepository transactionRepository,
            IBalanceCalculator balanceCalculator, IWalletRepository walletRepository,
            IContactRepository contactRepository, IAssetTracker assetTracker)
        {
            _nxtServer = nxtServer;
            _transactionRepository = transactionRepository;
            _balanceCalculator = balanceCalculator;
            _walletRepository = walletRepository;
            _contactRepository = contactRepository;
            _assetTracker = assetTracker;
        }

        public async Task Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var updatedTransactions = new List<Transaction>();
                try
                {
                    var knownTransactions = (await _transactionRepository.GetAllTransactionsAsync()).ToList();
                    var nxtTransactions = (await _nxtServer.GetTransactionsAsync()).ToList();
                    var balanceResult = await _nxtServer.GetBalanceAsync();

                    var newTransactions = nxtTransactions.Except(knownTransactions).ToList();
                    
                    if (!_balanceCalculator.BalanceEqualsLastTransactionBalance(nxtTransactions, knownTransactions, updatedTransactions, balanceResult))
                    {
                        var tradesResult = (await _nxtServer.GetAssetTradesAsync(_walletRepository.LastAssetTrade)).ToList();
                        var newTrades = tradesResult.Except(knownTransactions).ToList();
                        newTransactions.AddRange(newTrades);

                        if (tradesResult.Any())
                        {
                            await _walletRepository.UpdateLastAssetTrade(tradesResult.Max(t => t.Timestamp).AddSeconds(1));
                        }
                    }

                    await _assetTracker.UpdateAssetOwnership(newTransactions);

                    if (newTransactions.Any(t => t.TransactionType == TransactionType.DividendPayment))
                    {
                        var dividendTransactions = newTransactions.Where(t => t.TransactionType == TransactionType.DividendPayment).ToList();
                        foreach (var dividendTransaction in dividendTransactions)
                        {
                            var attachment = (ColoredCoinsDividendPaymentAttachment) dividendTransaction.Attachment;
                            var myOwnership = await _assetTracker.GetOwnership(attachment.AssetId, attachment.Height);
                            var quantityQnt = await _assetTracker.GetAssetQuantity(attachment.AssetId, attachment.Height);

                            var recipientQnt = quantityQnt - myOwnership.BalanceQnt;
                            var expenseNqt = attachment.AmountPerQnt.Nqt*recipientQnt;
                            dividendTransaction.NqtAmount = expenseNqt;
                        }
                    }
                    
                    updatedTransactions.AddRange(GetTransactionsWithUpdatedConfirmation(knownTransactions, nxtTransactions, newTransactions));
                    updatedTransactions.AddRange(await HandleNewTransactions(newTransactions, knownTransactions));
                    await HandleUpdatedTransactions(updatedTransactions);
                    await HandleBalance(balanceResult, newTransactions, knownTransactions);

                    await _assetTracker.SaveOwnerships();

                    await Task.Delay(_walletRepository.SleepTime, token);
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }

        private async Task HandleUpdatedTransactions(List<Transaction> updatedTransactions)
        {
            await _transactionRepository.UpdateTransactionsAsync(updatedTransactions.Distinct());
            updatedTransactions.ForEach(OnTransactionConfirmationUpdated);
        }

        private async Task<IEnumerable<Transaction>> HandleNewTransactions(List<Transaction> newTransactions, IEnumerable<Transaction> knownTransactions)
        {
            var updated = new List<Transaction>();
            if (newTransactions.Any())
            {
                await UpdateTransactionContacts(newTransactions);
                var allTransactions = knownTransactions.Union(newTransactions).OrderBy(t => t.Timestamp).ToList();
                updated = _balanceCalculator.Calculate(newTransactions, allTransactions).ToList();
                await _transactionRepository.SaveTransactionsAsync(newTransactions);

                newTransactions.ForEach(OnTransactionAdded);
            }
            return updated;
        }

        private async Task UpdateTransactionContacts(List<Transaction> newTransactions)
        {
            var accountsRs =
                newTransactions.Select(t => t.AccountFrom).Union(newTransactions.Select(t => t.AccountTo)).Distinct();
            var contacts = (await _contactRepository.GetContactsAsync(accountsRs)).ToDictionary(contact => contact.NxtAddressRs);
            newTransactions.ForEach(t => t.UpdateWithContactInfo(contacts));
        }

        private async Task HandleBalance(long confirmedBalanceNqt, IEnumerable<Transaction> newTransactions, List<Transaction> knownTransactions)
        {
            var unconfirmedBalanceNqt = newTransactions.Union(knownTransactions)
                .Where(t => t.UserIsRecipient && !t.IsConfirmed)
                .Sum(t => t.NqtAmount);

            var balanceNqt = unconfirmedBalanceNqt + confirmedBalanceNqt;
            var balance = balanceNqt.NqtToNxt().ToFormattedString();
            if (balance != _walletRepository.Balance)
            {
                await _walletRepository.UpdateBalanceAsync(balance);
                OnBalanceUpdated(balance);
            }
        }

        private static List<Transaction> GetTransactionsWithUpdatedConfirmation(IEnumerable<Transaction> knownTransactions, 
            IEnumerable<Transaction> nxtTransactions, IEnumerable<Transaction> newTransactions)
        {
            var updatedTransactions = knownTransactions
                .Where(t => t.IsConfirmed == false)
                .Except(nxtTransactions.Where(t => t.IsConfirmed == false))
                .Except(newTransactions)
                .ToList();

            updatedTransactions.ForEach(t => t.IsConfirmed = true);
            return updatedTransactions;
        }

        protected virtual void OnTransactionConfirmationUpdated(Transaction transaction)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() => TransactionConfirmationUpdated?.Invoke(this, transaction));
        }

        protected virtual void OnTransactionBalanceUpdated(Transaction transaction)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() => TransactionBalanceUpdated?.Invoke(this, transaction));
        }

        protected virtual void OnTransactionAdded(Transaction transaction)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() => TransactionAdded?.Invoke(this, transaction));
        }

        protected virtual void OnBalanceUpdated(string balance)
        {
            DispatcherHelper.CheckBeginInvokeOnUI(() => BalanceUpdated?.Invoke(this, balance));
        }
    }
}