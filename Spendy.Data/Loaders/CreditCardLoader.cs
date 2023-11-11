﻿namespace Spendy.Data.Loaders
{
    using Spendy.Data.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using TrueLayer.API;
    using TrueLayer.API.Models;

    public class CreditCardLoader : Loader<TLCard, Card>
    {
        public CreditCardLoader(AuthService authService, TrueLayerAPI trueLayerApi, LiteDBDatastore dataStore)
            : base(authService, trueLayerApi, dataStore)
        {
        }

        public async Task<Card[]> Load()
        {
            var creditCards = new List<Card>();

            var providers = _dataStore.FindAll<Auth>();
            foreach (var provider in providers)
            {
                creditCards.AddRange(await Load(provider.Id));
            }

            return creditCards.ToArray();
        }

        protected override DateTime GetLastUpdateTime(Auth auth, string accountId = null)
        {
            var allCards = _dataStore.Find<Card>(x => x.AuthId == auth.Id);
            return allCards?.Length > 0 ? allCards.Min(x => x.LastUpdated) : DateTime.MinValue;
        }

        protected override async Task<TLApiResponse<TLCard>> FetchApiData(Auth auth, string accountId = null)
        {
            var cards = await _trueLayerApi.GetCards(auth.AccessToken);

            if (cards?.Results?.Length > 0)
            {
                foreach (var card in cards.Results)
                {
                    card.Balance = (await _trueLayerApi.GetCardBalance(auth.AccessToken, card.AccountId)).Results.First();
                }
            }

            return cards;
        }

        protected override Card[] FetchDatabaseData(Auth auth, string accountId = null)
        {
            return _dataStore.Find<Card>(x => x.AuthId == auth.Id);
        }

        protected override Card[] MapToClasses(Auth auth, TLCard[] data, string accountId = null)
        {
            var newCards = new List<Card>();

            foreach (var card in data)
            {
                newCards.Add(new Card
                {
                    AuthId = auth.Id,
                    AccountId = card.AccountId,
                    DisplayName = card.DisplayName,
                    LogoUri = card.Provider.LogoUri,
                    AvailableBalance = card.Balance.Available,
                    CurrentBalance = card.Balance.Current,
                    CreditLimit = card.Balance.CreditLimit,
                    LastStatementBalance = card.Balance.LastStatementBalance,
                    LastStatementDate = card.Balance.LastStatementDate,
                    PaymentDue = card.Balance.PaymentDue,
                    PaymentDueDate = card.Balance.PaymentDueDate,
                    LastUpdated = card.UpdateTimestamp
                });
            }

            return newCards.ToArray();
        }

        protected override void SaveToDatabase(Auth auth, Card[] newCards, string accountId = null)
        {
            _dataStore.DeleteMany<Card>(x => x.AuthId == auth.Id);
            _dataStore.InsertMany<Card>(newCards.ToArray());
        }
    }
}
