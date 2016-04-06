﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using GalaSoft.MvvmLight;
using Newtonsoft.Json;
using NxtLib;
using NxtLib.Accounts;
using NxtLib.Local;
using NxtLib.Transactions;
using NxtWallet.Model;
using NxtWallet.ViewModel.Model;
using Transaction = NxtWallet.Model.Transaction;

namespace NxtWallet
{
    public interface INxtServer
    {
        event PropertyChangedEventHandler PropertyChanged;

        bool IsOnline { get; }

        Task<Result<string>> GetBalanceAsync();
        Task<IEnumerable<TransactionModel>> GetTransactionsAsync(DateTime lastTimestamp);
        Task<IEnumerable<TransactionModel>> GetTransactionsAsync();
        Task<Result<TransactionModel>> SendMoneyAsync(Account recipient, Amount amount, string message);
        void UpdateNxtServer(string newServerAddress);
    }

    public class NxtServer : ObservableObject, INxtServer
    {
        private readonly IWalletRepository _walletRepository;
        private readonly IMapper _mapper;
        private bool _isOnline;
        private IServiceFactory _serviceFactory;

        public bool IsOnline
        {
            get { return _isOnline; }
            set { Set(ref _isOnline, value); }
        }

        public NxtServer(IWalletRepository walletRepository, IMapper mapper)
        {
            _walletRepository = walletRepository;
            _mapper = mapper;
            IsOnline = false;
            _serviceFactory = new ServiceFactory(_walletRepository.NxtServer);
        }

        //TODO: Scrap Result class and instead cast one Exception type, wrapping the caught exception
        public async Task<Result<string>> GetBalanceAsync()
        {
            try
            {
                var accountService = _serviceFactory.CreateAccountService();
                var balanceResult = await accountService.GetBalance(_walletRepository.NxtAccount);
                IsOnline = true;
                return new Result<string>(balanceResult.UnconfirmedBalance.Nxt.ToFormattedString());
            }
            catch (HttpRequestException)
            {
                IsOnline = false;
            }
            catch (JsonReaderException)
            {
                IsOnline = false;
            }
            catch (NxtException e)
            {
                if (!e.Message.Equals("Unknown account"))
                {
                    throw;
                }
            }
            return new Result<string>(string.Empty, false);
        }

        //TODO: Phased transactions?
        public async Task<IEnumerable<TransactionModel>> GetTransactionsAsync(DateTime lastTimestamp)
        {
            var transactionList = new List<TransactionModel>();
            try
            {
                var transactionService = _serviceFactory.CreateTransactionService();
                var transactionsReply = await transactionService.GetBlockchainTransactions(
                    _walletRepository.NxtAccount, lastTimestamp, TransactionSubType.PaymentOrdinaryPayment);
                var unconfirmedReply =
                    await
                        transactionService.GetUnconfirmedTransactions(new List<Account> {_walletRepository.NxtAccount});

                transactionList.AddRange(_mapper.Map<List<TransactionModel>>(transactionsReply.Transactions));
                transactionList.AddRange(_mapper.Map<List<TransactionModel>>(unconfirmedReply.UnconfirmedTransactions));;
                IsOnline = true;
            }
            catch (HttpRequestException)
            {
                IsOnline = false;
            }
            catch (JsonReaderException)
            {
                IsOnline = false;
            }
            return transactionList.OrderByDescending(t => t.Timestamp);
        }

        public Task<IEnumerable<TransactionModel>> GetTransactionsAsync()
        {
            return GetTransactionsAsync(new DateTime(2013, 11, 24, 12, 0, 0, DateTimeKind.Utc));
        }

        //TODO: Use one known exception instead of Result<>
        public async Task<Result<TransactionModel>> SendMoneyAsync(Account recipient, Amount amount, string message)
        {
            try
            {
                var accountService = _serviceFactory.CreateAccountService();
                var transactionService = _serviceFactory.CreateTransactionService();
                var localTransactionService = new LocalTransactionService();

                var sendMoneyReply = await CreateUnsignedSendMoneyReply(recipient, amount, message, accountService);
                var signedTransaction = localTransactionService.SignTransaction(sendMoneyReply, _walletRepository.SecretPhrase);
                var broadcastReply = await transactionService.BroadcastTransaction(new TransactionParameter(signedTransaction.ToString()));

                IsOnline = true;

                var transaction = _mapper.Map<TransactionModel>(sendMoneyReply.Transaction);
                transaction.NxtId = broadcastReply.TransactionId;
                return new Result<TransactionModel>(transaction);
            }
            catch (HttpRequestException)
            {
                IsOnline = false;
            }
            catch (JsonReaderException)
            {
                IsOnline = false;
            }
            return new Result<TransactionModel>();
        }

        public void UpdateNxtServer(string newServerAddress)
        {
            _serviceFactory = new ServiceFactory(newServerAddress);
        }

        private async Task<TransactionCreatedReply> CreateUnsignedSendMoneyReply(Account recipient, Amount amount, string message,
            IAccountService accountService)
        {
            var createTransactionByPublicKey = new CreateTransactionByPublicKey(1440, Amount.Zero, _walletRepository.NxtAccount.PublicKey);
            if (!string.IsNullOrEmpty(message))
            {
                createTransactionByPublicKey.Message = new CreateTransactionParameters.UnencryptedMessage(message);
            }
            var sendMoneyReply = await accountService.SendMoney(createTransactionByPublicKey, recipient, amount);
            return sendMoneyReply;
        }
    }
}
